using HS.Framework.Ability.Tags;

namespace HS.Framework.Ability.Abilities
{
    /// <summary>
    /// 어빌리티 시스템에 보내는 게임플레이 이벤트이다. 태그로 종류를 나타내고 필요한 것을 실어 나른다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이벤트는 어빌리티를 바깥에서 시작하는 두 번째 길이다.</b> 첫 번째 길은 태그로 활성화를 요청하는 것인데,
    /// 그러려면 요청하는 쪽이 어떤 어빌리티가 반응해야 하는지 알아야 한다. 이벤트는 반대로 "이런 일이 일어났다"만
    /// 알리고, 무엇이 반응할지는 어빌리티 정의의 트리거가 정한다. 사망처럼 여러 어빌리티가 저마다 반응할 수 있는
    /// 일에 맞는 형태이다.
    /// </para>
    /// <para>
    /// 실어 나르는 것은 게임이 정한다. 프레임워크는 그 내용을 해석하지 않으며, 받은 어빌리티가
    /// <see cref="GameplayAbility.TriggeringEvent"/>에서 꺼내 쓴다.
    /// </para>
    /// </remarks>
    public readonly struct GameplayEventData
    {
        /// <summary>이벤트 종류를 나타내는 태그이다.</summary>
        public GameplayTag EventTag { get; }

        /// <summary>이벤트가 실어 나르는 것이며 없으면 null이다.</summary>
        public object Payload { get; }

        /// <summary>이벤트에 딸린 크기이며 뜻은 이벤트마다 다르다. 없으면 0이다.</summary>
        public float Magnitude { get; }

        /// <summary>게임플레이 이벤트를 생성한다.</summary>
        /// <param name="eventTag">이벤트 종류를 나타내는 태그이다.</param>
        /// <param name="payload">이벤트가 실어 나르는 것이며 없으면 null이다.</param>
        /// <param name="magnitude">이벤트에 딸린 크기이며 없으면 0이다.</param>
        public GameplayEventData(GameplayTag eventTag, object payload = null, float magnitude = 0f)
        {
            EventTag = eventTag;
            Payload = payload;
            Magnitude = magnitude;
        }

        /// <summary>이벤트가 아닌 것을 나타내는 빈 값이다.</summary>
        public static GameplayEventData None => default;

        /// <summary>실제 이벤트인지 여부이며, 태그가 유효하지 않으면 빈 값이다.</summary>
        public bool IsValid => EventTag.IsValid;

        /// <inheritdoc />
        public override string ToString()
        {
            return IsValid ? $"{EventTag} ({Payload ?? "없음"}, {Magnitude:0.##})" : "None";
        }
    }
}
