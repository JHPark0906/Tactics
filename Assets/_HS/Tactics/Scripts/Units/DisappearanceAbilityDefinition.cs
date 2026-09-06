using HS.Framework.Ability.Abilities;
using UnityEngine;

namespace HS.Tactics.Units
{
    /// <summary>
    /// <see cref="DisappearanceAbility"/>를 유닛에게 부여하기 위한 정의 에셋이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>사망과 소멸은 서로 다른 어빌리티다.</b> <see cref="DeathAbility"/>가 끝나며 보내는 소멸 이벤트를 이
    /// 정의가 트리거로 받아 활성화한다 — 지금 <see cref="DeathAbilityDefinition"/>이 체력 문의 사망 이벤트를
    /// 트리거로 받는 것과 같은 패턴이다.
    /// </para>
    /// <para>
    /// <b>물러나기까지의 시간은 연출의 길이다.</b> 그 시간이 지나야 유닛이 전장에서 사라지며, 값은 임시값이다.
    /// 0이면 다음 고정 스텝에 물러난다. 0이라도 소멸 이벤트 안에서 물러나지는 않는다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Tactics/Ability/Disappearance Ability", fileName = "DisappearanceAbility")]
    public sealed class DisappearanceAbilityDefinition : GameplayAbilityDefinition
    {
        [Tooltip("활성화된 뒤 물러나기까지 기다리는 시간(초)이다. 연출 길이에 맞춘다. 0이면 다음 틱에 물러난다.")]
        [SerializeField]
        [Min(0f)]
        private float disappearDelay;

        /// <summary>활성화된 뒤 물러나기까지 기다리는 시간(초)이다.</summary>
        public float DisappearDelay => Mathf.Max(0f, disappearDelay);

        /// <inheritdoc />
        public override GameplayAbility CreateAbility()
        {
            return new DisappearanceAbility();
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 소멸 어빌리티 정의를 만든다. 에셋을 만들 때와 같은 규칙이 채워진다.
        /// </summary>
        /// <param name="disappearDelay">물러나기까지 기다리는 시간(초)이다.</param>
        /// <returns>만든 어빌리티 정의이다.</returns>
        public static DisappearanceAbilityDefinition CreateRuntime(float disappearDelay = 0f)
        {
            var definition = CreateInstance<DisappearanceAbilityDefinition>();
            definition.disappearDelay = Mathf.Max(0f, disappearDelay);
            definition.Reset();
            return definition;
        }

        /// <summary>
        /// 에셋을 처음 만들 때 소멸 어빌리티의 규칙을 미리 채운다.
        /// 식별 태그, 소멸 이벤트 트리거, 다른 어빌리티 전부의 취소와 차단이다.
        /// </summary>
        private void Reset()
        {
            ConfigureRuntime(
                UnitAbilityTags.Disappearance,
                cancelAbilitiesWithTags: new[] { UnitAbilityTags.Family },
                blockAbilitiesWithTags: new[] { UnitAbilityTags.Family },
                triggerEventTags: new[] { DeathAbility.DisappearanceEventTagName });
        }
    }
}
