namespace HS.Framework.Foundation.MVVM
{
    /// <summary>
    /// 적용 전 편집 상태 변경 여부를 추적하는 ViewModel 계약이다.
    /// </summary>
    public interface IChangeTrackingViewModel : IViewModel
    {
        /// <summary>
        /// 적용된 값과 편집 중인 값이 다른지 여부를 가져온다.
        /// </summary>
        bool HasChanges { get; }
    }
}
