using System.Collections.Generic;
using HS.Framework.Ability.Effects;
using UnityEngine;

namespace HS.Framework.Ability.Abilities
{
    /// <summary>
    /// 활성화하는 순간 지정한 효과를 적용하고 곧바로 끝나는 어빌리티 정의이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 피해를 주거나 버프를 거는 것처럼 "효과를 적용하는 것이 전부"인 어빌리티가 흔하므로,
    /// 그런 경우에 게임이 클래스를 새로 만들지 않아도 되도록 프레임워크가 제공한다.
    /// 시간에 걸쳐 이어지는 어빌리티는 <see cref="GameplayAbility"/>를 상속해 직접 만든다.
    /// </para>
    /// <para>
    /// 적용한 효과는 어빌리티가 끝나도 스스로 걷히지 않는다. 효과의 수명은 효과 자신의 지속 정책이 정하며,
    /// 어빌리티가 끝났다고 걸어 둔 버프가 사라져야 한다면 그것은 어빌리티가 아니라 효과의 지속 시간으로 표현한다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "HS/Ability/Apply Effects Ability", fileName = "ApplyEffectsAbility")]
    public sealed class ApplyEffectsAbilityDefinition : GameplayAbilityDefinition
    {
        [Tooltip("활성화할 때 대상에게 적용할 효과 목록이다.")]
        [SerializeField]
        private List<GameplayEffectDefinition> effects = new();

        /// <summary>활성화할 때 적용할 효과 목록이다.</summary>
        public IReadOnlyList<GameplayEffectDefinition> Effects => effects;

        /// <inheritdoc />
        public override GameplayAbility CreateAbility()
        {
            return new ApplyEffectsAbility();
        }

        /// <inheritdoc />
        public override bool TryValidate(out string errorMessage)
        {
            if (!base.TryValidate(out errorMessage))
            {
                return false;
            }

            for (var index = 0; index < effects.Count; index++)
            {
                if (effects[index] == null)
                {
                    errorMessage = $"{index}번 효과가 비어 있다.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 어빌리티 정의를 만든다.
        /// </summary>
        /// <param name="abilityTagName">어빌리티를 식별하는 태그 이름이다.</param>
        /// <param name="appliedEffects">활성화할 때 적용할 효과 목록이다.</param>
        /// <param name="cost">활성화 비용 효과이다.</param>
        /// <param name="cooldown">쿨다운 효과이다.</param>
        /// <param name="activeTags">활성 중 부여할 태그 이름이다.</param>
        /// <param name="requiredTags">활성화에 필요한 태그 이름이다.</param>
        /// <param name="blockedTags">활성화를 막는 태그 이름이다.</param>
        /// <returns>만든 어빌리티 정의이다.</returns>
        public static ApplyEffectsAbilityDefinition CreateRuntime(
            string abilityTagName,
            IEnumerable<GameplayEffectDefinition> appliedEffects = null,
            GameplayEffectDefinition cost = null,
            GameplayEffectDefinition cooldown = null,
            IEnumerable<string> activeTags = null,
            IEnumerable<string> requiredTags = null,
            IEnumerable<string> blockedTags = null)
        {
            var definition = CreateInstance<ApplyEffectsAbilityDefinition>();
            definition.ConfigureRuntime(abilityTagName, cost, cooldown, activeTags, requiredTags, blockedTags);
            if (appliedEffects != null)
            {
                definition.effects.AddRange(appliedEffects);
            }

            return definition;
        }

        /// <summary>
        /// 효과를 적용하고 곧바로 끝나는 어빌리티이다.
        /// </summary>
        /// <remarks>
        /// <see cref="OnTick"/>을 재정의하지 않으므로 기본 구현대로 활성화된 그 자리에서 종료된다.
        /// 활성 상태가 남지 않으니 놓아야 할 자원도 없다.
        /// </remarks>
        private sealed class ApplyEffectsAbility : GameplayAbility
        {
            /// <inheritdoc />
            protected internal override void OnActivate()
            {
                if (Definition is not ApplyEffectsAbilityDefinition definition)
                {
                    return;
                }

                foreach (var effect in definition.Effects)
                {
                    if (effect != null)
                    {
                        System.Effects.Apply(effect, this);
                    }
                }
            }
        }
    }
}
