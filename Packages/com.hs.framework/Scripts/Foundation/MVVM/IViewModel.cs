using System;
using System.ComponentModel;

namespace HS.Framework.Foundation.MVVM
{
    /// <summary>
    /// View와 연결 가능한 ViewModel의 공통 수명 주기 계약이다.
    /// </summary>
    public interface IViewModel : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// ViewModel이 해제되었는지 여부를 가져온다.
        /// </summary>
        bool IsDisposed { get; }
    }
}
