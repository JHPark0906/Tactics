using HS.Framework.Ability.Abilities;
using HS.Framework.Gameplay.Health;
using UnityEngine;

namespace HS.Tactics.Units
{
    /// <summary>
    /// <see cref="DeathAbility"/>를 액터에게 부여하기 위한 정의 에셋이다. 유닛의 사망과 엄폐물의 파괴가 같은
    /// 정의 클래스를 쓴다 — 식별 태그만 에셋마다 다르게 지정한다(유닛은 <c>Ability.Death</c>,
    /// 엄폐물은 <c>Ability.CoverDeath</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>유닛이 죽는 것과 엄폐물이 부서지는 것은 같은 꼴이다.</b> 체력이 0에 닿으면 체력 문이 사망 이벤트를
    /// 보내고, 이 정의가 그 이벤트를 트리거로 받아 <see cref="DeathAbility"/>를 활성화한다. 다른 어빌리티를
    /// 전부 취소하고 활성 중에는 전부 차단하는 규칙도 이 정의의 태그 규칙으로 표현되며, 에셋을 새로 만들면 그
    /// 규칙이 미리 채워진다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Tactics/Ability/Death Ability", fileName = "DeathAbility")]
    public sealed class DeathAbilityDefinition : GameplayAbilityDefinition
    {
        /// <inheritdoc />
        public override GameplayAbility CreateAbility()
        {
            return new DeathAbility();
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 사망 어빌리티 정의를 만든다. 에셋을 만들 때와 같은 규칙이 채워진다.
        /// </summary>
        /// <param name="abilityTagName">이 어빌리티를 식별하는 태그 이름이다. 기본값은 유닛의 사망 태그이다.</param>
        /// <returns>만든 어빌리티 정의이다.</returns>
        public static DeathAbilityDefinition CreateRuntime(string abilityTagName = UnitAbilityTags.Death)
        {
            var definition = CreateInstance<DeathAbilityDefinition>();
            definition.ConfigureRuntime(
                abilityTagName,
                cancelAbilitiesWithTags: new[] { UnitAbilityTags.Family },
                blockAbilitiesWithTags: new[] { UnitAbilityTags.Family },
                triggerEventTags: new[] { HealthAttributeComponent.DefaultDeathEventTagName });
            return definition;
        }

        /// <summary>
        /// 에셋을 처음 만들 때 사망 어빌리티의 규칙을 미리 채운다.
        /// 식별 태그, 사망 이벤트 트리거, 다른 어빌리티 전부의 취소와 차단이다.
        /// </summary>
        private void Reset()
        {
            ConfigureRuntime(
                UnitAbilityTags.Death,
                cancelAbilitiesWithTags: new[] { UnitAbilityTags.Family },
                blockAbilitiesWithTags: new[] { UnitAbilityTags.Family },
                triggerEventTags: new[] { HealthAttributeComponent.DefaultDeathEventTagName });
        }
    }
}
