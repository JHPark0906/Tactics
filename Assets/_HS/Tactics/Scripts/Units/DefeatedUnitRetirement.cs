using System;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using HS.Tactics.Cover;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Units
{
    /// <summary>
    /// 자기 유닛의 사망을 어빌리티에 전달하고, 사망 어빌리티가 없으면 직접 전장에서 물러나게 한다.
    /// 죽은 상태 태그가 있으면 어빌리티가 이미 처리한 것으로 본다. 없으면 사망 이벤트를 보내고 태그를 다시 확인한다.
    /// 어빌리티로 처리할 수 없으면 경고를 남기고 엄폐 예약을 명시적으로 해제한 뒤 오브젝트를 비활성화한다.
    /// 이 폴백 경로는 즉시 비활성화하므로 소멸 어빌리티의 다음 고정 스텝 지연과 같은 승패 구독 순서 보장은 없다.
    /// TacticalUnit의 요구 구성요소로 부착되며 사망 이벤트 구독은 주입과 활성 수명에 따른다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefeatedUnitRetirement : MonoBehaviour
    {
        /// <summary>사망 어빌리티의 식별 태그이다.</summary>
        private static readonly GameplayTag DeathAbilityTag = GameplayTag.Parse(UnitAbilityTags.Death);

        /// <summary>사망을 알리는 게임플레이 이벤트 태그이다.</summary>
        private static readonly GameplayTag DeathEventTag =
            GameplayTag.Parse(HealthAttributeComponent.DefaultDeathEventTagName);

        /// <summary>
        /// 죽은 상태를 나타내는 태그이다. 사망 어빌리티는 죽은 상태 태그만 붙이고 곧바로 끝나므로, 어빌리티
        /// 시스템이 이 죽음을 이미 처리했는지는 어빌리티가 아니라 이 태그로 확인한다.
        /// </summary>
        private static readonly GameplayTag DeadStateTag =
            GameplayTag.Parse(HealthAttributeComponent.DefaultDeadStateTagName);

        private ISubscriber<DeathEvent> _deathSubscriber;
        private IDisposable _deathSubscription;
        private bool _hasWarnedMissingDeathAbility;

        /// <summary>사망 알림 구독자를 주입받고, 활성 상태면 곧바로 구독한다.</summary>
        /// <param name="deathSubscriber">프레임워크 사망 알림의 구독자이다.</param>
        [Inject]
        public void InjectMessagePipeDependencies(ISubscriber<DeathEvent> deathSubscriber)
        {
            _deathSubscriber = deathSubscriber ?? throw new ArgumentNullException(nameof(deathSubscriber));
            RefreshSubscription();
        }

        private void OnEnable()
        {
            RefreshSubscription();
        }

        private void OnDisable()
        {
            DetachSubscription();
        }

        private void OnDestroy()
        {
            DetachSubscription();
            _deathSubscriber = null;
        }

        /// <summary>
        /// 구독자가 준비되고 활성 상태일 때만 사망 알림을 구독한다.
        /// 주입이 여러 번 도착해도 구독이 쌓이지 않도록 기존 구독을 먼저 놓는다.
        /// </summary>
        private void RefreshSubscription()
        {
            DetachSubscription();
            if (!isActiveAndEnabled)
            {
                return;
            }

            _deathSubscription = _deathSubscriber?.Subscribe(OnDeath);
        }

        /// <summary>부착된 사망 알림 구독을 놓는다.</summary>
        private void DetachSubscription()
        {
            _deathSubscription?.Dispose();
            _deathSubscription = null;
        }

        /// <summary>
        /// 사망 알림이 자기 것이면 사망 어빌리티에 넘기고, 없으면 곧바로 전장에서 물러난다.
        /// </summary>
        /// <remarks>
        /// 곧바로 물러나는 경로에서는 비활성화하는 순간 이 구성요소의 <c>OnDisable</c>이 불려 구독을 놓는다.
        /// 알림을 돌리는 도중에 구독이 사라지는 셈이지만, 이벤트 버스가 R3 Subject 위에 있어
        /// 그 상황을 다루도록 되어 있고 다른 구독자에게는 알림이 그대로 간다.
        /// </remarks>
        /// <param name="deathEvent">프레임워크가 발행한 사망 알림이다.</param>
        private void OnDeath(DeathEvent deathEvent)
        {
            if (deathEvent.Target != gameObject)
            {
                return;
            }

            if (TryHandOffToDeathAbility(deathEvent))
            {
                return;
            }

            WarnOnceAboutMissingDeathAbility();
            ReleaseHeldClaims();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 사망 어빌리티가 부여되어 있으면 사망 이벤트를 넘겨 활성화하고, 이미 처리됐으면 그대로 둔다.
        /// </summary>
        /// <remarks>
        /// 사망 어빌리티는 죽은 상태 태그만 붙이고 곧바로 끝나므로(활성 상태가 남는 것은 뒤이어 트리거되는
        /// 소멸 어빌리티다), 이 죽음을 어빌리티 시스템이 이미 처리했는지는 <see cref="DeathAbilityTag"/>의
        /// 활성 여부가 아니라 죽은 상태 태그로 확인한다.
        /// </remarks>
        /// <param name="deathEvent">넘길 사망 알림이다.</param>
        /// <returns>사망 어빌리티가 물러남을 맡았으면 true이다.</returns>
        private bool TryHandOffToDeathAbility(DeathEvent deathEvent)
        {
            if (!TryGetComponent<GameplayAbilitySystemComponent>(out var abilitySystem))
            {
                return false;
            }

            var system = abilitySystem.System;
            if (!system.IsGranted(DeathAbilityTag))
            {
                return false;
            }

            if (!system.Tags.HasTag(DeadStateTag))
            {
                system.SendGameplayEvent(DeathEventTag, deathEvent);
            }

            return system.Tags.HasTag(DeadStateTag);
        }

        /// <summary>사망 어빌리티 없이 물러나는 경로임을 처음 한 번 알린다.</summary>
        private void WarnOnceAboutMissingDeathAbility()
        {
            if (_hasWarnedMissingDeathAbility)
            {
                return;
            }

            _hasWarnedMissingDeathAbility = true;
            Debug.LogWarning(
                $"[DefeatedUnitRetirement] {name}에 사망 어빌리티가 부여되지 않아 곧바로 물러난다. " +
                $"어빌리티 집합에 {UnitAbilityTags.Death}를 넣으면 연출과 정리를 어빌리티가 맡는다.",
                this);
        }

        /// <summary>
        /// 물러나기 전에 이 유닛이 잡고 있던 것을 놓는다.
        /// </summary>
        /// <remarks>
        /// 지금은 엄폐 예약 하나뿐이다. 잡고 있는 것이 늘면 여기에 함께 적어,
        /// 무엇이 언제 풀리는지가 이 한 곳에서 읽히게 한다.
        /// </remarks>
        private void ReleaseHeldClaims()
        {
            if (TryGetComponent<UnitCoverState>(out var coverState))
            {
                coverState.ReleaseCover();
            }
        }
    }
}
