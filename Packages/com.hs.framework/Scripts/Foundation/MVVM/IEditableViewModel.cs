namespace HS.Framework.Foundation.MVVM
{
    /// <summary>
    /// 적용, 취소, 기본값 복원 흐름을 제공하는 편집 가능한 ViewModel 계약이다.
    /// </summary>
    public interface IEditableViewModel : IChangeTrackingViewModel
    {
        /// <summary>
        /// 편집 중인 값을 적용된 값으로 확정한다.
        /// </summary>
        void Apply();

        /// <summary>
        /// 편집 중인 값을 마지막 적용 값으로 되돌린다.
        /// </summary>
        void Cancel();

        /// <summary>
        /// 편집 중인 값을 기본값으로 되돌린다.
        /// </summary>
        void ResetToDefaults();
    }
}
