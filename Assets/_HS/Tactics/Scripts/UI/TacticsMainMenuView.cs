using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HS.Framework.Foundation.MVVM;
using HS.Framework.ProjectManagement;
using HS.Framework.Runtime;
using HS.Tactics.Flow;
using HS.Tactics.Placement;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VContainer;

namespace HS.Tactics.UI
{
    /// <summary>
    /// 메인 메뉴 화면을 그리고 입력을 ViewModel에 넘기는 View이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 화면 요소와 그것을 채울 값만 다룬다. 씬 전환이나 진행도 같은 판단은 ViewModel이 하며,
    /// 이쪽은 무엇을 눌렀는지 전하고 바뀐 상태를 요소에 옮기기만 한다.
    /// </para>
    /// <para>
    /// <b>ViewModel은 Start에서 만든다.</b> 씬 주입은 <see cref="FrameworkInitializer"/>가 씬 로드 알림을 받아
    /// 수행하므로 Awake와 OnEnable보다 뒤, Start보다 앞에 도착한다. Start가 서비스를 손에 쥔 채로
    /// ViewModel을 만들 수 있는 첫 지점이다. 서비스가 없으면 씬을 Bootstrap 없이 연 것이므로
    /// 알리고 버튼을 잠근다.
    /// </para>
    /// <para>
    /// <b>스테이지 버튼은 인스펙터 배열 하나다.</b> 배열의 순서가 곧 목록 순서(번호 오름차순)이고,
    /// 라벨은 버튼 안의 TMP 텍스트에 번호와 클리어 표시를 쓴다. 스테이지가 몇 개 안 되므로
    /// 프리팹을 찍어 내지 않고 씬에 놓인 버튼을 그대로 쓴다. 목록보다 많은 버튼은 숨긴다.
    /// </para>
    /// <para>
    /// <b>버튼 리스너는 코드에서 단다.</b> 씬의 버튼에 인스펙터 연결이 없고, 리스너를 코드에 두면
    /// 무엇이 눌렸을 때 무엇이 일어나는지가 이 파일 하나에서 읽힌다.
    /// 직렬화 필드 검사에 <c>?.</c>를 쓰지 않는 것은 에디터에서 빠진 참조가 가짜 null이라
    /// <c>?.</c>가 걸러내지 못하기 때문이다.
    /// </para>
    /// <para>
    /// 씬은 스크립트 에셋의 GUID로 이 View를 참조한다.
    /// </para>
    /// </remarks>
    public sealed class TacticsMainMenuView : ViewBase<TacticsMainMenuViewModel>
    {
        /// <summary>클리어한 스테이지의 라벨에서 번호 뒤에 붙이는 표시이다.</summary>
        private const string ClearedMark = " CLEAR";

        [SerializeField] private Button startGameButton;

        [Tooltip("스테이지 버튼이다. 배열 순서가 곧 스테이지 목록 순서(번호 오름차순)이며, 목록보다 많은 버튼은 숨긴다. " +
                 "라벨은 각 버튼 안의 TMP 텍스트에 쓴다.")]
        [SerializeField] private Button[] stageButtons;

        [Tooltip("스테이지 버튼을 누르면 씬 전환 대신 이 자리에서 여는 영웅 배치 패널이다. " +
                 "\"게임 시작\"을 누르면 커밋하고 전환하며, 취소하면 아무 일도 하지 않는다.")]
        [SerializeField] private HeroPlacementPanelView heroPlacementPanel;

        private IGameFlowService _gameFlowService;
        private IProjectSceneCatalog _sceneCatalog;
        private StageProgressionService _progressionService;
        private PartySelectionService _partySelectionService;
        private TacticsMainMenuViewModel _ownedViewModel;
        private UnityAction[] _stageButtonHandlers;

        /// <summary>ViewModel을 만드는 데 필요한 서비스를 한 번에 주입받는다.</summary>
        /// <param name="gameFlowService">새 게임이나 지정한 스테이지로 씬을 넘기는 서비스이다.</param>
        /// <param name="sceneCatalog">스테이지 목록의 근거가 되는 프로젝트 씬 카탈로그이다.</param>
        /// <param name="progressionService">클리어 표시를 읽어 올 스테이지 진행 서비스이다.</param>
        /// <param name="partySelectionService">배치 패널이 확정한 선택을 게임플레이 씬 너머로 들고 갈 서비스이다.</param>
        [Inject]
        public void InjectServices(
            IGameFlowService gameFlowService,
            IProjectSceneCatalog sceneCatalog,
            StageProgressionService progressionService,
            PartySelectionService partySelectionService)
        {
            _gameFlowService = gameFlowService;
            _sceneCatalog = sceneCatalog;
            _progressionService = progressionService;
            _partySelectionService = partySelectionService;
        }

        private void Start()
        {
            if (_gameFlowService == null || _sceneCatalog == null || _progressionService == null
                || _partySelectionService == null)
            {
                Debug.LogError(
                    "[TacticsMainMenuView] 게임 흐름·씬 카탈로그·스테이지 진행·파티 선택 서비스 중 하나가 주입되지 않아 버튼을 잠근다. " +
                    "이 씬은 TacticsLifetimeScope가 있는 Bootstrap 씬을 거쳐 열어야 한다.",
                    this);
                SetStartButtonInteractable(false);
                SetStageButtonsInteractable(false);
                return;
            }

            _ownedViewModel = new TacticsMainMenuViewModel(
                _gameFlowService, _sceneCatalog, _progressionService.Progression, _partySelectionService);
            Initialize(_ownedViewModel);
            _ownedViewModel.Load();
            WarnIfStageButtonsAreFewerThanStages();

            if (heroPlacementPanel != null)
            {
                heroPlacementPanel.Close();
            }
        }

        private void OnEnable()
        {
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameButtonClicked);
            }

            if (heroPlacementPanel != null)
            {
                heroPlacementPanel.Confirmed += OnPlacementConfirmed;
                heroPlacementPanel.Cancelled += OnPlacementCancelled;
            }

            if (stageButtons == null)
            {
                return;
            }

            EnsureStageButtonHandlers();
            for (var index = 0; index < stageButtons.Length; index++)
            {
                if (stageButtons[index] != null)
                {
                    stageButtons[index].onClick.AddListener(_stageButtonHandlers[index]);
                }
            }
        }

        private void OnDisable()
        {
            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveListener(OnStartGameButtonClicked);
            }

            if (heroPlacementPanel != null)
            {
                heroPlacementPanel.Confirmed -= OnPlacementConfirmed;
                heroPlacementPanel.Cancelled -= OnPlacementCancelled;
            }

            if (stageButtons == null || _stageButtonHandlers == null)
            {
                return;
            }

            for (var index = 0; index < stageButtons.Length; index++)
            {
                if (stageButtons[index] != null)
                {
                    stageButtons[index].onClick.RemoveListener(_stageButtonHandlers[index]);
                }
            }
        }

        /// <inheritdoc />
        protected override void OnViewModelPropertyChanged(
            object sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
        {
            // TODO: 바뀐 속성 이름을 보고 해당 요소만 갱신한다. 지금은 통째로 다시 그린다.
            Refresh();
        }

        /// <summary>ViewModel의 지금 상태를 화면 요소에 옮긴다.</summary>
        public override void Refresh()
        {
            if (ViewModel == null)
            {
                return;
            }

            SetStartButtonInteractable(ViewModel.CanStartGame);
            RefreshStageButtons();
        }

        /// <summary>
        /// 시작 버튼이 눌리면 ViewModel에 새 게임을 요청하고 결과를 기다리지 않는다.
        /// </summary>
        /// <remarks>
        /// 전환이 끝나면 이 씬과 함께 View가 사라지므로 기다릴 주체가 없다. 실패는 UniTask가
        /// 예외로 기록하고, 그 사이 버튼은 ViewModel의 <see cref="TacticsMainMenuViewModel.CanStartGame"/>이
        /// 잠갔다가 되돌린다.
        /// </remarks>
        private void OnStartGameButtonClicked()
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.StartGameAsync().Forget();
        }

        /// <summary>
        /// 스테이지 버튼이 눌리면 씬을 바로 넘기지 않고 그 스테이지의 배치 패널을 연다.
        /// 실제 전환은 패널에서 "게임 시작"을 눌러야 <see cref="OnPlacementConfirmed"/>에서 일어난다.
        /// </summary>
        /// <param name="index">눌린 버튼의 배열 자리이며, 목록의 같은 자리를 가리킨다.</param>
        private void OnStageButtonClicked(int index)
        {
            if (ViewModel == null || index >= ViewModel.Stages.Count || heroPlacementPanel == null)
            {
                return;
            }

            ViewModel.OpenStagePlacement(ViewModel.Stages[index].StageId);
            heroPlacementPanel.Open();
        }

        /// <summary>
        /// 배치 패널에서 "게임 시작"이 눌리면 그 선택을 커밋하고 대기 중이던 스테이지로 넘어간다.
        /// 기다리지 않는 이유는 <see cref="OnStartGameButtonClicked"/>와 같다.
        /// </summary>
        /// <param name="entries">배치 패널이 확정한 선택이다.</param>
        private void OnPlacementConfirmed(IReadOnlyList<PartySelectionEntry> entries)
        {
            if (ViewModel == null)
            {
                return;
            }

            heroPlacementPanel.Close();
            ViewModel.ConfirmStagePlacementAndStartAsync(entries).Forget();
        }

        /// <summary>배치 패널에서 취소되면 그냥 닫고, 아무것도 커밋하지 않는다.</summary>
        private void OnPlacementCancelled()
        {
            heroPlacementPanel.Close();
            ViewModel?.CancelStagePlacement();
        }

        /// <summary>
        /// 목록의 각 스테이지를 같은 자리의 버튼에 옮긴다. 목록보다 많은 버튼은 숨기고,
        /// 라벨은 버튼 안의 TMP 텍스트에 번호와 클리어 표시를 쓴다.
        /// </summary>
        private void RefreshStageButtons()
        {
            if (stageButtons == null)
            {
                return;
            }

            var stages = ViewModel.Stages;
            for (var index = 0; index < stageButtons.Length; index++)
            {
                var button = stageButtons[index];
                if (button == null)
                {
                    continue;
                }

                var hasStage = index < stages.Count;
                button.gameObject.SetActive(hasStage);
                if (!hasStage)
                {
                    continue;
                }

                button.interactable = ViewModel.CanStartGame;
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    var entry = stages[index];
                    label.text = entry.IsCleared ? entry.StageId + ClearedMark : entry.StageId.ToString();
                }
            }
        }

        /// <summary>
        /// 버튼마다 자기 자리를 아는 리스너를 한 번만 만들어 둔다. 붙였다 뗄 때 같은 대리자를 써야 떼어진다.
        /// </summary>
        private void EnsureStageButtonHandlers()
        {
            if (_stageButtonHandlers != null && _stageButtonHandlers.Length == stageButtons.Length)
            {
                return;
            }

            _stageButtonHandlers = new UnityAction[stageButtons.Length];
            for (var index = 0; index < stageButtons.Length; index++)
            {
                var capturedIndex = index;
                _stageButtonHandlers[index] = () => OnStageButtonClicked(capturedIndex);
            }
        }

        /// <summary>목록보다 버튼이 적으면 뒤 스테이지를 고를 수 없으므로 한 번 알린다.</summary>
        private void WarnIfStageButtonsAreFewerThanStages()
        {
            var buttonCount = stageButtons != null ? stageButtons.Length : 0;
            var stageCount = ViewModel.Stages.Count;
            if (buttonCount < stageCount)
            {
                Debug.LogWarning(
                    $"[TacticsMainMenuView] 스테이지가 {stageCount}개인데 스테이지 버튼은 {buttonCount}개라 " +
                    $"{buttonCount + 1}번째부터는 고를 수 없다. 인스펙터의 Stage Buttons 배열에 버튼을 더해야 한다.",
                    this);
            }
        }

        private void SetStartButtonInteractable(bool isInteractable)
        {
            if (startGameButton != null)
            {
                startGameButton.interactable = isInteractable;
            }
        }

        private void SetStageButtonsInteractable(bool isInteractable)
        {
            if (stageButtons == null)
            {
                return;
            }

            for (var index = 0; index < stageButtons.Length; index++)
            {
                if (stageButtons[index] != null)
                {
                    stageButtons[index].interactable = isInteractable;
                }
            }
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            _ownedViewModel?.Dispose();
            _ownedViewModel = null;
            base.OnDestroy();
        }
    }
}
