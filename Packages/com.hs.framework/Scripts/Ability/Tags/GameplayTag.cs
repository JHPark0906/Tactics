using System;
using UnityEngine;

namespace HS.Framework.Ability.Tags
{
    /// <summary>
    /// 점으로 구분된 계층 이름을 가진 게임플레이 태그이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>태그는 데이터이다.</b> 프레임워크는 어떤 태그가 존재하는지 알지 않는다.
    /// 어떤 이름을 쓸지는 게임이 정하며, 프레임워크는 이름을 담고 비교하는 규칙만 제공한다.
    /// 게임이 태그 이름을 한곳에 모아 두는 방법은 <see cref="GameplayTagCatalog"/>를 참고한다.
    /// </para>
    /// <para>
    /// <b>계층 일치는 한 방향이다.</b> 자식은 조상에 일치하지만 그 역은 성립하지 않는다.
    /// 예를 들어 <c>A.B.C</c>는 <c>A.B</c>와 <c>A</c>에 일치하고, <c>A</c>는 <c>A.B</c>에 일치하지 않는다.
    /// 덕분에 "이 대상에게 <c>Cooldown</c> 계열 태그가 하나라도 있는가" 같은 질문을 상위 이름 하나로 물을 수 있다.
    /// </para>
    /// <para>
    /// <b>잘못된 형식은 고치지 않고 거부한다.</b> 빈 이름, 점으로 시작하거나 끝나는 이름, 연속된 점,
    /// 공백이 섞인 이름은 모두 유효하지 않다. 자동으로 다듬으면 <c>State..Slowed</c>와 <c>State.Slowed</c>가
    /// 같은 태그가 되어 오타가 조용히 통과하는데, 이런 시스템에서 가장 흔한 사고가 바로 그 조용한 실패이기 때문이다.
    /// 데이터에서 읽을 때는 <see cref="TryParse"/>로 걸러 내고, 코드 상수처럼 반드시 맞아야 하는 자리에서는
    /// <see cref="Parse"/>로 즉시 예외를 받아 문제를 드러낸다.
    /// </para>
    /// <para>
    /// <b>비교는 대소문자를 구분하지 않는다.</b> 표시에는 작성한 그대로의 문자열을 쓰되 비교와 해시는
    /// 대소문자를 무시하므로, 같은 태그를 다르게 적은 두 문자열이 서로 다른 태그가 되는 일이 없다.
    /// 값이 같으면 해시도 같으므로 사전 키로 그대로 쓸 수 있다.
    /// </para>
    /// <para>
    /// 태그는 이름 문자열을 보관하며, 이름과 계층 경계를 직접 비교한다.
    /// </para>
    /// </remarks>
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        /// <summary>계층을 구분하는 문자이다.</summary>
        public const char SeparatorChar = '.';

        /// <summary>비교와 해시에 사용하는 규칙이며 대소문자를 구분하지 않는다.</summary>
        private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

        [Tooltip("점으로 구분된 태그 이름이다. 예: State.Slowed")]
        [SerializeField]
        private string name;

        /// <summary>
        /// 이미 검증된 이름으로 태그를 만든다.
        /// </summary>
        /// <param name="validatedName">검증을 마친 태그 이름이다.</param>
        private GameplayTag(string validatedName)
        {
            name = validatedName;
        }

        /// <summary>어떤 태그도 가리키지 않는 값이다.</summary>
        public static GameplayTag None => default;

        /// <summary>점으로 구분된 태그 이름이며, 유효하지 않은 태그에서는 빈 문자열이다.</summary>
        public readonly string Name => name ?? string.Empty;

        /// <summary>실제 태그를 가리키는 유효한 값인지 여부이다.</summary>
        public readonly bool IsValid => !string.IsNullOrEmpty(name);

        /// <summary>계층의 깊이이며, 최상위 태그는 1이고 유효하지 않은 태그는 0이다.</summary>
        public readonly int Depth
        {
            get
            {
                if (!IsValid)
                {
                    return 0;
                }

                var depth = 1;
                for (var index = 0; index < name.Length; index++)
                {
                    if (name[index] == SeparatorChar)
                    {
                        depth++;
                    }
                }

                return depth;
            }
        }

        /// <summary>
        /// 태그 이름을 해석한다. 형식이 잘못되었으면 만들지 않고 실패로 답한다.
        /// 데이터나 사용자 입력에서 읽은 이름에 사용한다.
        /// </summary>
        /// <param name="value">해석할 태그 이름이다.</param>
        /// <param name="tag">해석한 태그이며 실패하면 <see cref="None"/>이다.</param>
        /// <returns>유효한 이름이어서 태그를 만들었으면 true이다.</returns>
        public static bool TryParse(string value, out GameplayTag tag)
        {
            tag = None;
            if (value == null)
            {
                return false;
            }

            var trimmedValue = value.Trim();
            if (!IsValidName(trimmedValue))
            {
                return false;
            }

            tag = new GameplayTag(trimmedValue);
            return true;
        }

        /// <summary>
        /// 태그 이름을 해석하며, 형식이 잘못되었으면 예외를 던진다.
        /// 반드시 맞아야 하는 코드 상수처럼 오타를 즉시 드러내야 하는 자리에 사용한다.
        /// </summary>
        /// <param name="value">해석할 태그 이름이다.</param>
        /// <returns>해석한 태그이다.</returns>
        /// <exception cref="ArgumentException">태그 이름의 형식이 잘못되었으면 발생한다.</exception>
        public static GameplayTag Parse(string value)
        {
            if (!TryParse(value, out var tag))
            {
                throw new ArgumentException(
                    $"'{value}'는 유효한 게임플레이 태그 이름이 아니다. " +
                    "이름은 비어 있을 수 없고 점으로 시작하거나 끝날 수 없으며, 연속된 점이나 공백을 담을 수 없다.",
                    nameof(value));
            }

            return tag;
        }

        /// <summary>
        /// 태그 이름이 유효한 형식인지 검사한다.
        /// </summary>
        /// <param name="value">검사할 태그 이름이며 앞뒤 공백이 없어야 한다.</param>
        /// <returns>유효한 형식이면 true이다.</returns>
        public static bool IsValidName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var segmentLength = 0;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character == SeparatorChar)
                {
                    // 구분자가 처음에 오거나 연달아 오면 빈 단계가 생긴다.
                    if (segmentLength == 0)
                    {
                        return false;
                    }

                    segmentLength = 0;
                    continue;
                }

                if (char.IsWhiteSpace(character))
                {
                    return false;
                }

                segmentLength++;
            }

            // 마지막 단계가 비어 있으면 이름이 구분자로 끝난 것이다.
            return segmentLength > 0;
        }

        /// <summary>
        /// 이 태그가 지정한 태그이거나 그 하위 태그인지 확인한다.
        /// 계층 일치는 한 방향이므로 상위 태그는 하위 태그에 일치하지 않는다.
        /// </summary>
        /// <param name="other">비교 기준이 되는 상위 태그이다.</param>
        /// <returns>이 태그가 기준 태그에 일치하면 true이다.</returns>
        public readonly bool Matches(GameplayTag other)
        {
            if (!IsValid || !other.IsValid)
            {
                return false;
            }

            var selfName = Name;
            var otherName = other.Name;
            if (selfName.Length < otherName.Length ||
                !selfName.StartsWith(otherName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 이름이 접두사로 겹치기만 해서는 안 되고 단계 경계에서 끊겨야 한다.
            // 그래야 A.Bomb이 A.B의 하위로 잘못 판정되지 않는다.
            return selfName.Length == otherName.Length || selfName[otherName.Length] == SeparatorChar;
        }

        /// <summary>
        /// 이 태그가 지정한 태그와 정확히 같은지 확인한다. 계층은 고려하지 않는다.
        /// </summary>
        /// <param name="other">비교할 태그이다.</param>
        /// <returns>같은 태그이면 true이다.</returns>
        public readonly bool MatchesExact(GameplayTag other)
        {
            return Equals(other);
        }

        /// <summary>
        /// 한 단계 위의 상위 태그를 구한다.
        /// </summary>
        /// <param name="parent">구한 상위 태그이며 없으면 <see cref="None"/>이다.</param>
        /// <returns>상위 태그가 있으면 true이며, 최상위 태그이거나 유효하지 않으면 false이다.</returns>
        public readonly bool TryGetParent(out GameplayTag parent)
        {
            parent = None;
            if (!IsValid)
            {
                return false;
            }

            var separatorIndex = name.LastIndexOf(SeparatorChar);
            if (separatorIndex <= 0)
            {
                return false;
            }

            parent = new GameplayTag(name.Substring(0, separatorIndex));
            return true;
        }

        /// <inheritdoc />
        public readonly bool Equals(GameplayTag other)
        {
            return NameComparer.Equals(Name, other.Name);
        }

        /// <inheritdoc />
        public readonly override bool Equals(object obj)
        {
            return obj is GameplayTag other && Equals(other);
        }

        /// <inheritdoc />
        public readonly override int GetHashCode()
        {
            return NameComparer.GetHashCode(Name);
        }

        /// <inheritdoc />
        public readonly override string ToString()
        {
            return IsValid ? Name : "GameplayTag(None)";
        }

        /// <summary>두 태그가 같은지 비교한다.</summary>
        public static bool operator ==(GameplayTag left, GameplayTag right) => left.Equals(right);

        /// <summary>두 태그가 다른지 비교한다.</summary>
        public static bool operator !=(GameplayTag left, GameplayTag right) => !left.Equals(right);
    }
}
