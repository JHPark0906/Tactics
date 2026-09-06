using System;
using HS.Framework.Foundation.Input;
using HS.Framework.Scene;
using MessagePipe;
using VContainer.Unity;

namespace HS.Framework.Runtime
{
    /// <summary>
    /// 씬 로딩 메시지에 맞춰 입력 차단 토큰을 획득·해제해 전환 중 클라이언트 입력을 차단한다.
    /// 스냅샷 복원이 아니라 차단 토큰을 사용하므로 다른 차단 주체(일시정지 서비스 등)와
    /// 상태가 겹쳐도 안전하게 합성된다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>차단 범위는 <see cref="InputBlockScope.All"/>이다.</b> 전환이 진행되는 동안 화면에 남아 있는 UI는
    /// 곧 사라질 이전 씬의 것이라 눌러도 유효한 결과가 나오지 않으며, 사라지는 창을 조작하면 오히려
    /// 상태가 어긋난다. 되돌릴 조작 자체가 필요 없는 구간이므로 UI까지 함께 막는다.
    /// 전환은 완료·실패 메시지로 반드시 끝나므로 차단이 무기한 남지 않는다.
    /// </para>
    /// <para>
    /// <b>컨테이너의 엔트리포인트다.</b> 이 게이트를 해석하는 소비자는 없다. <see cref="FrameworkLifetimeScope"/>가
    /// 엔트리포인트로 등록하므로 컨테이너가 만들고 <see cref="Initialize"/>를 불러 구독을 걸며,
    /// 그 단계는 <see cref="FrameworkInitializer"/>가 첫 씬 전환을 시작하기 전이다.
    /// </para>
    /// </remarks>
    public sealed class SceneInputGate : IInitializable, IDisposable
    {
        private readonly ISubscriber<SceneLoadStartedEvent> _startedSubscriber;
        private readonly ISubscriber<SceneLoadCompletedEvent> _completedSubscriber;
        private readonly ISubscriber<SceneLoadFailedEvent> _failedSubscriber;
        private readonly IInputStateController _inputStateController;
        private IDisposable _completedSubscription;
        private IDisposable _failedSubscription;
        private IDisposable _startedSubscription;
        private IDisposable _inputBlockToken;

        /// <summary>MessagePipe subscriber를 사용하는 씬 입력 게이트를 생성한다.</summary>
        public SceneInputGate(
            ISubscriber<SceneLoadStartedEvent> startedSubscriber,
            ISubscriber<SceneLoadCompletedEvent> completedSubscriber,
            ISubscriber<SceneLoadFailedEvent> failedSubscriber,
            IInputStateController inputStateController)
        {
            _startedSubscriber = startedSubscriber ?? throw new ArgumentNullException(nameof(startedSubscriber));
            _completedSubscriber = completedSubscriber ?? throw new ArgumentNullException(nameof(completedSubscriber));
            _failedSubscriber = failedSubscriber ?? throw new ArgumentNullException(nameof(failedSubscriber));
            _inputStateController = inputStateController ?? throw new ArgumentNullException(nameof(inputStateController));
        }

        /// <summary>
        /// 씬 로딩 메시지 구독을 등록한다. 컨테이너가 엔트리포인트 초기화 단계에서 부르며,
        /// 여러 번 불려도 구독이 쌓이지 않는다.
        /// </summary>
        public void Initialize()
        {
            if (_startedSubscription != null)
            {
                return;
            }

            _startedSubscription = _startedSubscriber.Subscribe(OnSceneLoadStarted);
            _completedSubscription = _completedSubscriber.Subscribe(OnSceneLoadFinished);
            _failedSubscription = _failedSubscriber.Subscribe(OnSceneLoadFinished);
        }

        /// <summary>등록한 씬 로딩 이벤트 구독을 해제하고 보유 중인 입력 차단 토큰을 해제한다.</summary>
        public void Dispose()
        {
            _inputBlockToken?.Dispose();
            _inputBlockToken = null;
            _startedSubscription?.Dispose();
            _completedSubscription?.Dispose();
            _failedSubscription?.Dispose();
            _startedSubscription = null;
            _completedSubscription = null;
            _failedSubscription = null;
        }

        private void OnSceneLoadStarted(SceneLoadStartedEvent message)
        {
            if (_inputBlockToken != null)
            {
                return;
            }

            _inputBlockToken = _inputStateController.AcquireInputBlock(InputBlockScope.All);
        }

        private void OnSceneLoadFinished<T>(T message)
        {
            _inputBlockToken?.Dispose();
            _inputBlockToken = null;
        }
    }
}
