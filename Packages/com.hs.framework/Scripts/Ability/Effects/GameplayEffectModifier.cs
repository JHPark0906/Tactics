using System;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Tags;
using UnityEngine;

namespace HS.Framework.Ability.Effects
{
    /// <summary>
    /// 효과가 얼마나 오래 남는지를 정하는 정책이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 구분이 효과 계층의 핵심이다.</b> 즉시 효과는 기본값을 바꾸고, 지속·무한 효과는 수정자를 얹었다가 뗀다.
    /// 둘을 바꿔 구현하면 값이 새거나 영구히 남는다. 즉시 효과를 수정자로 만들면 효과가 사라질 때 피해가 되돌아가고,
    /// 지속 효과를 기본값 변경으로 만들면 만료돼도 변화가 남는다.
    /// </para>
    /// <para>
    /// 어트리뷰트 계층이 기본값과 수정자의 역할을 나눠 둔 이유가 그대로 여기에 대응한다.
    /// 원인이 사라져도 남아야 하는 변화는 기본값, 원인이 사라지면 되돌아가야 하는 변화는 수정자이다.
    /// </para>
    /// </remarks>
    public enum GameplayEffectDurationPolicy
    {
        /// <summary>한 번 적용하고 끝난다. 어트리뷰트의 기본값을 바꾸므로 되돌아가지 않는다.</summary>
        Instant = 0,

        /// <summary>정해진 시간 동안 유지된다. 수정자를 얹었다가 만료 시 뗀다.</summary>
        Duration = 1,

        /// <summary>제거될 때까지 유지된다. 수정자를 얹었다가 제거 시 뗀다.</summary>
        Infinite = 2
    }

    /// <summary>
    /// 수정자의 크기를 어디서 읽는지 정한다.
    /// </summary>
    /// <remarks>
    /// 숫자로 고정된 크기 말고는 모두 <c>계수 × (읽은 값 + 곱하기 전 더함) + 곱한 뒤 더함</c>으로 계산한다.
    /// 계수를 -1로 두면 호출자가 넘긴 양수 피해량을 체력에서 빼는 더하기가 되므로,
    /// 넘기는 쪽이 부호를 신경 쓰지 않아도 된다.
    /// </remarks>
    public enum GameplayEffectMagnitudeSource
    {
        /// <summary>정의에 적힌 숫자를 그대로 쓴다.</summary>
        Scalar = 0,

        /// <summary>적용하는 쪽이 사정에 태그로 등록한 값을 읽는다. 명중 판정 뒤 정해지는 피해량 같은 것이다.</summary>
        SetByCaller = 1,

        /// <summary>사정에 담긴 원천 어트리뷰트 집합에서 읽는다. 보통 가해자의 공격력이다.</summary>
        SourceAttribute = 2,

        /// <summary>효과를 받는 대상의 어트리뷰트 집합에서 읽는다. 최대 체력의 몇 할 같은 것이다.</summary>
        TargetAttribute = 3
    }

    /// <summary>
    /// 효과가 어떤 어트리뷰트를 어떻게 바꾸는지 나타내는 항목이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 같은 항목이라도 지속 정책에 따라 적용 방식이 달라진다. 즉시 효과와 주기 실행에서는 기본값을 바꾸고,
    /// 지속·무한 효과에서는 같은 연산의 수정자로 얹는다. 연산의 계산 순서는 어트리뷰트 계층이 정한 대로
    /// 더하기 → 곱하기 → 덮어쓰기이다.
    /// </para>
    /// <para>
    /// 크기는 정의에 적힌 숫자일 수도, 적용하는 순간의 사정에서 읽은 값일 수도 있다.
    /// 사정에서 읽는 크기는 적용하는 순간 한 번 정해지며, 지속 효과라도 그 뒤 원천이 바뀐다고 따라 바뀌지 않는다.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class GameplayEffectModifier
    {
        [Tooltip("바꿀 어트리뷰트 정의이다.")]
        [SerializeField]
        private AttributeDefinition attribute;

        [Tooltip("값을 바꾸는 방식이다. 계산 순서는 더하기 → 곱하기 → 덮어쓰기이다.")]
        [SerializeField]
        private AttributeModifierOperation operation = AttributeModifierOperation.Add;

        [Tooltip("크기를 어디서 읽을지 정한다. Scalar면 아래 크기를 그대로 쓴다.")]
        [SerializeField]
        private GameplayEffectMagnitudeSource magnitudeSource = GameplayEffectMagnitudeSource.Scalar;

        [Tooltip("수정자의 크기이다. 더하기는 더할 양, 곱하기는 곱할 배수, 덮어쓰기는 대체할 값이다. Scalar에서만 쓴다.")]
        [SerializeField]
        private float magnitude;

        [Tooltip("SetByCaller에서 읽을 값의 태그 이름이다. 예: Data.Damage")]
        [SerializeField]
        private string setByCallerTagName;

        [Tooltip("SourceAttribute나 TargetAttribute에서 읽을 어트리뷰트 정의이다.")]
        [SerializeField]
        private AttributeDefinition capturedAttribute;

        [Tooltip("읽은 값에 곱할 계수이다. -1이면 양수로 넘긴 피해량을 빼는 더하기가 된다.")]
        [SerializeField]
        private float coefficient = 1f;

        [Tooltip("계수를 곱하기 전에 읽은 값에 더할 양이다.")]
        [SerializeField]
        private float preMultiplyAdd;

        [Tooltip("계수를 곱한 뒤에 더할 양이다.")]
        [SerializeField]
        private float postMultiplyAdd;

        /// <summary>이름에서 해석한 태그를 담아 두는 지연 생성 캐시이다.</summary>
        private GameplayTag? _setByCallerTag;

        /// <summary>바꿀 어트리뷰트 정의이며 지정되지 않았으면 null이다.</summary>
        public AttributeDefinition Attribute => attribute;

        /// <summary>값을 바꾸는 방식이다.</summary>
        public AttributeModifierOperation Operation => operation;

        /// <summary>크기를 어디서 읽는지이다.</summary>
        public GameplayEffectMagnitudeSource MagnitudeSource => magnitudeSource;

        /// <summary>정의에 적힌 크기이며 <see cref="GameplayEffectMagnitudeSource.Scalar"/>에서만 쓰인다.</summary>
        public float Magnitude => magnitude;

        /// <summary>SetByCaller에서 읽을 값의 태그이며 이름이 잘못되었으면 유효하지 않은 값이다.</summary>
        public GameplayTag SetByCallerTag
        {
            get
            {
                if (_setByCallerTag.HasValue)
                {
                    return _setByCallerTag.Value;
                }

                GameplayTag.TryParse(setByCallerTagName, out var tag);
                _setByCallerTag = tag;
                return tag;
            }
        }

        /// <summary>원천이나 대상에서 읽을 어트리뷰트 정의이며 없으면 null이다.</summary>
        public AttributeDefinition CapturedAttribute => capturedAttribute;

        /// <summary>읽은 값에 곱할 계수이다.</summary>
        public float Coefficient => coefficient;

        /// <summary>계수를 곱하기 전에 더할 양이다.</summary>
        public float PreMultiplyAdd => preMultiplyAdd;

        /// <summary>계수를 곱한 뒤에 더할 양이다.</summary>
        public float PostMultiplyAdd => postMultiplyAdd;

        /// <summary>이 항목이 적용 가능한 형태인지 여부이다.</summary>
        public bool IsValid => TryValidate(out _);

        /// <summary>
        /// 이 항목이 적용 가능한 형태인지 검사한다.
        /// </summary>
        /// <param name="errorMessage">검증에 실패한 원인이며 성공하면 null이다.</param>
        /// <returns>문제가 없으면 true이다.</returns>
        public bool TryValidate(out string errorMessage)
        {
            if (attribute == null)
            {
                errorMessage = "어트리뷰트가 지정되지 않았다.";
                return false;
            }

            switch (magnitudeSource)
            {
                case GameplayEffectMagnitudeSource.SetByCaller when !SetByCallerTag.IsValid:
                    errorMessage = $"SetByCaller 태그 '{setByCallerTagName}'이 유효한 형식이 아니다.";
                    return false;
                case GameplayEffectMagnitudeSource.SourceAttribute when capturedAttribute == null:
                case GameplayEffectMagnitudeSource.TargetAttribute when capturedAttribute == null:
                    errorMessage = "크기를 읽을 어트리뷰트가 지정되지 않았다.";
                    return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 적용하는 순간의 사정에서 이 수정자의 크기를 정한다.
        /// </summary>
        /// <param name="context">적용하는 순간의 사정이며 없으면 null이다.</param>
        /// <param name="targetAttributes">효과를 받는 대상의 어트리뷰트 집합이다.</param>
        /// <param name="resolvedMagnitude">정해진 크기이며 정하지 못했으면 0이다.</param>
        /// <returns>크기를 정했으면 true이며, 읽을 곳이 없으면 false이다.</returns>
        public bool TryResolveMagnitude(
            GameplayEffectContext context,
            AttributeSet targetAttributes,
            out float resolvedMagnitude)
        {
            float readValue;
            switch (magnitudeSource)
            {
                case GameplayEffectMagnitudeSource.Scalar:
                    resolvedMagnitude = magnitude;
                    return true;

                case GameplayEffectMagnitudeSource.SetByCaller:
                    if (context == null || !context.TryGetSetByCaller(SetByCallerTag, out readValue))
                    {
                        resolvedMagnitude = 0f;
                        return false;
                    }

                    break;

                case GameplayEffectMagnitudeSource.SourceAttribute:
                    if (context?.SourceAttributes == null ||
                        !context.SourceAttributes.TryGetCurrentValue(capturedAttribute, out readValue))
                    {
                        resolvedMagnitude = 0f;
                        return false;
                    }

                    break;

                case GameplayEffectMagnitudeSource.TargetAttribute:
                    if (targetAttributes == null || !targetAttributes.TryGetCurrentValue(capturedAttribute, out readValue))
                    {
                        resolvedMagnitude = 0f;
                        return false;
                    }

                    break;

                default:
                    resolvedMagnitude = 0f;
                    return false;
            }

            resolvedMagnitude = coefficient * (readValue + preMultiplyAdd) + postMultiplyAdd;
            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 고정 크기 수정자 항목을 만든다.
        /// </summary>
        /// <param name="attribute">바꿀 어트리뷰트 정의이다.</param>
        /// <param name="operation">값을 바꾸는 방식이다.</param>
        /// <param name="magnitude">수정자의 크기이다.</param>
        /// <returns>만든 수정자 항목이다.</returns>
        public static GameplayEffectModifier CreateRuntime(
            AttributeDefinition attribute,
            AttributeModifierOperation operation,
            float magnitude)
        {
            return new GameplayEffectModifier
            {
                attribute = attribute,
                operation = operation,
                magnitude = magnitude
            };
        }

        /// <summary>
        /// 적용하는 쪽이 넘긴 값에서 크기를 읽는 수정자 항목을 만든다.
        /// </summary>
        /// <param name="attribute">바꿀 어트리뷰트 정의이다.</param>
        /// <param name="operation">값을 바꾸는 방식이다.</param>
        /// <param name="setByCallerTagName">읽을 값의 태그 이름이다.</param>
        /// <param name="coefficient">읽은 값에 곱할 계수이다.</param>
        /// <param name="preMultiplyAdd">계수를 곱하기 전에 더할 양이다.</param>
        /// <param name="postMultiplyAdd">계수를 곱한 뒤에 더할 양이다.</param>
        /// <returns>만든 수정자 항목이다.</returns>
        public static GameplayEffectModifier CreateSetByCallerRuntime(
            AttributeDefinition attribute,
            AttributeModifierOperation operation,
            string setByCallerTagName,
            float coefficient = 1f,
            float preMultiplyAdd = 0f,
            float postMultiplyAdd = 0f)
        {
            return new GameplayEffectModifier
            {
                attribute = attribute,
                operation = operation,
                magnitudeSource = GameplayEffectMagnitudeSource.SetByCaller,
                setByCallerTagName = setByCallerTagName,
                coefficient = coefficient,
                preMultiplyAdd = preMultiplyAdd,
                postMultiplyAdd = postMultiplyAdd
            };
        }

        /// <summary>
        /// 원천이나 대상의 어트리뷰트에서 크기를 읽는 수정자 항목을 만든다.
        /// </summary>
        /// <param name="attribute">바꿀 어트리뷰트 정의이다.</param>
        /// <param name="operation">값을 바꾸는 방식이다.</param>
        /// <param name="source">원천에서 읽을지 대상에서 읽을지이며 둘 중 하나여야 한다.</param>
        /// <param name="capturedAttribute">읽을 어트리뷰트 정의이다.</param>
        /// <param name="coefficient">읽은 값에 곱할 계수이다.</param>
        /// <param name="preMultiplyAdd">계수를 곱하기 전에 더할 양이다.</param>
        /// <param name="postMultiplyAdd">계수를 곱한 뒤에 더할 양이다.</param>
        /// <returns>만든 수정자 항목이다.</returns>
        /// <exception cref="ArgumentException">읽는 곳이 원천도 대상도 아니면 발생한다.</exception>
        public static GameplayEffectModifier CreateAttributeBasedRuntime(
            AttributeDefinition attribute,
            AttributeModifierOperation operation,
            GameplayEffectMagnitudeSource source,
            AttributeDefinition capturedAttribute,
            float coefficient = 1f,
            float preMultiplyAdd = 0f,
            float postMultiplyAdd = 0f)
        {
            if (source != GameplayEffectMagnitudeSource.SourceAttribute &&
                source != GameplayEffectMagnitudeSource.TargetAttribute)
            {
                throw new ArgumentException("어트리뷰트에서 읽는 크기는 원천이나 대상이어야 한다.", nameof(source));
            }

            return new GameplayEffectModifier
            {
                attribute = attribute,
                operation = operation,
                magnitudeSource = source,
                capturedAttribute = capturedAttribute,
                coefficient = coefficient,
                preMultiplyAdd = preMultiplyAdd,
                postMultiplyAdd = postMultiplyAdd
            };
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var attributeText = attribute != null ? attribute.Id : "None";
            return magnitudeSource switch
            {
                GameplayEffectMagnitudeSource.SetByCaller => $"{attributeText} {operation} SetByCaller({setByCallerTagName})",
                GameplayEffectMagnitudeSource.SourceAttribute =>
                    $"{attributeText} {operation} Source({(capturedAttribute != null ? capturedAttribute.Id : "None")})",
                GameplayEffectMagnitudeSource.TargetAttribute =>
                    $"{attributeText} {operation} Target({(capturedAttribute != null ? capturedAttribute.Id : "None")})",
                _ => $"{attributeText} {operation} {magnitude}"
            };
        }
    }

    /// <summary>
    /// 효과 실행기에서 일어난 변화의 종류이다.
    /// </summary>
    public enum GameplayEffectChangeKind
    {
        /// <summary>지속 또는 무한 효과가 적용되어 유지되기 시작했다.</summary>
        Applied = 0,

        /// <summary>즉시 효과가 실행되었거나 지속 효과의 주기가 한 번 실행되었다.</summary>
        Executed = 1,

        /// <summary>유지되던 효과가 만료되거나 제거되어 되돌려졌다.</summary>
        Removed = 2,

        /// <summary>쌓이는 효과의 층수가 바뀌었거나, 한도에 닿은 재적용으로 지속 시간이 갱신되었다.</summary>
        StackChanged = 3,

        /// <summary>유지 중 효과가 진행 조건에 따라 억제되거나 억제에서 풀렸다. 억제 여부는 기록의 IsInhibited가 말한다.</summary>
        InhibitionChanged = 4
    }

    /// <summary>
    /// 효과 실행기의 변화를 알리는 값이다.
    /// </summary>
    public readonly struct GameplayEffectChange
    {
        /// <summary>변화가 일어난 효과 정의이다.</summary>
        public GameplayEffectDefinition Definition { get; }

        /// <summary>
        /// 변화가 일어난 활성 효과이며, 즉시 효과의 실행처럼 유지되는 효과가 없으면 null이다.
        /// </summary>
        public ActiveGameplayEffect Effect { get; }

        /// <summary>일어난 변화의 종류이다.</summary>
        public GameplayEffectChangeKind ChangeKind { get; }

        /// <summary>효과 변화 알림을 생성한다.</summary>
        /// <param name="definition">변화가 일어난 효과 정의이다.</param>
        /// <param name="effect">변화가 일어난 활성 효과이며 없으면 null이다.</param>
        /// <param name="changeKind">일어난 변화의 종류이다.</param>
        public GameplayEffectChange(
            GameplayEffectDefinition definition,
            ActiveGameplayEffect effect,
            GameplayEffectChangeKind changeKind)
        {
            Definition = definition;
            Effect = effect;
            ChangeKind = changeKind;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{(Definition != null ? Definition.name : "None")} {ChangeKind}";
        }
    }
}
