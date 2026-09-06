using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Effects;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>
    /// 끝내라고 할 때까지 활성 상태로 남는 테스트용 어빌리티이다.
    /// 취소·차단·트리거처럼 어빌리티가 살아 있는 동안의 관계를 검증할 때 쓴다.
    /// </summary>
    internal sealed class RunningAbility : GameplayAbility
    {
        /// <summary>활성화된 횟수이다.</summary>
        public int ActivateCount { get; private set; }

        /// <summary>종료된 횟수이다.</summary>
        public int EndCount { get; private set; }

        /// <summary>다음 틱에서 스스로 끝날지 여부이다.</summary>
        public bool FinishRequested { get; set; }

        /// <inheritdoc />
        protected override void OnActivate()
        {
            ActivateCount++;
            FinishRequested = false;
        }

        /// <inheritdoc />
        protected override GameplayAbilityTickResult OnTick(float deltaTime)
        {
            return FinishRequested ? GameplayAbilityTickResult.Finished : GameplayAbilityTickResult.Running;
        }

        /// <inheritdoc />
        protected override void OnEnd(GameplayAbilityEndReason endReason)
        {
            EndCount++;
        }
    }

    /// <summary>
    /// 미리 만들어 둔 <see cref="RunningAbility"/>를 돌려주며, 태그 규칙과 트리거를 마음대로 지정할 수 있는 테스트용 정의이다.
    /// </summary>
    internal sealed class RunningAbilityDefinition : GameplayAbilityDefinition
    {
        private RunningAbility _ability;

        /// <inheritdoc />
        public override GameplayAbility CreateAbility()
        {
            return _ability;
        }

        /// <summary>지정한 어빌리티를 돌려주는 정의를 만든다.</summary>
        /// <param name="tagName">어빌리티를 식별하는 태그 이름이다.</param>
        /// <param name="ability">부여될 때 돌려줄 어빌리티이며 없으면 새로 만든다.</param>
        /// <param name="assetTags">이 어빌리티를 설명하는 태그 이름이다.</param>
        /// <param name="cancelAbilitiesWithTags">활성화하는 순간 취소할 다른 어빌리티의 태그 이름이다.</param>
        /// <param name="blockAbilitiesWithTags">활성 중인 동안 막을 다른 어빌리티의 태그 이름이다.</param>
        /// <param name="triggerEventTags">받으면 활성화를 시도할 이벤트 태그 이름이다.</param>
        /// <param name="triggerOnTagGained">대상이 얻는 순간 활성화를 시도할 태그 이름이다.</param>
        /// <param name="triggerWhileTagPresent">대상이 가진 동안 활성 상태를 유지할 태그 이름이다.</param>
        /// <param name="activateOnGrant">부여되는 순간 활성화를 시도할지 여부이다.</param>
        /// <param name="activeTags">활성 중 부여할 태그 이름이다.</param>
        /// <param name="blockedTags">활성화를 막는 태그 이름이다.</param>
        /// <param name="cooldown">쿨다운 효과이다.</param>
        /// <returns>만든 정의이다.</returns>
        public static RunningAbilityDefinition CreateRuntime(
            string tagName,
            RunningAbility ability = null,
            IEnumerable<string> assetTags = null,
            IEnumerable<string> cancelAbilitiesWithTags = null,
            IEnumerable<string> blockAbilitiesWithTags = null,
            IEnumerable<string> triggerEventTags = null,
            IEnumerable<string> triggerOnTagGained = null,
            IEnumerable<string> triggerWhileTagPresent = null,
            bool activateOnGrant = false,
            IEnumerable<string> activeTags = null,
            IEnumerable<string> blockedTags = null,
            GameplayEffectDefinition cooldown = null,
            IEnumerable<string> cueTags = null)
        {
            var definition = CreateInstance<RunningAbilityDefinition>();
            definition._ability = ability ?? new RunningAbility();
            definition.ConfigureRuntime(
                tagName,
                cooldown: cooldown,
                activeTags: activeTags,
                blockedTags: blockedTags,
                assetTags: assetTags,
                cancelAbilitiesWithTags: cancelAbilitiesWithTags,
                blockAbilitiesWithTags: blockAbilitiesWithTags,
                triggerEventTags: triggerEventTags,
                triggerOnTagGained: triggerOnTagGained,
                triggerWhileTagPresent: triggerWhileTagPresent,
                activateOnGranted: activateOnGrant,
                cueTags: cueTags);
            return definition;
        }
    }
}
