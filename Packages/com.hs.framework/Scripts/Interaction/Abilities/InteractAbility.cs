using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using UnityEngine;

namespace HS.Framework.Interaction.Abilities
{
    /// <summary>
    /// 상호작용을 시도하는 행위 자체를 어빌리티로 감싼 정의이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Tactics의 이동·조준과 같은 결이다.</b> 어빌리티 시스템은 액터마다 정해 둔 적은 어휘를 전제로
    /// 태그·코스트·쿨다운·차단 태그로 서로를 통제한다. 「상호작용을 시도한다」도 그 어휘의 하나로 두면,
    /// 다른 어빌리티가 차단 태그로 상호작용을 막거나(예: 기절 중에는 못 만짐), 상호작용 자체에
    /// 쿨다운을 거는 것이 <see cref="InteractionController"/>를 고치지 않고 <b>어빌리티 정의 하나만
    /// 바꿔 붙이는 것</b>으로 된다.
    /// </para>
    /// <para>
    /// 대상 탐색·가능 여부 평가·실행은 <see cref="InteractionController.PerformInteraction"/>이 담당한다.
    /// 이 어빌리티는 그 진입점을 호출해 상호작용 로직을 한 곳에서 관리한다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "HS/Interaction/Interact Ability", fileName = "InteractAbility")]
    public sealed class InteractAbilityDefinition : GameplayAbilityDefinition
    {
        /// <summary>이 어빌리티를 식별하는 태그 이름이다.</summary>
        public const string TagName = "Ability.Interact";

        private static readonly GameplayTag Tag = GameplayTag.Parse(TagName);

        /// <summary>상호작용 시도 어빌리티를 식별하는 태그이다.</summary>
        public static GameplayTag InteractTag => Tag;

        /// <inheritdoc />
        public override GameplayAbility CreateAbility() => new InteractAbility();

        /// <summary>테스트와 런타임 조립에 사용할 비저장 정의를 만든다.</summary>
        /// <returns>만든 정의이다.</returns>
        public static InteractAbilityDefinition CreateRuntime()
        {
            var definition = CreateInstance<InteractAbilityDefinition>();
            definition.ConfigureRuntime(TagName);
            return definition;
        }

        /// <summary>
        /// 활성화되는 순간 소유 액터의 <see cref="InteractionController"/>에게 실행을 넘기고 곧바로 끝나는 어빌리티이다.
        /// </summary>
        /// <remarks>
        /// <see cref="OnTick"/>을 재정의하지 않으므로 기본 구현대로 활성화된 그 틱에 끝난다. 상호작용을
        /// 시도하는 것은 순간의 판단이지 지속되는 상태가 아니므로 활성 상태로 남을 이유가 없다.
        /// </remarks>
        private sealed class InteractAbility : GameplayAbility
        {
            /// <inheritdoc />
            protected override void OnActivate()
            {
                var owner = System?.Owner;
                if (owner == null || !owner.TryGetComponent<InteractionController>(out var controller))
                {
                    return;
                }

                controller.PerformInteraction();
            }
        }
    }
}
