using System.Runtime.CompilerServices;
using HS.Framework.Foundation.MVVM;

namespace HS.Framework.Foundation.MVVM
{
    /// <summary>
    /// 편집 중인 값의 변경 여부를 알리는 ViewModel 기반 클래스이다.
    /// </summary>
    public abstract class ChangeTrackingViewModelBase : ViewModelBase, IChangeTrackingViewModel
    {
        /// <summary>
        /// 적용된 값과 편집 중인 값이 다른지 여부를 가져온다.
        /// </summary>
        public abstract bool HasChanges { get; }

        /// <summary>
        /// 변경 여부 속성 값이 변경되었음을 알린다.
        /// </summary>
        protected void OnHasChangesChanged()
        {
            OnPropertyChanged(nameof(HasChanges));
        }

        /// <summary>
        /// 필드 값을 변경하고 변경된 경우 해당 속성과 변경 여부 알림을 함께 발생시킨다.
        /// </summary>
        /// <param name="field">변경할 필드이다.</param>
        /// <param name="value">새 값이다.</param>
        /// <param name="propertyName">변경된 속성 이름이다.</param>
        /// <typeparam name="T">필드 값의 형식이다.</typeparam>
        /// <returns>값이 실제로 변경되었으면 true를 반환한다.</returns>
        protected bool SetTrackedValue<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!SetProperty(ref field, value, propertyName))
            {
                return false;
            }

            OnHasChangesChanged();
            return true;
        }
    }
}
