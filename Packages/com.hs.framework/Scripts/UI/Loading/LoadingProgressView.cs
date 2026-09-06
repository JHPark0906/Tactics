using HS.Framework.Foundation.MVVM;
using HS.Framework.Scene;
using UnityEngine;
using VContainer;

namespace HS.Framework.UI.Loading
{
    /// <summary>
    /// 씬 전환 서비스의 최신 로딩 상태를 표시하는 기본 로딩 화면이다.
    /// </summary>
    public sealed class LoadingProgressView : ViewBase<LoadingProgressViewModel>
    {
        private LoadingProgressViewModel _ownedViewModel;

        /// <summary>
        /// 씬 전환 서비스를 주입받아 소유 ViewModel을 만들고 연결한다. 활성 상태면 곧바로 상태 구독을 시작한다.
        /// 이미 ViewModel을 만들었으면 다시 만들지 않는다.
        /// </summary>
        /// <param name="sceneTransitionService">로딩 상태를 읽어 올 씬 전환 서비스이다.</param>
        [Inject]
        public void InjectSceneTransitionService(ISceneTransitionService sceneTransitionService)
        {
            if (_ownedViewModel != null)
            {
                return;
            }

            _ownedViewModel = new LoadingProgressViewModel(sceneTransitionService);
            Initialize(_ownedViewModel);
            if (isActiveAndEnabled)
            {
                _ownedViewModel.Start();
            }
        }

        private void OnEnable()
        {
            _ownedViewModel?.Start();
        }

        private void OnDisable()
        {
            _ownedViewModel?.Stop();
        }

        private void OnGUI()
        {
            if (ViewModel == null)
            {
                return;
            }

            const float width = 360f;
            var rect = new Rect((Screen.width - width) * .5f, Screen.height * .72f, width, 24f);
            GUI.Box(rect, string.Empty);
            GUI.Box(new Rect(rect.x + 3f, rect.y + 3f, (width - 6f) * ViewModel.Progress, 18f), string.Empty);
            var statusText = ViewModel.State.Phase == SceneLoadPhase.Recovering
                ? "Returning to Main Menu"
                : $"Loading {ViewModel.Progress * 100f:0}%";
            GUI.Label(new Rect(rect.x, rect.y - 28f, width, 24f), statusText);
        }

        /// <summary>
        /// OnGUI가 현재 ViewModel 상태를 직접 읽으므로 별도 표시 캐시를 두지 않는다.
        /// </summary>
        public override void Refresh()
        {
        }

        /// <summary>
        /// View와 소유 ViewModel을 함께 해제한다.
        /// </summary>
        protected override void OnDestroy()
        {
            _ownedViewModel?.Dispose();
            _ownedViewModel = null;
            base.OnDestroy();
        }
    }
}
