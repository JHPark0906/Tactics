namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 드롭다운이나 목록에서 선택할 수 있는 설정 옵션을 표현한다.
    /// </summary>
    /// <typeparam name="T">옵션 값의 형식이다.</typeparam>
    public sealed class SettingsOptionViewModel<T>
    {

        /// <summary>
        /// 설정 옵션을 생성한다.
        /// </summary>
        /// <param name="id">옵션을 식별하는 ID이다.</param>
        /// <param name="displayName">사용자에게 표시할 이름이다.</param>
        /// <param name="value">실제 설정 값이다.</param>
        public SettingsOptionViewModel(string id, string displayName, T value)
        {
            Id = string.IsNullOrWhiteSpace(id) ? displayName : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            Value = value;
        }

        /// <summary>
        /// 옵션을 식별하는 ID를 가져온다.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 사용자에게 표시할 이름을 가져온다.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 실제 설정 값을 가져온다.
        /// </summary>
        public T Value { get; }
    }
}
