using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HS.Framework.Scene
{
    /// <summary>로딩 씬의 표시 수명주기와 최소 노출 시간을 관리하고, 진행률을 이미지 채우기로 보여 준다.</summary>
    /// <remarks>
    /// 진행률 표시는 <see cref="ISceneTransitionService.State"/>를 그대로 구독한다. 별도 스트림을 만들지
    /// 않는 것은 그 스트림이 이미 <see cref="SceneLoadPhase.LoadingTarget"/>과
    /// <see cref="SceneLoadPhase.Initializing"/> 둘 다 0에서 1 사이의 <c>Value</c>로 담고 있기 때문이다.
    /// </remarks>
    public sealed class LoadingSceneController : MonoBehaviour
    {
        /// <summary>로딩 화면을 보여줄 최소 시간이다.</summary>
        [SerializeField] [Min(0f)] private float minimumDisplaySeconds = 0.25f;

        [Tooltip("진행률을 채워 보여줄 이미지(Image Type: Filled)이다. 비워 두면 진행률을 표시하지 않는다.")]
        [SerializeField] private Image progressFillImage;

        private float _shownAt;
        private ISceneTransitionService _sceneTransitionService;
        private IDisposable _stateSubscription;

        /// <summary>씬 전환 서비스를 주입받는다.</summary>
        /// <param name="sceneTransitionService">진행 상태를 읽어 올 씬 전환 서비스이다.</param>
        [Inject]
        public void InjectDependencies(ISceneTransitionService sceneTransitionService)
        {
            DetachStateSubscription();
            _sceneTransitionService = sceneTransitionService;
            AttachStateSubscription();
        }

        private void OnEnable()
        {
            _shownAt = Time.realtimeSinceStartup;
            AttachStateSubscription();
        }

        private void OnDisable()
        {
            DetachStateSubscription();
        }

        private void OnDestroy()
        {
            DetachStateSubscription();
            _sceneTransitionService = null;
        }

        /// <summary>서비스가 있고 보여 줄 이미지가 있을 때만 상태를 구독한다.</summary>
        private void AttachStateSubscription()
        {
            if (_sceneTransitionService == null || !isActiveAndEnabled || progressFillImage == null)
            {
                return;
            }

            _stateSubscription = _sceneTransitionService.State.Subscribe(OnStateChanged);
        }

        /// <summary>부착된 상태 구독을 해제한다.</summary>
        private void DetachStateSubscription()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
        }

        /// <summary>
        /// 목적지 씬을 불러오는 두 단계(<see cref="SceneLoadPhase.LoadingTarget"/>·<see cref="SceneLoadPhase.Initializing"/>)
        /// 동안만 진행률 이미지를 채운다. 다른 단계에서는 마지막 값을 그대로 둔다.
        /// </summary>
        /// <param name="state">새로 도착한 씬 전환 상태이다.</param>
        private void OnStateChanged(SceneTransitionState state)
        {
            if (state.Phase != SceneLoadPhase.LoadingTarget && state.Phase != SceneLoadPhase.Initializing)
            {
                return;
            }

            progressFillImage.fillAmount = state.Value;
        }

        /// <summary>현재 로딩 씬이 설정한 최소 노출 시간을 보장한다.</summary>
        public static async UniTask WaitForMinimumDisplayAsync()
        {
            var controller = FindFirstObjectByType<LoadingSceneController>();
            if (controller == null)
            {
                return;
            }

            var remainingSeconds = controller.minimumDisplaySeconds - (Time.realtimeSinceStartup - controller._shownAt);
            if (remainingSeconds > 0f)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(remainingSeconds),
                    DelayType.Realtime,
                    PlayerLoopTiming.Update);
            }
        }
    }
}
