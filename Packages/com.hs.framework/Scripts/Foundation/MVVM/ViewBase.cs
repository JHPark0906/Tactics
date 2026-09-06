using System.ComponentModel;
using UnityEngine;

namespace HS.Framework.Foundation.MVVM
{
    /// <summary>
    /// ViewModel의 속성 변경을 수신하고 연결 수명 주기를 관리하는 View 기반 클래스이다.
    /// </summary>
    /// <typeparam name="TViewModel">연결할 ViewModel 형식이다.</typeparam>
    public abstract class ViewBase<TViewModel> : MonoBehaviour
        where TViewModel : class, INotifyPropertyChanged
    {
        /// <summary>
        /// 현재 연결된 ViewModel이다.
        /// </summary>
        protected TViewModel ViewModel { get; private set; }

        /// <summary>
        /// ViewModel을 연결하고 현재 상태를 표시한다.
        /// </summary>
        /// <param name="viewModel">연결할 ViewModel이다.</param>
        public void Initialize(TViewModel viewModel)
        {
            if (ReferenceEquals(ViewModel, viewModel))
            {
                Refresh();
                return;
            }

            Unbind();
            ViewModel = viewModel;

            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
                OnViewModelBound();
            }

            Refresh();
        }

        /// <summary>
        /// ViewModel 연결을 해제한다.
        /// </summary>
        protected void Unbind()
        {
            if (ViewModel == null)
            {
                return;
            }

            OnViewModelUnbinding();
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel = null;
        }

        /// <summary>
        /// ViewModel이 연결된 직후 추가 바인딩을 구성한다.
        /// </summary>
        protected virtual void OnViewModelBound()
        {
        }

        /// <summary>
        /// ViewModel 연결을 해제하기 전 추가 바인딩을 정리한다.
        /// </summary>
        protected virtual void OnViewModelUnbinding()
        {
        }

        /// <summary>
        /// ViewModel 속성 변경 시 표시를 갱신한다.
        /// </summary>
        /// <param name="sender">변경된 ViewModel이다.</param>
        /// <param name="eventArgs">속성 변경 정보이다.</param>
        protected virtual void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            Refresh();
        }

        /// <summary>
        /// ViewModel 상태를 UI에 반영한다.
        /// </summary>
        public abstract void Refresh();

        /// <summary>
        /// 오브젝트 제거 시 ViewModel 연결을 해제한다.
        /// </summary>
        protected virtual void OnDestroy()
        {
            Unbind();
        }
    }
}
