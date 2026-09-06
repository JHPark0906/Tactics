using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HS.Framework.Foundation.MVVM;

namespace HS.Framework.Foundation.MVVM
{
    /// <summary>
    /// UI ViewModel에서 공통으로 사용하는 속성 변경 알림과 수명 주기 기반 클래스이다.
    /// </summary>
    public abstract class ViewModelBase : IViewModel
    {
        private bool _isDisposed;

        /// <summary>
        /// ViewModel 속성 값이 변경되었을 때 발생한다.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// ViewModel이 해제되었는지 여부를 가져온다.
        /// </summary>
        public bool IsDisposed => _isDisposed;

        /// <summary>
        /// 속성 값이 변경되었음을 알린다.
        /// </summary>
        /// <param name="propertyName">변경된 속성 이름이다.</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 여러 속성 값이 변경되었음을 알린다.
        /// </summary>
        /// <param name="propertyNames">변경된 속성 이름 목록이다.</param>
        protected void OnPropertiesChanged(params string[] propertyNames)
        {
            if (propertyNames == null)
            {
                return;
            }

            foreach (var propertyName in propertyNames)
            {
                OnPropertyChanged(propertyName);
            }
        }

        /// <summary>
        /// 필드 값을 변경하고 변경된 경우 속성 변경 알림을 발생시킨다.
        /// </summary>
        /// <param name="field">변경할 필드이다.</param>
        /// <param name="value">새 값이다.</param>
        /// <param name="propertyName">변경된 속성 이름이다.</param>
        /// <typeparam name="T">필드 값의 형식이다.</typeparam>
        /// <returns>값이 실제로 변경되었으면 true를 반환한다.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// ViewModel이 보유한 이벤트 구독과 리소스를 해제한다.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            OnDispose();
            PropertyChanged = null;
        }

        /// <summary>
        /// 파생 ViewModel이 보유한 리소스를 해제한다.
        /// </summary>
        protected virtual void OnDispose()
        {
        }
    }
}
