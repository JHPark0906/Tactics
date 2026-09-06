using HS.Framework.Ability.Abilities;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 엄폐 중인 유닛이 맞을 때 확률로 피해를 엄폐물에 넘기는 회피 어빌리티의 정의 에셋이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>엄폐의 이득은 이 어빌리티 하나로 나타난다.</b> 유닛이 엄폐 지점을 확보하고 그 자리에 도착하면 유닛 상태가
    /// 엄폐 중 태그를 붙이고, 이 정의가 그 태그를 트리거로 받아 <see cref="CoverEvasionAbility"/>를 활성화한다. 태그가
    /// 떨어지면 어빌리티도 끝난다. 그래서 어빌리티는 항상 부여해 두고, 활성 여부는 태그가 정한다.
    /// </para>
    /// <para>
    /// <b>엄폐물에 넘기는 피해는 효과로 준다.</b> 회피에 성공하면 유닛이 받을 피해량을 사정에 실어 엄폐물의 효과 실행기에
    /// 여기 연결한 피해 효과를 적용한다. 그 길은 사격이 유닛을 맞힐 때와 같은 길이므로 엄폐물 쪽 가로채기와 알림이
    /// 그대로 성립한다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Tactics/Ability/Cover Evasion Ability", fileName = "CoverEvasionAbility")]
    public sealed class CoverEvasionAbilityDefinition : GameplayAbilityDefinition
    {
        [Tooltip("회피에 성공했을 때 엄폐물에 적용할 피해 효과이다. 피해량은 Data.Damage 사정으로 싣는다.")]
        [SerializeField]
        private DamageEffectDefinition damageEffect;

        /// <summary>회피에 성공했을 때 엄폐물에 적용할 피해 효과이다.</summary>
        public DamageEffectDefinition DamageEffect => damageEffect;

        /// <inheritdoc />
        public override GameplayAbility CreateAbility()
        {
            return new CoverEvasionAbility();
        }

        /// <inheritdoc />
        public override bool TryValidate(out string errorMessage)
        {
            if (!base.TryValidate(out errorMessage))
            {
                return false;
            }

            if (damageEffect == null)
            {
                errorMessage = $"{name}: 엄폐물에 넘길 피해 효과가 연결되어 있지 않다.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 정의를 만든다. 에셋을 만들 때와 같은 규칙이 채워진다.
        /// </summary>
        /// <param name="damageEffect">엄폐물에 넘길 피해 효과이다.</param>
        /// <returns>만든 어빌리티 정의이다.</returns>
        public static CoverEvasionAbilityDefinition CreateRuntime(DamageEffectDefinition damageEffect)
        {
            var definition = CreateInstance<CoverEvasionAbilityDefinition>();
            definition.damageEffect = damageEffect;
            definition.Reset();
            return definition;
        }

        /// <summary>에셋을 처음 만들 때 규칙을 미리 채운다. 식별 태그와 엄폐 중 태그 트리거이다.</summary>
        private void Reset()
        {
            ConfigureRuntime(
                UnitAbilityTags.CoverEvasion,
                triggerWhileTagPresent: new[] { UnitAbilityTags.InCoverState });
        }
    }
}
