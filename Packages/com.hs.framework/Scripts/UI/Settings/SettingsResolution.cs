using System;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 설정창에서 선택할 수 있는 화면 해상도 값을 표현한다.
    /// </summary>
    public readonly struct SettingsResolution : IEquatable<SettingsResolution>
    {

        /// <summary>
        /// 화면 해상도 값을 생성한다.
        /// </summary>
        /// <param name="width">해상도 너비이다.</param>
        /// <param name="height">해상도 높이이다.</param>
        public SettingsResolution(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
        }

        /// <summary>
        /// 해상도 너비를 가져온다.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// 해상도 높이를 가져온다.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// 사용자에게 표시할 해상도 이름을 가져온다.
        /// </summary>
        public string DisplayName => $"{Width} x {Height}";

        /// <summary>
        /// 다른 해상도 값과 같은지 확인한다.
        /// </summary>
        /// <param name="other">비교할 해상도 값이다.</param>
        /// <returns>너비와 높이가 모두 같으면 true를 반환한다.</returns>
        public bool Equals(SettingsResolution other)
        {
            return Width == other.Width && Height == other.Height;
        }

        /// <summary>
        /// 다른 객체와 같은 해상도 값인지 확인한다.
        /// </summary>
        /// <param name="obj">비교할 객체이다.</param>
        /// <returns>같은 해상도 값이면 true를 반환한다.</returns>
        public override bool Equals(object obj)
        {
            return obj is SettingsResolution other && Equals(other);
        }

        /// <summary>
        /// 해상도 값의 해시 코드를 반환한다.
        /// </summary>
        /// <returns>해상도 값의 해시 코드이다.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        /// <summary>
        /// 사용자에게 표시할 해상도 이름을 반환한다.
        /// </summary>
        /// <returns>해상도 표시 이름이다.</returns>
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
