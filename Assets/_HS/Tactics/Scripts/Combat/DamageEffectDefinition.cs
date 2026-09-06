using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 체력을 깎는 즉시 효과의 정의 에셋이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>피해는 효과다.</b> 쏜 쪽이 대상의 체력을 직접 깎지 않고 이 효과를 대상에게 적용한다. 효과가 체력 어트리뷰트의
    /// 기본값을 바꾸면 체력 문이 그 변화를 받아 가로채기(엄폐 흡수)와 알림(피해·사망)을 처리하므로,
    /// 어떤 원인의 피해든 같은 자리를 지난다.
    /// </para>
    /// <para>
    /// <b>크기는 쏘는 순간 정해진다.</b> 명중 판정을 거친 피해량을 쏘는 쪽이 <see cref="DamageTagName"/>으로 사정에 실어
    /// 보내고, 이 효과의 수정자가 그 값에 계수 -1을 곱해 체력에서 뺀다. 그래서 에셋에는 숫자가 없고 병종마다 다른
    /// 피해량은 유닛 정의와 어트리뷰트가 준다. 에셋을 새로 만들면 그 모양이 미리 채워지며, 체력 어트리뷰트만 연결하면 된다.
    /// </para>
    /// <para>
    /// 즉시 효과이고 체력을 깎는 방향이어야 한다는 것을 검증이 지킨다. 그 밖의 모양으로 바꾸면 부여할 때 거부된다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Tactics/Effect/Damage Effect", fileName = "DamageEffect")]
    public sealed class DamageEffectDefinition : GameplayEffectDefinition
    {
        /// <summary>쏘는 쪽이 사정에 피해량을 실을 때 쓰는 태그 이름이다.</summary>
        public const string DamageTagName = "Data.Damage";

        /// <summary>피해 효과의 에셋 태그 이름이다. 면역과 제거 규칙이 이 이름으로 피해 효과를 가리킨다.</summary>
        public const string AssetTagName = "Effect.Damage";

        /// <inheritdoc />
        public override bool TryValidate(out string errorMessage)
        {
            if (!base.TryValidate(out errorMessage))
            {
                return false;
            }

            if (!IsInstant)
            {
                errorMessage = "피해 효과는 즉시 효과여야 한다. 남아 있다가 되돌아가는 피해는 피해가 아니다.";
                return false;
            }

            if (Modifiers.Count == 0)
            {
                errorMessage = "피해 효과에 체력을 깎는 수정자가 없다.";
                return false;
            }

            foreach (var modifier in Modifiers)
            {
                if (modifier.Operation != AttributeModifierOperation.Add || !LowersTheValue(modifier))
                {
                    errorMessage = $"수정자 {modifier}는 값을 깎는 더하기가 아니다. 피해는 체력에서 빼는 더하기로 표현한다.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 피해 효과 정의를 만든다. 크기는 쏘는 쪽이 사정에 싣는다.
        /// </summary>
        /// <param name="healthAttribute">깎을 체력 어트리뷰트 정의이다.</param>
        /// <returns>만든 효과 정의이다.</returns>
        public static DamageEffectDefinition CreateRuntime(AttributeDefinition healthAttribute)
        {
            var definition = CreateInstance<DamageEffectDefinition>();
            definition.Configure(healthAttribute);
            return definition;
        }

        /// <summary>수정자가 값을 깎는 방향인지 확인한다. 고정 크기는 음수, 사정에서 읽는 크기는 계수가 음수여야 한다.</summary>
        /// <param name="modifier">확인할 수정자이다.</param>
        /// <returns>깎는 방향이면 true이다.</returns>
        private static bool LowersTheValue(GameplayEffectModifier modifier)
        {
            return modifier.MagnitudeSource == GameplayEffectMagnitudeSource.Scalar
                ? modifier.Magnitude < 0f
                : modifier.Coefficient < 0f;
        }

        /// <summary>피해 효과의 모양을 채운다. 체력 어트리뷰트가 없으면 수정자의 자리만 잡아 두어 검증이 그것을 드러낸다.</summary>
        /// <param name="healthAttribute">깎을 체력 어트리뷰트 정의이며 없으면 null이다.</param>
        private void Configure(AttributeDefinition healthAttribute)
        {
            ConfigureRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[]
                {
                    GameplayEffectModifier.CreateSetByCallerRuntime(
                        healthAttribute, AttributeModifierOperation.Add, DamageTagName, coefficient: -1f)
                },
                assetTags: new[] { AssetTagName });
        }

        /// <summary>에셋을 처음 만들 때 피해 효과의 모양을 미리 채운다. 체력 어트리뷰트는 인스펙터에서 연결한다.</summary>
        private void Reset()
        {
            Configure(null);
        }
    }
}
