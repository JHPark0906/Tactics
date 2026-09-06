using HS.Framework.Ability.Abilities;
using UnityEngine;

namespace HS.Tactics.Units
{
    /// <summary>
    /// <see cref="DeathAbility"/>가 끝나며 보낸 소멸 이벤트로 활성화되어, 정해진 시간이 지나면 소유 액터를
    /// 전장에서 물러나게 하는 유닛 전용 어빌리티이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>물러남은 활성화를 일으킨 이벤트 안에서 일어나지 않는다.</b> 활성화되는 순간의 판정(흐른 시간이 없는
    /// 틱)에서는 끝내지 않고, 시간이 흐른 틱에서야 끝낸다. 그래서 액터가 사라지는 것은 언제나 사망 알림이 듣는
    /// 쪽 전부에 닿은 뒤이며, 알림을 받는 순서에 기대지 않는다. 물러남은 스스로 끝났을 때만 하고, 파괴나 정리로
    /// 취소되었을 때는 하지 않는다.
    /// </para>
    /// <para>
    /// <b>엄폐물에는 이 어빌리티가 없다.</b> 부서진 엄폐물은 죽은 상태 태그만 받고 그대로 남는다 — 이 경로는 Object.Destroy를 호출하지 않는다.
    /// </para>
    /// </remarks>
    public sealed class DisappearanceAbility : GameplayAbility
    {
        /// <summary>이 어빌리티가 물러나게 할 소유 액터이며 활성화될 때 잡힌다.</summary>
        private GameObject Owner { get; set; }

        /// <summary>물러나기까지 기다리는 시간(초)이며 정의가 소멸 정의가 아니면 0이다.</summary>
        private float DisappearDelay =>
            Definition is DisappearanceAbilityDefinition definition ? definition.DisappearDelay : 0f;

        /// <inheritdoc />
        /// <remarks>물러나게 할 액터가 있어야 한다. 규칙만 검증하는 자리에서는 소유자가 없으므로 거부한다.</remarks>
        public override bool CanActivate()
        {
            return System != null && System.Owner != null;
        }

        /// <inheritdoc />
        protected override void OnActivate()
        {
            Owner = System.Owner;
        }

        /// <inheritdoc />
        /// <remarks>흐른 시간이 없는 틱은 활성화되는 순간의 판정이므로 끝내지 않는다.</remarks>
        protected override GameplayAbilityTickResult OnTick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return GameplayAbilityTickResult.Running;
            }

            return ActiveTime >= DisappearDelay ? GameplayAbilityTickResult.Finished : GameplayAbilityTickResult.Running;
        }

        /// <inheritdoc />
        /// <remarks>스스로 끝났을 때만 물러난다. 파괴나 정리로 취소된 것은 물러남이 아니다.</remarks>
        protected override void OnEnd(GameplayAbilityEndReason endReason)
        {
            if (endReason == GameplayAbilityEndReason.Completed && Owner != null)
            {
                Owner.SetActive(false);
            }
        }
    }
}
