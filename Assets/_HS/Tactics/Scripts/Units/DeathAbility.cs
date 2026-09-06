using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;

namespace HS.Tactics.Units
{
    /// <summary>
    /// 활성화되면 소유 액터에게 죽은 상태 태그를 무한 효과로 붙이는 것만 하는 어빌리티이다.
    /// 유닛과 엄폐물이 함께 쓰는 공통 클래스이며 둘을 가르는 분기가 없다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 어빌리티는 죽은 상태를 알리기만 한다.</b> 이동을 멈추거나, 행동 트리를 끄거나, 엄폐 예약을 놓거나,
    /// 엄폐 지점을 파괴 표시하는 일은 여기서 하지 않는다. 그런 반응은 각자의 컴포넌트(<c>UnitCoverState</c>,
    /// <c>PlanarCharacterMover</c>, 행동 트리 쪽 반응, <c>CoverPoint</c>)가 자기 오브젝트의 죽은 상태 태그 변화를
    /// 직접 구독해 스스로 한다. 유닛인지 엄폐물인지 몰라도 되는 것은 그래서이다.
    /// </para>
    /// <para>
    /// <b>끝나면 소멸을 트리거한다.</b> 활성화 자체는 태그를 붙이고 바로 끝나며(기본 <see cref="OnTick"/>이
    /// 곧바로 끝낸다), 끝나는 순간(<see cref="OnEnd"/>) 소멸 이벤트를 보낸다. 유닛은 <see cref="DisappearanceAbility"/>가
    /// 그 이벤트를 트리거로 받아 연출 시간 뒤에 실제로 오브젝트를 비활성화하고, 엄폐물은 그 어빌리티를 갖지 않으므로
    /// 아무도 반응하지 않는다 — 엄폐물이 부서져도 오브젝트가 사라지지 않는 것은 의도한 동작이다.
    /// </para>
    /// </remarks>
    public sealed class DeathAbility : GameplayAbility
    {
        /// <summary>죽은 상태를 나타내는 태그 이름이며, 체력 문이 재발행을 막을 때 보는 이름과 같다.</summary>
        public const string DeadStateTagName = HealthAttributeComponent.DefaultDeadStateTagName;

        /// <summary>이 어빌리티가 끝나면 보내는 소멸 트리거 이벤트 태그 이름이다.</summary>
        public const string DisappearanceEventTagName = "Event.Disappear";

        private static readonly GameplayTag DisappearanceEventTag = GameplayTag.Parse(DisappearanceEventTagName);

        /// <summary>죽은 상태 태그를 부여하는 무한 효과 정의이며 처음 활성화될 때 만들어진다.</summary>
        private GameplayEffectDefinition _deadStateEffect;

        /// <inheritdoc />
        protected override void OnActivate()
        {
            if (_deadStateEffect == null)
            {
                _deadStateEffect = GameplayEffectDefinition.CreateRuntime(
                    GameplayEffectDurationPolicy.Infinite,
                    grantedTags: new[] { DeadStateTagName });
                _deadStateEffect.name = "DeadState";
            }

            System.Effects.Apply(_deadStateEffect, this);
        }

        /// <inheritdoc />
        /// <remarks>스스로 끝났을 때만 소멸을 트리거한다. 파괴나 정리로 취소된 것은 소멸이 아니다.</remarks>
        protected override void OnEnd(GameplayAbilityEndReason endReason)
        {
            if (endReason == GameplayAbilityEndReason.Completed)
            {
                System.SendGameplayEvent(DisappearanceEventTag);
            }
        }
    }
}
