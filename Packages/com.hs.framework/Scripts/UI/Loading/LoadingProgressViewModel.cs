using System;
using HS.Framework.Foundation.MVVM;
using HS.Framework.Scene;
using R3;

namespace HS.Framework.UI.Loading
{
    /// <summary>
    /// 로딩 진행 이벤트를 화면에 표시할 상태로 변환하는 ViewModel이다.
    /// </summary>
    public sealed class LoadingProgressViewModel : ViewModelBase
    {
        private readonly ISceneTransitionService _sceneTransitionService;
        private SceneTransitionState _state;
        private IDisposable _stateSubscription;

        /// <summary>
        /// 로딩 진행 상태를 가져온다.
        /// </summary>
        public SceneTransitionState State => _state;

        /// <summary>0부터 1 사이의 현재 로딩 진행률이다.</summary>
        public float Progress => _state.Value;

        /// <summary>
        /// 지정한 씬 전환 서비스를 사용하는 ViewModel을 생성한다.
        /// </summary>
        /// <param name="sceneTransitionService">현재 상태를 제공할 씬 전환 서비스이다.</param>
        public LoadingProgressViewModel(ISceneTransitionService sceneTransitionService)
        {
            _sceneTransitionService = sceneTransitionService ??
                                      throw new ArgumentNullException(nameof(sceneTransitionService));
        }

        /// <summary>
        /// 로딩 진행 이벤트 구독을 시작한다.
        /// </summary>
        public void Start()
        {
            _stateSubscription ??= _sceneTransitionService.State.Subscribe(OnStateChanged);
        }

        /// <summary>
        /// 로딩 진행 이벤트 구독을 중지한다.
        /// </summary>
        public void Stop()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
        }

        /// <summary>
        /// ViewModel 폐기 시 이벤트 구독을 해제한다.
        /// </summary>
        protected override void OnDispose()
        {
            Stop();
            base.OnDispose();
        }

        private void OnStateChanged(SceneTransitionState state)
        {
            if (SetProperty(ref _state, state, nameof(State)))
            {
                OnPropertyChanged(nameof(Progress));
            }
        }
    }
}
