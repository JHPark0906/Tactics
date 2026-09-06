using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HS.Framework.Foundation.MVVM;
using HS.Framework.ProjectManagement;
using HS.Framework.Runtime;
using HS.Tactics.Flow;
using HS.Tactics.Placement;

namespace HS.Tactics.UI
{
    /// <summary>
    /// 메인 메뉴 목록에 놓이는 스테이지 하나이다.
    /// </summary>
    public readonly struct StageEntry
    {
        /// <summary>스테이지 식별자이며, 씬 카탈로그의 게임플레이 레벨 번호 그대로이다.</summary>
        public int StageId { get; }

        /// <summary>클리어한 것으로 표시할지 여부이다.</summary>
        public bool IsCleared { get; }

        /// <summary>목록 항목을 만든다.</summary>
        /// <param name="stageId">스테이지 식별자이다.</param>
        /// <param name="isCleared">클리어한 것으로 표시할지 여부이다.</param>
        public StageEntry(int stageId, bool isCleared)
        {
            StageId = stageId;
            IsCleared = isCleared;
        }
    }

    /// <summary>
    /// 메인 메뉴 화면의 상태와 동작을 담는 ViewModel이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 화면 요소를 알지 못한다. 버튼도 텍스트도 여기서는 보이지 않으며, View가 이 상태를 읽어
    /// 자기 요소에 옮긴다. 서비스 의존은 전부 이쪽에 모으고, View는 이 ViewModel 하나만 안다.
    /// </para>
    /// <para>
    /// 씬 전환은 <see cref="IGameFlowService"/>에 맡기므로 씬 경로는 알지 않는다. 스테이지 목록은
    /// 씬 카탈로그의 게임플레이 레벨 번호로 만들고, 클리어 표시는 <see cref="StageProgression.IsCleared"/>
    /// 하나로 판단한다. 해금은 없으므로 목록의 모든 스테이지를 처음부터 고를 수 있다.
    /// </para>
    /// </remarks>
    public sealed class TacticsMainMenuViewModel : ViewModelBase
    {
        private readonly IGameFlowService _gameFlowService;
        private readonly IProjectSceneCatalog _sceneCatalog;
        private readonly StageProgression _progression;
        private readonly PartySelectionService _partySelectionService;
        private readonly List<StageEntry> _stages = new();

        private bool _isBusy;
        private bool _isPlacementPanelOpen;
        private int _pendingStageId;

        /// <summary>지금 다른 일이 진행 중이어서 입력을 받지 않아야 하는지 여부이다.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanStartGame));
                }
            }
        }

        /// <summary>스테이지 버튼이 연 배치 패널이 지금 열려 있는지 여부이다.</summary>
        public bool IsPlacementPanelOpen
        {
            get => _isPlacementPanelOpen;
            private set
            {
                if (SetProperty(ref _isPlacementPanelOpen, value))
                {
                    OnPropertyChanged(nameof(CanStartGame));
                }
            }
        }

        /// <summary>배치 패널이 연 스테이지의 식별자이다. 패널이 닫혀 있을 때는 뜻이 없다.</summary>
        public int PendingStageId => _pendingStageId;

        /// <summary>
        /// 게임 시작이나 스테이지 선택을 지금 누를 수 있는지 여부이다.
        /// 배치 패널이 열려 있는 동안에는 그 패널만 조작해야 하므로 메인 메뉴 버튼을 함께 잠근다.
        /// </summary>
        public bool CanStartGame => !IsBusy && !IsPlacementPanelOpen;

        /// <summary>고를 수 있는 스테이지 목록이며 번호 오름차순이다. <see cref="Load"/>가 채운다.</summary>
        public IReadOnlyList<StageEntry> Stages => _stages;

        /// <summary>플레이어가 지금까지 깬 스테이지 수이며, 목록에서 센다.</summary>
        public int ClearedStageCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < _stages.Count; index++)
                {
                    if (_stages[index].IsCleared)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>필요한 서비스를 받아 ViewModel을 만든다.</summary>
        /// <param name="gameFlowService">새 게임이나 지정한 스테이지로 씬을 넘기는 서비스이다.</param>
        /// <param name="sceneCatalog">스테이지 목록의 근거가 되는 프로젝트 씬 카탈로그이다.</param>
        /// <param name="progression">클리어 표시를 읽어 올 스테이지 진행 상태이다.</param>
        /// <param name="partySelectionService">배치 패널이 확정한 선택을 게임플레이 씬 너머로 들고 갈 서비스이다.</param>
        public TacticsMainMenuViewModel(
            IGameFlowService gameFlowService,
            IProjectSceneCatalog sceneCatalog,
            StageProgression progression,
            PartySelectionService partySelectionService)
        {
            _gameFlowService = gameFlowService ?? throw new ArgumentNullException(nameof(gameFlowService));
            _sceneCatalog = sceneCatalog ?? throw new ArgumentNullException(nameof(sceneCatalog));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _partySelectionService = partySelectionService ?? throw new ArgumentNullException(nameof(partySelectionService));
        }

        /// <summary>화면이 열릴 때 스테이지 목록과 클리어 표시를 읽어 온다.</summary>
        /// <remarks>
        /// 진행 상태는 Bootstrap 단계에서 이미 복원되어 있으므로 여기서는 읽기만 한다.
        /// 카탈로그의 게임플레이 씬만 고르고 번호 순으로 세운다. 클리어 표시는 스테이지마다
        /// <see cref="StageProgression.IsCleared"/>에 묻는다.
        /// </remarks>
        public void Load()
        {
            _stages.Clear();

            var scenes = _sceneCatalog.Scenes;
            if (scenes != null)
            {
                for (var index = 0; index < scenes.Count; index++)
                {
                    var scene = scenes[index];
                    if (scene == null || scene.Category != ProjectSceneCategory.Gameplay || scene.GameplayLevelId <= 0)
                    {
                        continue;
                    }

                    _stages.Add(new StageEntry(scene.GameplayLevelId, _progression.IsCleared(scene.GameplayLevelId)));
                }
            }

            _stages.Sort(CompareByStageId);
            OnPropertiesChanged(nameof(Stages), nameof(ClearedStageCount));
        }

        /// <summary>
        /// 새 게임을 시작해 첫 전투 씬으로 넘어간다. 이미 전환 중이면 요청을 무시한다.
        /// </summary>
        /// <remarks>
        /// 첫 스테이지가 어느 것인지는 흐름 서비스가 카탈로그의 기본 레벨로 정하므로,
        /// 목록에서 첫 스테이지를 고르는 것과 같은 곳에 닿는다.
        /// </remarks>
        /// <returns>전환이 끝나면 완료되는 작업이다.</returns>
        public UniTask StartGameAsync()
        {
            return RunWhileBusyAsync(_gameFlowService.StartNewGameAsync);
        }

        /// <summary>
        /// 지정한 스테이지의 전투 씬으로 넘어간다. 이미 전환 중이면 요청을 무시한다.
        /// </summary>
        /// <param name="stageId">고른 스테이지 식별자이다.</param>
        /// <returns>전환이 끝나면 완료되는 작업이다.</returns>
        public UniTask StartStageAsync(int stageId)
        {
            return RunWhileBusyAsync(() => _gameFlowService.LoadGameplayLevelAsync(stageId));
        }

        /// <summary>
        /// 스테이지 버튼을 눌렀을 때 그 스테이지의 배치 패널을 연다. 씬은 아직 넘어가지 않는다.
        /// </summary>
        /// <param name="stageId">고른 스테이지 식별자이다.</param>
        public void OpenStagePlacement(int stageId)
        {
            if (IsBusy || IsPlacementPanelOpen)
            {
                return;
            }

            _pendingStageId = stageId;
            IsPlacementPanelOpen = true;
        }

        /// <summary>배치 패널을 닫는다. 아무것도 커밋하지 않으며, 게임플레이 씬으로도 넘어가지 않는다.</summary>
        public void CancelStagePlacement()
        {
            IsPlacementPanelOpen = false;
        }

        /// <summary>
        /// 배치 패널이 확정한 선택을 <see cref="PartySelectionService"/>에 커밋하고,
        /// 패널을 열 때 골랐던 스테이지로 넘어간다.
        /// </summary>
        /// <remarks>
        /// 커밋과 씬 전환 요청 사이에서 실패할 일이 없으므로(둘 다 예외를 던지지 않는 한
        /// 순서대로 끝난다), 커밋이 된 뒤에 전환이 취소되어 선택만 남는 경우는 생기지 않는다.
        /// </remarks>
        /// <param name="entries">배치 패널이 확정한 선택이다.</param>
        /// <returns>전환이 끝나면 완료되는 작업이다.</returns>
        public UniTask ConfirmStagePlacementAndStartAsync(IReadOnlyList<PartySelectionEntry> entries)
        {
            _partySelectionService.Commit(entries);
            var stageId = _pendingStageId;
            IsPlacementPanelOpen = false;
            return StartStageAsync(stageId);
        }

        /// <summary>
        /// 전환 하나를 <see cref="IsBusy"/>로 잠근 채 수행한다. 새 게임과 스테이지 선택이 같은 잠금을 지난다.
        /// </summary>
        /// <remarks>
        /// 전환이 실패하면 예외를 삼키지 않고 그대로 내보낸다. 여기서 기록하면 View가 그 사실을 알 길이 없고,
        /// 호출한 쪽이 기다리든 흘려보내든 실패는 한 곳(호출한 쪽)에서 드러나야 한다.
        /// 어느 쪽이든 <see cref="IsBusy"/>는 되돌리므로 버튼이 잠긴 채 남지 않는다.
        /// </remarks>
        /// <param name="transition">수행할 전환이다.</param>
        private async UniTask RunWhileBusyAsync(Func<UniTask> transition)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await transition();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static int CompareByStageId(StageEntry left, StageEntry right)
        {
            return left.StageId.CompareTo(right.StageId);
        }
    }
}
