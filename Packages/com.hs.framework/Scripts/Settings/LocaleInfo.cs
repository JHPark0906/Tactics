namespace HS.Framework.Settings
{
    /// <summary>
    /// Unity Localization 형식에 의존하지 않고 로케일을 표현하는 식별 코드와 표시 이름이다.
    /// </summary>
    public readonly struct LocaleInfo
    {
        /// <summary>
        /// 로케일 정보를 생성한다.
        /// </summary>
        /// <param name="code">로케일 식별 코드이다.</param>
        /// <param name="displayName">사용자에게 표시할 로케일 이름이다.</param>
        public LocaleInfo(string code, string displayName)
        {
            Code = code;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? code : displayName;
        }

        /// <summary>
        /// 로케일 식별 코드를 가져온다.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// 사용자에게 표시할 로케일 이름을 가져온다.
        /// </summary>
        public string DisplayName { get; }
    }
}
