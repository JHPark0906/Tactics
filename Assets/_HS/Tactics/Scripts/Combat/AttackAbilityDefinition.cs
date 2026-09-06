using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 사격 어빌리티를 액터에게 부여하기 위한 정의 에셋이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 사격 수치는 이 에셋이 아니라 유닛 정의와 어트리뷰트에서 읽는다. 같은 사격 어빌리티를 여러 병종이 공유하되
    /// 사거리와 피해는 병종마다 다른 것이 이 게임의 구성이기 때문이다.
    /// 그래서 이 에셋에는 활성화 조건과 태그, 그리고 피해를 어떤 효과로 줄지만 담긴다.
    /// </para>
    /// <para>
    /// <b>피해 효과.</b> 명중했을 때 대상에게 적용할 <see cref="DamageEffectDefinition"/>을 연결하면 피해가 효과로 간다.
    /// 피해량은 쏘는 순간 사정에 실리므로 효과 에셋에는 숫자가 없다. 비워 두면 대상의 피해 창구에 직접 피해를 준다.
    /// </para>
    /// <para>
    /// <b>공격력 어트리뷰트.</b> 연결하고 소유자가 그 어트리뷰트를 가지고 있으면 피해량을 거기서 읽는다.
    /// 레벨이 공격력을 올리는 길이 이것이며, 없으면 유닛 정의의 공격력을 쓴다.
    /// </para>
    /// <para>
    /// <b>사격 간격은 이 에셋의 쿨다운 칸으로 표현하지 않는다.</b> 간격을 재활성화 조건으로 두면
    /// 간격을 기다리는 동안 활성화가 실패해 유닛이 아래 우선순위로 떨어지고, 결국 엄폐를 유지한 채
    /// 사격하지 못한다. 간격은 <see cref="AttackAbility"/>가 활성 상태 안에서 세며,
    /// 쿨다운 칸은 "교전을 마친 뒤 잠시 다시 걸 수 없게" 하는 다른 목적에만 쓴다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Tactics/Ability/Attack Ability", fileName = "AttackAbility")]
    public sealed class AttackAbilityDefinition : GameplayAbilityDefinition
    {
        [Tooltip("명중했을 때 대상에게 적용할 피해 효과이다. 비워 두면 대상의 피해 창구에 직접 피해를 준다.")]
        [SerializeField]
        private DamageEffectDefinition damageEffect;

        [Tooltip("피해량을 읽을 공격력 어트리뷰트이다. 비워 두거나 소유자에게 없으면 유닛 정의의 공격력을 쓴다.")]
        [SerializeField]
        private AttributeDefinition attackPowerAttribute;

        /// <summary>명중했을 때 대상에게 적용할 피해 효과이며 없으면 null이다.</summary>
        public DamageEffectDefinition DamageEffect => damageEffect;

        /// <summary>피해량을 읽을 공격력 어트리뷰트이며 없으면 null이다.</summary>
        public AttributeDefinition AttackPowerAttribute => attackPowerAttribute;

        /// <inheritdoc />
        public override GameplayAbility CreateAbility()
        {
            return new AttackAbility();
        }

        /// <inheritdoc />
        public override bool TryValidate(out string errorMessage)
        {
            if (!base.TryValidate(out errorMessage))
            {
                return false;
            }

            if (damageEffect != null && !damageEffect.TryValidate(out var damageError))
            {
                errorMessage = $"피해 효과 {damageEffect.name}: {damageError}";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 사격 어빌리티 정의를 만든다.
        /// </summary>
        /// <param name="abilityTagName">어빌리티를 식별하는 태그 이름이다.</param>
        /// <param name="damage">명중했을 때 적용할 피해 효과이며 없으면 null이다.</param>
        /// <param name="attackPower">피해량을 읽을 공격력 어트리뷰트이며 없으면 null이다.</param>
        /// <returns>만든 어빌리티 정의이다.</returns>
        public static AttackAbilityDefinition CreateRuntime(
            string abilityTagName,
            DamageEffectDefinition damage = null,
            AttributeDefinition attackPower = null)
        {
            var definition = CreateInstance<AttackAbilityDefinition>();
            definition.damageEffect = damage;
            definition.attackPowerAttribute = attackPower;
            definition.ConfigureRuntime(abilityTagName);
            return definition;
        }
    }
}
