using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 체력을 회복하는 즉시 효과의 정의 에셋이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>회복도 피해와 같은 문을 지난다.</b> 체력 어트리뷰트의 기본값을 올리는 즉시 효과이며, 최대 체력을 넘는 몫은
    /// 상한 어트리뷰트가 잘라 낸다. 그래서 회복 효과는 넘침을 스스로 다루지 않으며, 체력 정의가 최대 체력을
    /// 상한으로 삼고 있어야 한다는 것을 검증이 지킨다.
    /// </para>
    /// <para>
    /// 회복량은 에셋의 고정 크기이며 임시값이다. 사정에서 읽는 크기로 바꿔도 검증은 그대로 통과한다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Tactics/Effect/Heal Effect", fileName = "HealEffect")]
    public sealed class HealEffectDefinition : GameplayEffectDefinition
    {
        /// <summary>에셋을 처음 만들 때 채우는 회복량이다. 임시값이다.</summary>
        public const float DefaultHealAmount = 25f;

        /// <inheritdoc />
        public override bool TryValidate(out string errorMessage)
        {
            if (!base.TryValidate(out errorMessage))
            {
                return false;
            }

            if (!IsInstant)
            {
                errorMessage = "회복 효과는 즉시 효과여야 한다. 남아 있다가 되돌아가는 회복은 회복이 아니다.";
                return false;
            }

            if (Modifiers.Count == 0)
            {
                errorMessage = "회복 효과에 체력을 올리는 수정자가 없다.";
                return false;
            }

            foreach (var modifier in Modifiers)
            {
                if (modifier.Operation != AttributeModifierOperation.Add || !RaisesTheValue(modifier))
                {
                    errorMessage = $"수정자 {modifier}는 값을 올리는 더하기가 아니다. 회복은 체력에 더하는 더하기로 표현한다.";
                    return false;
                }

                if (modifier.Attribute.CapAttribute == null && !modifier.Attribute.HasMaxValue)
                {
                    errorMessage = $"어트리뷰트 {modifier.Attribute.Id}에 상한이 없어 회복이 넘친다. 최대 체력을 상한으로 연결해야 한다.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 회복 효과 정의를 만든다.
        /// </summary>
        /// <param name="healthAttribute">올릴 체력 어트리뷰트 정의이다.</param>
        /// <param name="amount">회복량이다.</param>
        /// <returns>만든 효과 정의이다.</returns>
        public static HealEffectDefinition CreateRuntime(AttributeDefinition healthAttribute, float amount = DefaultHealAmount)
        {
            var definition = CreateInstance<HealEffectDefinition>();
            definition.Configure(healthAttribute, amount);
            return definition;
        }

        /// <summary>수정자가 값을 올리는 방향인지 확인한다. 고정 크기는 양수, 사정에서 읽는 크기는 계수가 양수여야 한다.</summary>
        /// <param name="modifier">확인할 수정자이다.</param>
        /// <returns>올리는 방향이면 true이다.</returns>
        private static bool RaisesTheValue(GameplayEffectModifier modifier)
        {
            return modifier.MagnitudeSource == GameplayEffectMagnitudeSource.Scalar
                ? modifier.Magnitude > 0f
                : modifier.Coefficient > 0f;
        }

        /// <summary>회복 효과의 모양을 채운다.</summary>
        /// <param name="healthAttribute">올릴 체력 어트리뷰트 정의이며 없으면 null이다.</param>
        /// <param name="amount">회복량이다.</param>
        private void Configure(AttributeDefinition healthAttribute, float amount)
        {
            ConfigureRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(healthAttribute, AttributeModifierOperation.Add, amount) });
        }

        /// <summary>에셋을 처음 만들 때 회복 효과의 모양을 미리 채운다. 체력 어트리뷰트는 인스펙터에서 연결한다.</summary>
        private void Reset()
        {
            Configure(null, DefaultHealAmount);
        }
    }
}
