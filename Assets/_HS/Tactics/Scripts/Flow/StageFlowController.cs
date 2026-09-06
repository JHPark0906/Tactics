using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HS.Framework.Persistence;
using HS.Framework.ProjectManagement;
using HS.Framework.Runtime;
using HS.Framework.UI.Windows;
using HS.Tactics.Placement;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Flow
{
    /// <summary>
    /// 전투 결과를 받아 보상과 기록을 마친 뒤 결과 창을 띄우고, 플레이어가 확인하면 스테이지 선택으로 돌아간다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이기든 지든 전투가 끝나면 스테이지 선택으로 돌아간다.</b> 스테이지 선택 화면은 메인 메뉴 안의
    /// 목록이므로 돌아가는 곳은 메인 메뉴다. 패배한 전투를 배치도 바꾸지 않고 다시 하는 것은 뜻이 없고,
    /// 전투가 끝난 뒤의 선택은 플레이어에게 맡기므로, 결과에 따라 갈리는 길을 두지 않는다.
    /// 승패로 달라지는 것은 경험치 하나뿐이다. 클리어하면 파티가 나눠 받고, 지면 받지 않는다.
    /// </para>
    /// <para>
    /// <b>떠나기 전에 보상과 기록을 끝낸다.</b> 경험치를 나누고, 클리어 기록을 올리고, 저장한 뒤에야
    /// 씬을 떠난다. 씬이 바뀌면 이 컴포넌트는 사라지므로 그 뒤에는 할 기회가 없다.
    /// </para>
    /// <para>
    /// 씬 이동은 프레임워크의 <see cref="IGameFlowService"/>를 그대로 쓰고, 레벨 ID 라우팅은
    /// <see cref="IProjectSceneCatalog"/>가 담당하므로 이 컴포넌트는 씬 경로를 직접 해석하지 않는다.
    /// </para>
    /// <para>
    /// 경험치를 나눠 줄 명단은 <b>전투가 시작되는 순간</b> 잡아 둔다. 결과가 날 때 세면 죽은 유닛이
    /// 빠지고, 그러면 일부러 죽게 두어 몫을 키우는 길이 생긴다.
    /// </para>
    /// <para>
    /// 진행 상태는 씬 수명과 분리된 <see cref="StageProgressionService"/>가 소유한다.
    /// 그 서비스가 등록되지 않은 환경에서도 동작하도록, 주입되지 않으면 씬 카탈로그만으로 임시 진행 상태를 만든다.
    /// 이때 클리어 기록의 저장과 복원만 이루어지지 않고 스테이지 선택으로 돌아가는 것은 정상 동작한다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class StageFlowController : MonoBehaviour
    {
        [Header("Result UI")]
        [Tooltip("결과 창을 관리할 UI 창 관리자이다. 지정하지 않으면 결과 창을 창 스택에 얹지 않는다.")]
        [SerializeField]
        private UiWindowManager windowManager;

        [Tooltip("전투 결과를 표시할 창이다. 지정하지 않으면 결과 확정 즉시 스테이지 선택으로 돌아간다.")]
        [SerializeField]
        private BattleResultWindow resultWindow;

        [Header("Reward")]
        [Tooltip("배치 명단을 읽어 올 배치 컨트롤러이다. 비워 두면 같은 씬에서 찾는다.")]
        [SerializeField]
        private UnitPlacementController placementController;

        private IGameFlowService _gameFlowService;
        private IProjectSceneCatalog _sceneCatalog;
        private StageProgressionService _progressionService;
        private SaveOrchestrator _saveOrchestrator;
        private ISubscriber<BattleOutcomeDecidedEvent> _outcomeSubscriber;
        private IDisposable _outcomeSubscription;
        private StageRewardService _rewardService;
        private UnitPlacementController _placementSource;
        private readonly List<string> _deployedDefinitionIds = new();
        private bool _isTransitionRequested;
        private bool _hasHandledOutcome;
        private BattleOutcome _handledOutcome;

        /// <summary>
        /// 진행 상태 서비스이며, 주입되지 않았으면 씬 카탈로그로 만든 임시 서비스를 사용한다.
        /// 씬 카탈로그마저 없으면 null이다.
        /// </summary>
        public StageProgressionService ProgressionService => _progressionService ??= CreateFallbackProgressionService();

        /// <summary>결과 확정 후 스테이지 선택으로 돌아가기를 요청해 진행 중인지 여부이다. 전환이 실패하면 되돌아간다.</summary>
        public bool IsTransitionRequested => _isTransitionRequested;

        /// <summary>결과 정산은 끝났지만 저장에 실패해, 씬을 떠나기 전에 저장을 다시 해야 하는지 여부이다.</summary>
        public bool HasPendingSave { get; private set; }

        /// <summary>전투가 시작될 때 잡아 둔 배치 명단이며, 승리 시 경험치를 나눌 대상이다.</summary>
        public IReadOnlyList<string> DeployedDefinitionIds => _deployedDefinitionIds;

        /// <summary>
        /// 결과 이벤트 구독자와 씬 이동·진행·저장 서비스를 한 번에 주입받고,
        /// 로드된 씬에서 현재 스테이지를 맞춘 뒤 활성 상태면 결과 이벤트를 구독한다.
        /// </summary>
        /// <param name="outcomeSubscriber">전투 결과 확정 이벤트의 구독자이다.</param>
        /// <param name="gameFlowService">씬 이동에 쓰는 프레임워크 게임 흐름 서비스이다.</param>
        /// <param name="sceneCatalog">스테이지 조회에 쓰는 프로젝트 씬 카탈로그이다.</param>
        /// <param name="progressionService">씬 수명과 분리된 스테이지 진행 서비스이며 null이면 임시 진행 상태를 쓴다.</param>
        /// <param name="saveOrchestrator">진행도를 기록할 저장 조정자이며 null이면 저장하지 않는다.</param>
        /// <param name="rewardService">승리 시 경험치를 나눠 줄 서비스이며 null이면 나눠 주지 않는다.</param>
        [Inject]
        public void InjectRuntimeDependencies(
            ISubscriber<BattleOutcomeDecidedEvent> outcomeSubscriber,
            IGameFlowService gameFlowService,
            IProjectSceneCatalog sceneCatalog,
            StageProgressionService progressionService,
            SaveOrchestrator saveOrchestrator,
            StageRewardService rewardService)
        {
            _outcomeSubscriber = outcomeSubscriber;
            _gameFlowService = gameFlowService;
            _sceneCatalog = sceneCatalog;
            _progressionService = progressionService;
            _saveOrchestrator = saveOrchestrator;
            _rewardService = rewardService;
            SyncCurrentStageFromLoadedScene();
            RefreshSubscription();
            AttachPlacementSource();
        }

        private void OnEnable()
        {
            RefreshSubscription();
            AttachPlacementSource();
        }

        private void OnDisable()
        {
            DetachSubscription();
            DetachPlacementSource();
        }

        private void OnDestroy()
        {
            DetachSubscription();
            DetachPlacementSource();
            _outcomeSubscriber = null;
            _gameFlowService = null;
        }

        /// <summary>
        /// 구독자가 준비되고 컴포넌트가 활성 상태일 때만 전투 결과 이벤트를 구독한다.
        /// 주입이 여러 번 도착해도 중복 구독이 쌓이지 않도록 기존 구독을 먼저 해제한다.
        /// </summary>
        private void RefreshSubscription()
        {
            DetachSubscription();
            if (!isActiveAndEnabled)
            {
                return;
            }

            _outcomeSubscription = _outcomeSubscriber?.Subscribe(OnBattleOutcomeDecided);
        }

        /// <summary>부착된 전투 결과 구독을 해제한다.</summary>
        private void DetachSubscription()
        {
            _outcomeSubscription?.Dispose();
            _outcomeSubscription = null;
        }

        /// <summary>
        /// 확정된 전투 결과로 보상과 기록을 마친 뒤 결과 창을 띄워 확인을 기다린다.
        /// 결과 창이 없으면 저장에 성공한 경우에만 곧바로 스테이지 선택으로 돌아간다.
        /// </summary>
        /// <remarks>
        /// 순서가 뜻을 가진다. 경험치는 지금 열려 있는 스테이지의 것이므로 진행 상태를 건드리기 전에 주고,
        /// 그 뒤 클리어 기록을 올려 저장한다. 떠나는 것은 그 셋이 모두 끝난 뒤다.
        /// </remarks>
        /// <param name="outcomeEvent">확정된 전투 결과 이벤트이다.</param>
        private void OnBattleOutcomeDecided(BattleOutcomeDecidedEvent outcomeEvent)
        {
            if (_hasHandledOutcome)
            {
                return;
            }

            var progressionService = ProgressionService;
            if (progressionService == null)
            {
                Debug.LogError($"[StageFlowController] {name}에 스테이지 진행 정보가 없어 결과를 처리하지 못했다.", this);
                return;
            }

            _hasHandledOutcome = true;
            _handledOutcome = outcomeEvent.Outcome;
            var progression = progressionService.Progression;
            AwardStageExperience(outcomeEvent.Outcome, progression.CurrentStageId);
            progression.ApplyOutcome(outcomeEvent.Outcome);
            var saved = TrySaveProgress();

            if (resultWindow == null)
            {
                if (saved)
                {
                    ReturnToStageSelect();
                }
                return;
            }

            ShowResult();
        }

        /// <summary>이미 정산한 결과를 표시한다. 저장·전환 재시도 안내를 갱신해도 보상은 다시 정산하지 않는다.</summary>
        private void ShowResult(bool isRetry = false)
        {
            if (resultWindow == null)
            {
                return;
            }

            if (windowManager != null)
            {
                windowManager.Register(resultWindow);
            }

            if (HasPendingSave)
            {
                resultWindow.ShowSaveFailure(_handledOutcome, ReturnToStageSelect);
            }
            else
            {
                resultWindow.Show(_handledOutcome, ReturnToStageSelect, isRetry);
            }
        }

        /// <summary>
        /// 승리했을 때 전투 시작 시점에 잡아 둔 명단으로 경험치를 나눠 준다.
        /// </summary>
        /// <remarks>
        /// 패배하면 아무것도 주지 않는다. 진 판에서도 자란다면 이기는 것과 지는 것의 값이 같아진다.
        /// </remarks>
        /// <param name="outcome">확정된 전투 결과이다.</param>
        /// <param name="clearedStageId">방금 치른 스테이지의 식별자이다.</param>
        private void AwardStageExperience(BattleOutcome outcome, int clearedStageId)
        {
            if (outcome != BattleOutcome.Victory || _rewardService == null)
            {
                return;
            }

            if (_deployedDefinitionIds.Count == 0)
            {
                Debug.LogWarning(
                    $"[StageFlowController] {name}이 배치 명단을 잡지 못해 {clearedStageId}번 스테이지의 경험치를 나누지 못했다. " +
                    "배치 컨트롤러가 같은 씬에 있는지 확인해야 한다.",
                    this);
                return;
            }

            _rewardService.Award(clearedStageId, _deployedDefinitionIds);
        }

        /// <summary>
        /// 배치 컨트롤러를 찾아 단계 변화를 구독한다. 지정하지 않았으면 같은 씬에서 찾는다.
        /// </summary>
        /// <remarks>
        /// 씬에서 찾아 주는 것은 스테이지 씬마다 참조를 손으로 이어야 하는 자리를 하나 줄이기 위해서이다.
        /// 이어 두면 그 참조를 쓰므로, 한 씬에 배치 컨트롤러가 둘일 때 어느 쪽인지 정할 수 있다.
        /// </remarks>
        private void AttachPlacementSource()
        {
            DetachPlacementSource();
            _placementSource = placementController != null
                ? placementController
                : FindFirstObjectByType<UnitPlacementController>(FindObjectsInactive.Include);
            if (_placementSource == null)
            {
                return;
            }

            _placementSource.PhaseChanged += OnPlacementPhaseChanged;
        }

        /// <summary>배치 단계 구독을 해제한다.</summary>
        private void DetachPlacementSource()
        {
            if (_placementSource == null)
            {
                return;
            }

            _placementSource.PhaseChanged -= OnPlacementPhaseChanged;
            _placementSource = null;
        }

        /// <summary>
        /// 전투가 시작되는 순간 배치 명단을 잡아 둔다.
        /// </summary>
        /// <remarks>
        /// <b>여기서 잡는 것이 요점이다.</b> 결과가 날 때 세면 죽은 유닛이 빠져 몫이 커지고,
        /// 그러면 일부러 죽게 두는 것이 이득이 된다. 배치 단계로 돌아가면 명단을 비워,
        /// 지난 판의 명단이 섞이지 않게 한다.
        /// </remarks>
        /// <param name="phase">바뀐 배치 단계이다.</param>
        private void OnPlacementPhaseChanged(PlacementPhase phase)
        {
            _deployedDefinitionIds.Clear();
            if (phase == PlacementPhase.Placing)
            {
                _hasHandledOutcome = false;
            }
            if (phase != PlacementPhase.Battle || _placementSource == null)
            {
                return;
            }

            var entries = _placementSource.Plan.Entries;
            for (var index = 0; index < entries.Count; index++)
            {
                var definition = entries[index].Definition;
                _deployedDefinitionIds.Add(definition != null ? definition.Id : string.Empty);
            }
        }

        /// <summary>
        /// 정산한 결과를 저장한다. 실패한 참여자는 dirty를 유지하므로 재시도해도 이미 기록한 참여자는 건너뛴다.
        /// 저장 실패는 결과 창에서 복구하며, 보상과 클리어 정산은 다시 실행하지 않는다.
        /// </summary>
        private bool TrySaveProgress()
        {
            try
            {
                _saveOrchestrator?.SaveAll(SaveKind.Automatic);
                HasPendingSave = false;
                return true;
            }
            catch (Exception exception)
            {
                HasPendingSave = true;
                Debug.LogException(exception, this);
                return false;
            }
        }

        /// <summary>
        /// 스테이지 선택 화면, 곧 메인 메뉴로 돌아간다.
        /// 결과 창의 콜백과 창 파괴가 겹쳐도 이동이 두 번 일어나지 않도록 한 번만 실행한다.
        /// </summary>
        private void ReturnToStageSelect()
        {
            if (_isTransitionRequested)
            {
                return;
            }

            // 버튼뿐 아니라 ESC와 창 닫힘도 이 경로를 지나므로 저장 실패를 우회해 떠날 수 없다.
            if (HasPendingSave && !TrySaveProgress())
            {
                ShowResult();
                return;
            }

            if (_gameFlowService == null)
            {
                Debug.LogError($"[StageFlowController] {name}에 게임 흐름 서비스가 없어 스테이지 선택으로 돌아가지 못했다.", this);
                return;
            }

            _isTransitionRequested = true;
            RunTransitionAsync(_gameFlowService.ReturnToMainMenuAsync).Forget();
        }

        /// <summary>
        /// 전환 하나를 요청 표시로 잠근 채 수행한다. 성공하면 씬이 바뀌어 이 컴포넌트가 사라지므로 표시는 뜻을 잃고,
        /// 실패하면 표시를 되돌려 다음 전환이 막히지 않게 한다.
        /// </summary>
        /// <remarks>
        /// 흐름 서비스는 시작하기 전에 동기로 던질 수 있다(열 수 없는 씬 참조). 그 호출을 이 메서드
        /// 안에서 하므로 동기 예외도 작업의 실패가 되어 같은 길로 표시가 풀린다. 씬이 남아 있으면
        /// 정산을 반복하지 않고 결과 창을 다시 열어 플레이어가 재시도하게 한다. 실패는 UniTask가 예외로 기록한다.
        /// 메인 메뉴 ViewModel의 잠금과 같은 꼴이다.
        /// </remarks>
        /// <param name="transition">수행할 전환이다.</param>
        private async UniTask RunTransitionAsync(Func<UniTask> transition)
        {
            try
            {
                await transition();
            }
            catch
            {
                // 로드가 실패하기 전에 기존 씬이 언로드됐거나 안전 복귀가 성공했으면
                // 이 컴포넌트도 파괴되어 있으므로 사라진 씬의 창을 다시 열지 않는다.
                if (this != null && isActiveAndEnabled)
                {
                    ShowResult(isRetry: true);
                }

                throw;
            }
            finally
            {
                _isTransitionRequested = false;
            }
        }

        /// <summary>
        /// 지금 로드된 씬의 레벨 ID로 현재 스테이지를 맞춘다.
        /// 저장된 진행도와 무관하게 특정 스테이지 씬을 직접 열어도 보상과 클리어 기록이 그 스테이지에 붙게 한다.
        /// </summary>
        private void SyncCurrentStageFromLoadedScene()
        {
            if (_sceneCatalog == null || _progressionService == null)
            {
                return;
            }

            if (_sceneCatalog.TryGetGameplayLevelId(gameObject.scene.path, out var levelId))
            {
                _progressionService.Progression.SetCurrentStage(levelId);
            }
        }

        /// <summary>
        /// 진행 서비스가 주입되지 않은 환경에서 씬 카탈로그만으로 임시 진행 서비스를 만든다.
        /// 클리어 기록은 유지되지 않지만 현재 스테이지는 로드된 씬에서 되찾으므로 보상과 복귀는 동작한다.
        /// </summary>
        /// <returns>만든 임시 진행 서비스이며, 씬 카탈로그가 없으면 null이다.</returns>
        private StageProgressionService CreateFallbackProgressionService()
        {
            if (_sceneCatalog == null)
            {
                return null;
            }

            Debug.LogWarning(
                $"[StageFlowController] {name}에 스테이지 진행 서비스가 주입되지 않아 임시 진행 상태를 사용한다. " +
                "클리어 기록을 저장하려면 TacticsLifetimeScope가 구성된 Bootstrap 씬을 사용해야 한다.",
                this);

            _progressionService = new StageProgressionService(_sceneCatalog);
            SyncCurrentStageFromLoadedScene();
            return _progressionService;
        }
    }
}
