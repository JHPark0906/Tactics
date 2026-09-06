using System;
using HS.Framework.Foundation.Input;
using HS.Framework.Scene;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace HS.Framework.Runtime
{
    /// <summary>
    /// 시간 배율을 저장·복원하고 입력 차단 토큰을 획득·해제하며 클라이언트 일시정지를 제어한다.
    /// 씬 전환 시작 메시지를 구독해 전환 시작 시 일시정지 상태가 다음 씬으로 새어 나가지 않게 자동 해제한다.
    /// 입력은 스냅샷 복원이 아니라 차단 토큰으로 관리하므로 다른 차단 주체(씬 입력 게이트 등)와
    /// 상태가 겹쳐도 안전하게 합성된다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>차단 범위는 <see cref="InputBlockScope.Gameplay"/>이다.</b> 일시정지를 푸는 수단은 대개 일시정지 메뉴이므로
    /// UI 입력까지 막으면 플레이어가 스스로 일시정지를 해제할 수 없게 된다. 멈춰야 하는 것은 세계에 대한 개입이지
    /// 메뉴 조작이 아니다.
    /// </para>
    /// <para>
    /// <b>컨테이너의 엔트리포인트다.</b> <see cref="FrameworkLifetimeScope"/>가 이 서비스를 엔트리포인트로 등록하므로
    /// 아무도 <see cref="IPauseService"/>를 해석하지 않아도 컨테이너가 만들고 <see cref="Initialize"/>를 불러
    /// 구독을 건다. 그 단계는 <see cref="FrameworkInitializer"/>가 첫 씬 전환을 시작하기 전이므로
    /// 첫 전환 시작 메시지부터 놓치지 않는다.
    /// </para>
    /// </remarks>
    public sealed class PauseService : IPauseService, IInitializable, IDisposable
    {
        private readonly IInputStateController _inputStateController;
        private readonly IPublisher<PauseChangedEvent> _pausePublisher;
        private readonly ISubscriber<SceneLoadStartedEvent> _sceneLoadStartedSubscriber;
        private IDisposable _sceneLoadStartedSubscription;
        private IDisposable _inputBlockToken;
        private float _savedTimeScale;

        /// <inheritdoc />
        public bool IsPaused { get; private set; }

        /// <summary>MessagePipe publisher와 subscriber를 사용하는 일시정지 서비스를 생성한다.</summary>
        public PauseService(
            IInputStateController inputStateController,
            IPublisher<PauseChangedEvent> publisher,
            ISubscriber<SceneLoadStartedEvent> subscriber)
        {
            _inputStateController = inputStateController ?? throw new ArgumentNullException(nameof(inputStateController));
            _pausePublisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _sceneLoadStartedSubscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
        }

        /// <summary>
        /// 씬 전환 시작 메시지 구독을 등록한다. 컨테이너가 엔트리포인트 초기화 단계에서 부르며,
        /// 여러 번 불려도 구독이 쌓이지 않는다.
        /// </summary>
        public void Initialize()
        {
            if (_sceneLoadStartedSubscription != null)
            {
                return;
            }

            _sceneLoadStartedSubscription = _sceneLoadStartedSubscriber.Subscribe(OnSceneLoadStarted);
        }

        /// <inheritdoc />
        public void Pause()
        {
            if (IsPaused)
            {
                return;
            }

            _savedTimeScale = Time.timeScale;
            IsPaused = true;
            Time.timeScale = 0f;
            _inputBlockToken = _inputStateController.AcquireInputBlock(InputBlockScope.Gameplay);
            _pausePublisher.Publish(new PauseChangedEvent(true));
        }

        /// <inheritdoc />
        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            RestorePausedState();
            _pausePublisher.Publish(new PauseChangedEvent(false));
        }

        /// <summary>
        /// 씬 전환 시작 구독을 해제하고, 일시정지 상태였다면 메시지 발행 없이 저장한 상태를 복원한다.
        /// </summary>
        public void Dispose()
        {
            if (IsPaused)
            {
                RestorePausedState();
            }

            _sceneLoadStartedSubscription?.Dispose();
            _sceneLoadStartedSubscription = null;
        }

        /// <summary>
        /// 저장한 시간 배율을 복원하고 입력 차단 토큰을 해제한다.
        /// </summary>
        private void RestorePausedState()
        {
            IsPaused = false;
            Time.timeScale = _savedTimeScale;
            _inputBlockToken?.Dispose();
            _inputBlockToken = null;
        }

        private void OnSceneLoadStarted(SceneLoadStartedEvent message)
        {
            Resume();
        }
    }
}
