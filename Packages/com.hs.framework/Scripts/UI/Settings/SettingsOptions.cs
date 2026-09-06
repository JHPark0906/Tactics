using System.Collections.Generic;
using UnityEngine;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 설정 섹션 ViewModel이 공통으로 사용하는 옵션 목록 조회와 선택 인덱스 보정 헬퍼이다.
    /// </summary>
    public static class SettingsOptions
    {
        /// <summary>
        /// 옵션 목록에서 지정한 값과 일치하는 첫 인덱스를 찾는다.
        /// </summary>
        /// <typeparam name="T">옵션 값의 형식이다.</typeparam>
        /// <param name="options">검색할 옵션 목록이다.</param>
        /// <param name="value">찾을 값이다.</param>
        /// <param name="comparer">값 비교에 사용할 비교자이며, 없으면 형식의 기본 비교자를 사용한다.</param>
        /// <returns>찾은 인덱스이며, 목록이 없거나 일치하는 값이 없으면 -1을 반환한다.</returns>
        public static int IndexOfValue<T>(
            IReadOnlyList<SettingsOptionViewModel<T>> options,
            T value,
            IEqualityComparer<T> comparer = null)
        {
            if (options == null)
            {
                return -1;
            }

            var valueComparer = comparer ?? EqualityComparer<T>.Default;
            for (var index = 0; index < options.Count; index++)
            {
                if (valueComparer.Equals(options[index].Value, value))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// 옵션 목록에서 지정한 값과 일치하는 첫 인덱스를 찾고, 없으면 대체 인덱스를 반환한다.
        /// </summary>
        /// <typeparam name="T">옵션 값의 형식이다.</typeparam>
        /// <param name="options">검색할 옵션 목록이다.</param>
        /// <param name="value">찾을 값이다.</param>
        /// <param name="comparer">값 비교에 사용할 비교자이며, 없으면 형식의 기본 비교자를 사용한다.</param>
        /// <param name="fallbackIndex">일치하는 값이 없을 때 사용할 인덱스이다.</param>
        /// <returns>찾은 인덱스이며, 없으면 대체 인덱스를 반환한다.</returns>
        public static int IndexOfValueOrDefault<T>(
            IReadOnlyList<SettingsOptionViewModel<T>> options,
            T value,
            IEqualityComparer<T> comparer = null,
            int fallbackIndex = 0)
        {
            var index = IndexOfValue(options, value, comparer);
            return index >= 0 ? index : fallbackIndex;
        }

        /// <summary>
        /// 선택 인덱스를 옵션 개수에 맞는 유효 범위로 보정한다.
        /// </summary>
        /// <param name="index">요청된 인덱스이다.</param>
        /// <param name="count">옵션 개수이다.</param>
        /// <returns>0 이상이며 마지막 옵션을 넘지 않는 인덱스이고, 옵션이 없으면 0이다.</returns>
        public static int ClampSelectedIndex(int index, int count)
        {
            return Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
        }
    }
}
