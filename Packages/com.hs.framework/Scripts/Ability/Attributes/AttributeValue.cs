using System;

namespace HS.Framework.Ability.Attributes
{
    /// <summary>
    /// 수정자가 기본값을 어떤 방식으로 바꾸는지 나타낸다.
    /// </summary>
    /// <remarks>
    /// <b>적용 순서는 더하기 → 곱하기 → 덮어쓰기이며 이 순서는 바꾸지 않는다.</b>
    /// 열거형 값의 크기가 곧 적용 순서이다. 순서가 달라지면 같은 수정자 조합에서도 결과가 달라지므로,
    /// 예컨대 "+10 후 ×2"와 "×2 후 +10"은 서로 다른 값을 낸다. 밸런스는 이 순서를 전제로 잡히기 때문에
    /// 순서를 조용히 바꾸면 기존 수치가 전부 어긋난다. 새 연산을 더할 일이 있으면
    /// 기존 값 사이에 끼워 넣지 말고 의미에 맞는 자리를 정한 뒤 이 주석도 함께 고쳐야 한다.
    /// </remarks>
    public enum AttributeModifierOperation
    {
        /// <summary>기본값에 더한다. 같은 연산의 수정자는 모두 합산한 뒤 한 번에 적용한다.</summary>
        Add = 0,

        /// <summary>더하기까지 끝난 값에 곱한다. 같은 연산의 수정자는 모두 곱해 한 번에 적용한다.</summary>
        Multiply = 1,

        /// <summary>
        /// 앞선 계산 결과를 무시하고 값을 지정한 값으로 대체한다.
        /// 덮어쓰기가 여러 개면 가장 나중에 추가한 것이 이긴다.
        /// </summary>
        Override = 2
    }

    /// <summary>
    /// 상한으로 삼은 어트리뷰트의 값이 바뀌었을 때 기본값을 어떻게 다룰지 정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 최대 체력 버프가 끝나는 순간처럼 상한이 줄어들면 기본값이 상한을 넘게 된다.
    /// 이때의 처리는 게임마다 기대가 달라 하나로 정할 수 없으므로 정의가 고르게 한다.
    /// </para>
    /// <para>
    /// 기본값이 <see cref="ClampBaseValue"/>인 이유는, 임시로 늘어난 상한만큼 채워 둔 몫은
    /// 상한이 사라질 때 함께 사라지는 것이 대부분의 게임이 기대하는 동작이기 때문이다.
    /// 또한 이 정책만이 상한 변화로 값이 <b>늘어나는</b> 부작용을 만들지 않는다.
    /// </para>
    /// </remarks>
    public enum AttributeCapPolicy
    {
        /// <summary>
        /// 상한이 줄어 기본값이 상한을 넘으면 기본값을 상한까지 잘라낸다. 넘친 몫은 되돌아오지 않는다.
        /// 상한이 늘어날 때는 아무것도 하지 않으므로, 최대치가 올라간다고 현재값이 저절로 차오르지 않는다.
        /// </summary>
        ClampBaseValue = 0,

        /// <summary>
        /// 상한이 바뀐 비율만큼 기본값을 함께 조정해 채워진 비율을 유지한다.
        /// 상한이 늘어나면 기본값도 같이 올라가므로, 최대치 증가가 곧 회복이 되기를 바라는 게임이 고른다.
        /// </summary>
        PreserveRatio = 1
    }

    /// <summary>
    /// 어트리뷰트의 현재값이 바뀐 사실을 알리는 알림이다.
    /// </summary>
    /// <remarks>
    /// 값 변화 통지는 이 프레임워크가 이미 쓰는 R3 스트림으로만 흐른다.
    /// 어트리뷰트마다 스트림을 따로 두지 않고 하나로 모아 흘리므로, 특정 어트리뷰트만 보려면
    /// 구독하는 쪽에서 <see cref="Definition"/>으로 걸러 낸다.
    /// </remarks>
    public readonly struct AttributeChangedEvent
    {
        /// <summary>값이 바뀐 어트리뷰트의 정의이다.</summary>
        public AttributeDefinition Definition { get; }

        /// <summary>바뀌기 전의 현재값이다.</summary>
        public float PreviousValue { get; }

        /// <summary>바뀐 뒤의 현재값이다.</summary>
        public float CurrentValue { get; }

        /// <summary>
        /// 이 변화의 원인이다. 기본값을 바꾼 호출이 넘긴 것이 그대로 오며,
        /// 수정자나 상한 변화처럼 원인을 밝히지 않는 변화에서는 <see cref="AttributeChangeContext.None"/>이다.
        /// </summary>
        public AttributeChangeContext Context { get; }

        /// <summary>원인을 밝히지 않은 어트리뷰트 변경 알림을 생성한다.</summary>
        /// <param name="definition">값이 바뀐 어트리뷰트의 정의이다.</param>
        /// <param name="previousValue">바뀌기 전의 현재값이다.</param>
        /// <param name="currentValue">바뀐 뒤의 현재값이다.</param>
        public AttributeChangedEvent(AttributeDefinition definition, float previousValue, float currentValue)
            : this(definition, previousValue, currentValue, AttributeChangeContext.None)
        {
        }

        /// <summary>원인이 밝혀진 어트리뷰트 변경 알림을 생성한다.</summary>
        /// <param name="definition">값이 바뀐 어트리뷰트의 정의이다.</param>
        /// <param name="previousValue">바뀌기 전의 현재값이다.</param>
        /// <param name="currentValue">바뀐 뒤의 현재값이다.</param>
        /// <param name="context">이 변화의 원인이다.</param>
        public AttributeChangedEvent(
            AttributeDefinition definition,
            float previousValue,
            float currentValue,
            AttributeChangeContext context)
        {
            Definition = definition;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            Context = context;
        }

        /// <summary>이번 변화의 증감폭이다.</summary>
        public float Delta => CurrentValue - PreviousValue;
    }

    /// <summary>
    /// 어트리뷰트 값을 다루는 공통 규칙을 모아 둔다.
    /// </summary>
    /// <remarks>
    /// 어트리뷰트는 곱하기 수정자를 받으므로 내부적으로 실수로 다루지만, 체력처럼 정수로 주고받는
    /// 소비자가 있다. 변환 규칙이 호출부마다 다르면 같은 값이 곳에 따라 90과 91로 갈리므로
    /// 정수 변환은 반드시 이 규칙 하나만 쓴다.
    /// </remarks>
    public static class AttributeValue
    {
        /// <summary>
        /// 어트리뷰트 값을 정수로 바꾼다. 버림이 아니라 반올림하며, 정확히 0.5인 값은 0에서 먼 쪽으로 보낸다.
        /// </summary>
        /// <remarks>
        /// 버림을 쓰지 않는 이유는 0.9처럼 거의 1에 가까운 값이 매번 사라져 작은 값이 조용히 누락되기 때문이다.
        /// 0.5를 0에서 먼 쪽으로 보내는 것은 양수와 음수를 대칭으로 다루기 위해서이며,
        /// 이렇게 해야 90.5는 91이 되고 -90.5는 -91이 되어 부호에 따라 규칙이 달라지지 않는다.
        /// </remarks>
        /// <param name="value">변환할 어트리뷰트 값이다.</param>
        /// <returns>변환한 정수 값이다.</returns>
        public static int ToInt(float value)
        {
            return (int)Math.Round((double)value, MidpointRounding.AwayFromZero);
        }
    }
}
