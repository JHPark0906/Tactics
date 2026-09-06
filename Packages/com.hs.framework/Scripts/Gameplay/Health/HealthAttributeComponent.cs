using System;
using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using MessagePipe;
using R3;
using UnityEngine;
using VContainer;

namespace HS.Framework.Gameplay.Health
{
    /// <summary>
    /// 어트리뷰트 집합에 담긴 체력을 가리키며, 피해가 지나는 문과 사망 알림을 어트리뷰트 변화 위에 세운다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>체력은 이 컴포넌트가 아니라 어트리뷰트 집합이 갖는다.</b> 어떤 어트리뷰트가 체력인지만 여기서 가리키고,
    /// 값을 바꾸는 길은 어트리뷰트 기본값 변경 하나로 모인다. 피해 효과가 체력을 직접 깎든, <see cref="ApplyDamage"/>가
    /// 깎든, 회복 효과가 올리든 모두 같은 자리를 지나므로 어느 길로 들어와도 아래 둘이 똑같이 일어난다.
    /// </para>
    /// <para>
    /// <b>가로채기는 필터로, 알림은 변화 관찰로.</b> 체력 어트리뷰트에 <see cref="IAttributeBaseValueFilter"/>를 등록해
    /// 체력이 줄어들기 직전에 같은 오브젝트의 <see cref="IDamageInterceptor"/>에게 차례로 묻고, 남은 몫만 통과시킨다.
    /// 어트리뷰트 변화 알림을 구독해 실제로 줄어든 만큼 <see cref="DamageAppliedEvent"/>를, 0에 닿는 순간
    /// <see cref="DeathEvent"/>를 발행한다. 사망 판정이 알림 발행자에 기대지 않으므로 발행자가 없어도 죽은 유닛이
    /// 살아 있는 것처럼 남지 않는다.
    /// </para>
    /// <para>
    /// <b>사망은 어빌리티 시스템에도 알린다.</b> 같은 오브젝트에 <see cref="GameplayAbilitySystemComponent"/>가 있으면
    /// 사망 이벤트 태그로 게임플레이 이벤트를 보내, 그 이벤트를 트리거로 적은 어빌리티(연출과 정리를 맡는 사망 어빌리티)가
    /// 활성화된다. 사망 상태 태그가 이미 있으면 다시 알리지 않는다. 죽은 뒤 회복이 잘못 들어와 0 위로 올렸다가
    /// 다시 떨어지는 경로에서 사망이 두 번 나면 집계가 두 번 세기 때문이다.
    /// </para>
    /// <para>
    /// <b>정의는 데이터가 준다.</b> 체력과 최대 체력 어트리뷰트 정의는 반드시 지정해야 하며, 집합에 아직 없으면
    /// 정의의 기본값으로 더하되 경고를 남긴다. 어트리뷰트 묶음이 갖춰 두는 것이 정상 경로이다.
    /// </para>
    /// <para>
    /// <b>피해로 세는 것은 원인이 밝혀진 감소뿐이다.</b> 최대치가 줄어 넘친 몫이 잘리는 것은 맞은 것이 아니므로
    /// 알리지 않는다. 스냅숏 복원은 피해도 사망도 아니므로 가로채지도 알리지도 않는다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AttributeSetComponent))]
    public sealed class HealthAttributeComponent : MonoBehaviour, IDamageable, IAttributeBaseValueFilter
    {
        /// <summary>사망을 알리는 게임플레이 이벤트 태그의 기본 이름이다. 게임은 이 이름을 태그 카탈로그에 등록한다.</summary>
        public const string DefaultDeathEventTagName = "Event.Death";

        /// <summary>죽은 상태를 나타내는 태그의 기본 이름이다. 이 태그가 있으면 사망을 다시 알리지 않는다.</summary>
        public const string DefaultDeadStateTagName = "State.Dead";

        [Tooltip("현재 체력을 담는 어트리뷰트 정의이다. 반드시 지정해야 한다.")]
        [SerializeField]
        private AttributeDefinition healthAttribute;

        [Tooltip("최대 체력을 담는 어트리뷰트 정의이다. 현재 체력 정의의 상한 어트리뷰트가 이 정의여야 상한이 걸린다.")]
        [SerializeField]
        private AttributeDefinition maxHealthAttribute;

        [Tooltip("체력이 0에 닿을 때 어빌리티 시스템에 보낼 게임플레이 이벤트 태그 이름이다.")]
        [SerializeField]
        private string deathEventTagName = DefaultDeathEventTagName;

        [Tooltip("이 태그를 이미 가지고 있으면 사망을 다시 알리지 않는다.")]
        [SerializeField]
        private string deadStateTagName = DefaultDeadStateTagName;

        /// <summary>가로채는 것들을 담아 재사용하는 목록이며, 매 피해마다 다시 채운다.</summary>
        private readonly List<IDamageInterceptor> _interceptors = new();

        private AttributeSetComponent _attributeSetComponent;
        private IPublisher<DamageAppliedEvent> _damagePublisher;
        private IPublisher<DeathEvent> _deathPublisher;
        private IDisposable _filterHandle;
        private IDisposable _changedSubscription;
        private bool _isBound;
        private bool _hasFailedToBind;
        private bool _hasWarnedMissingPublisher;

        /// <inheritdoc />
        public int MaxHealth => EnsureBound() ? Mathf.Max(1, Attributes.GetCurrentValueAsInt(maxHealthAttribute)) : 1;

        /// <inheritdoc />
        public int CurrentHealth => EnsureBound() ? Mathf.Clamp(Attributes.GetCurrentValueAsInt(healthAttribute), 0, MaxHealth) : 1;

        /// <inheritdoc />
        public bool IsDead => CurrentHealth <= 0;

        /// <summary>
        /// 체력 어트리뷰트에 묶였는지 여부이다.
        /// </summary>
        /// <remarks>
        /// 묶으려는 시도를 하지 않고 지금 상태만 읽으므로 <see cref="EnsureBound"/>가 남기는 오류 로그가
        /// 따라오지 않는다. 묶이기 전에는 <see cref="IsDead"/>가 항상 "살아 있음"으로 답하므로, 그 답을
        /// 사망 여부의 확정으로 쓰려는 쪽은 먼저 이 값을 봐야 한다 — 묶이지 않았다는 것은 "안 죽었다"가
        /// 아니라 "아직 모른다"는 뜻이다.
        /// </remarks>
        public bool IsBound => _isBound;

        /// <summary>현재 체력을 담는 어트리뷰트 정의이며, 효과와 조립 코드가 이 정의를 겨냥한다.</summary>
        public AttributeDefinition HealthAttribute => healthAttribute;

        /// <summary>최대 체력을 담는 어트리뷰트 정의이며 현재 체력의 상한 역할을 한다.</summary>
        public AttributeDefinition MaxHealthAttribute => maxHealthAttribute;

        /// <summary>사망을 알리는 게임플레이 이벤트 태그이며 이름이 잘못되었으면 유효하지 않은 값이다.</summary>
        public GameplayTag DeathEventTag
        {
            get
            {
                GameplayTag.TryParse(deathEventTagName, out var tag);
                return tag;
            }
        }

        /// <summary>죽은 상태를 나타내는 태그이며 이름이 잘못되었으면 유효하지 않은 값이다.</summary>
        public GameplayTag DeadStateTag
        {
            get
            {
                GameplayTag.TryParse(deadStateTagName, out var tag);
                return tag;
            }
        }

        /// <summary>이 오브젝트의 어트리뷰트 집합이다.</summary>
        private AttributeSet Attributes => _attributeSetComponent.Attributes;

        /// <summary>VContainer와 MessagePipe를 사용하는 주입 경로이다.</summary>
        /// <param name="damagePublisher">피해 알림 발행자이다.</param>
        /// <param name="deathPublisher">사망 알림 발행자이다.</param>
        [Inject]
        public void InjectMessagePipePublishers(
            IPublisher<DamageAppliedEvent> damagePublisher,
            IPublisher<DeathEvent> deathPublisher)
        {
            _damagePublisher = damagePublisher ?? throw new ArgumentNullException(nameof(damagePublisher));
            _deathPublisher = deathPublisher ?? throw new ArgumentNullException(nameof(deathPublisher));
        }

        /// <summary>
        /// 이 컴포넌트가 가리키는 어트리뷰트 정의를 코드에서 지정하고 곧바로 묶는다.
        /// 조립 코드와 검사가 쓰며, 이미 묶여 있으면 바꾸지 않는다.
        /// </summary>
        /// <remarks>
        /// 지정하는 순간 묶는 것이 중요하다. 첫 접근 때까지 미루면 그 사이에 들어온 효과가 문을 지나지 않아
        /// 가로채기도 알림도 없이 체력이 깎인다.
        /// </remarks>
        /// <param name="health">현재 체력을 담을 어트리뷰트 정의이다.</param>
        /// <param name="maxHealth">최대 체력을 담을 어트리뷰트 정의이다.</param>
        /// <returns>지정했으면 true이며, 이미 묶인 뒤라면 false이다.</returns>
        public bool ConfigureAttributes(AttributeDefinition health, AttributeDefinition maxHealth)
        {
            if (_isBound)
            {
                Debug.LogWarning($"[HealthAttributeComponent] {name}은 이미 어트리뷰트에 묶여 있어 정의를 바꿀 수 없다.", this);
                return false;
            }

            healthAttribute = health;
            maxHealthAttribute = maxHealth;
            _hasFailedToBind = false;
            EnsureBound();
            return true;
        }

        /// <inheritdoc />
        public void ApplyDamage(int amount, GameObject instigator)
        {
            if (amount <= 0 || !EnsureBound() || IsDead)
            {
                return;
            }

            Attributes.AddToBaseValue(healthAttribute, -amount, new AttributeChangeContext(this, instigator));
        }

        /// <summary>
        /// 체력을 회복한다. 회복량이 0 이하이거나 이미 사망한 상태이면 아무 변화도 일어나지 않으며,
        /// 최대 체력을 넘도록 회복되지 않는다. 넘치는 몫은 상한 어트리뷰트가 자른다.
        /// </summary>
        /// <param name="amount">회복량이다.</param>
        public void Heal(int amount)
        {
            if (amount <= 0 || !EnsureBound() || IsDead)
            {
                return;
            }

            Attributes.AddToBaseValue(healthAttribute, amount, new AttributeChangeContext(this));
        }

        /// <summary>
        /// 최대 체력을 바꾼다. 조립 코드가 데이터에서 계산한 값을 넣는 자리이며, 계산 자체는 여기서 하지 않는다.
        /// </summary>
        /// <param name="value">새 최대 체력이며 1 미만이면 1로 보정된다.</param>
        /// <param name="restoreToFull">
        /// true이면 현재 체력을 새 최대치로 되돌린다. false이면 현재 체력을 유지하되 새 최대치를 넘지 않도록 제한하며,
        /// 넘치는 몫을 잘라 내는 것은 상한 어트리뷰트의 정책이 수행한다.
        /// </param>
        public void SetMaxHealth(int value, bool restoreToFull = true)
        {
            if (!EnsureBound())
            {
                return;
            }

            var context = new AttributeChangeContext(this);
            Attributes.SetBaseValue(maxHealthAttribute, Mathf.Max(1, value), context);
            if (restoreToFull)
            {
                Attributes.SetBaseValue(healthAttribute, MaxHealth, context);
            }
        }

        /// <summary>체력을 최대치로 되돌린다. 사망 상태에서 재사용할 때 호출한다.</summary>
        public void ResetHealth()
        {
            if (EnsureBound())
            {
                Attributes.SetBaseValue(healthAttribute, MaxHealth, new AttributeChangeContext(this));
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 체력이 줄어드는 변화만 가로채기 대상이다. 스냅숏 복원은 피해가 아니므로 그대로 통과시킨다.
        /// 가로채인 몫만큼 제안값을 되돌리고, 전부 가로채이면 현재값을 돌려 변화를 없던 것으로 만든다.
        /// </remarks>
        float IAttributeBaseValueFilter.FilterBaseValue(
            AttributeDefinition definition,
            float currentBaseValue,
            float proposedBaseValue,
            in AttributeChangeContext context)
        {
            if (proposedBaseValue >= currentBaseValue || context.Cause is AttributeSetSnapshot)
            {
                return proposedBaseValue;
            }

            var amount = AttributeValue.ToInt(currentBaseValue - proposedBaseValue);
            if (amount <= 0)
            {
                return proposedBaseValue;
            }

            var taken = TakeInterceptedShare(amount, context.Instigator);
            return taken >= amount ? currentBaseValue : proposedBaseValue + taken;
        }

        /// <summary>
        /// 인스펙터에서 정의를 연결해 둔 프리팹은 시작할 때 곧바로 묶는다.
        /// 정의가 아직 없으면 묶지 않는다. 조립 코드가 나중에 지정하는 경우이며, 그때 오류를 내면 잘못된 경보가 된다.
        /// </summary>
        private void Awake()
        {
            if (healthAttribute != null && maxHealthAttribute != null)
            {
                EnsureBound();
            }
        }

        private void OnDestroy()
        {
            _filterHandle?.Dispose();
            _filterHandle = null;
            _changedSubscription?.Dispose();
            _changedSubscription = null;
        }

        /// <summary>
        /// 붙어 있는 가로채는 것들에게 차례로 물어 가져간 몫을 구한다.
        /// 매번 다시 찾는 것은 실행 중에 붙거나 떨어지는 것을 놓치지 않기 위해서이다.
        /// </summary>
        /// <param name="amount">아직 대상에게 닿지 않은 피해량이다.</param>
        /// <param name="instigator">피해를 발생시킨 GameObject이며 알 수 없으면 null이다.</param>
        /// <returns>가로채여 체력에 닿지 않는 피해량이다.</returns>
        private int TakeInterceptedShare(int amount, GameObject instigator)
        {
            GetComponents(_interceptors);
            var remaining = amount;
            for (var index = 0; index < _interceptors.Count && remaining > 0; index++)
            {
                var taken = Mathf.Clamp(_interceptors[index].InterceptDamage(remaining, instigator), 0, remaining);
                remaining -= taken;
            }

            _interceptors.Clear();
            return amount - remaining;
        }

        /// <summary>
        /// 체력 어트리뷰트에 필터와 변화 관찰을 묶는다. 여러 번 불러도 처음 한 번만 수행한다.
        /// </summary>
        /// <remarks>
        /// 수명주기 콜백이 아니라 값에 처음 접근할 때 묶으므로, Awake가 돌기 전에 최대 체력을 지정하는 조립 순서에서도 어긋나지 않는다.
        /// 정의가 지정되지 않았으면 오류를 남기고 묶지 않으며, 그때는 살아 있는 것으로 답해 조용히 사망 처리되지 않게 한다.
        /// </remarks>
        /// <returns>묶여서 어트리뷰트를 쓸 수 있으면 true이다.</returns>
        private bool EnsureBound()
        {
            if (_isBound)
            {
                return true;
            }

            if (_hasFailedToBind)
            {
                return false;
            }

            if (healthAttribute == null || maxHealthAttribute == null)
            {
                _hasFailedToBind = true;
                Debug.LogError(
                    $"[HealthAttributeComponent] {name}에 체력 어트리뷰트 정의가 지정되지 않아 체력을 다룰 수 없다. " +
                    "어트리뷰트 묶음이 주는 Health와 MaxHealth 정의를 인스펙터나 ConfigureAttributes로 지정해야 한다.",
                    this);
                return false;
            }

            _attributeSetComponent = GetComponent<AttributeSetComponent>();
            if (_attributeSetComponent == null)
            {
                _hasFailedToBind = true;
                return false;
            }

            var attributes = _attributeSetComponent.Attributes;
            EnsureAttributePresent(attributes, maxHealthAttribute);
            EnsureAttributePresent(attributes, healthAttribute);
            WarnIfCapIsNotWired();

            _filterHandle = attributes.AddBaseValueFilter(healthAttribute, this);
            _changedSubscription = attributes.Changed.Subscribe(OnAttributeChanged);
            _isBound = true;
            return true;
        }

        /// <summary>집합에 없는 어트리뷰트를 정의의 기본값으로 더하고 경고를 남긴다.</summary>
        /// <param name="attributes">대상 집합이다.</param>
        /// <param name="definition">있어야 할 어트리뷰트 정의이다.</param>
        private void EnsureAttributePresent(AttributeSet attributes, AttributeDefinition definition)
        {
            if (attributes.Contains(definition))
            {
                return;
            }

            attributes.AddAttribute(definition);
            Debug.LogWarning(
                $"[HealthAttributeComponent] {name}의 어트리뷰트 집합에 {definition.Id}가 없어 정의의 기본값으로 더했다. " +
                "어트리뷰트 묶음이 갖춰 두는 것이 정상 경로이다.",
                this);
        }

        /// <summary>현재 체력 정의가 최대 체력 정의를 상한으로 삼고 있는지 확인하고, 아니면 진단을 남긴다.</summary>
        private void WarnIfCapIsNotWired()
        {
            if (healthAttribute.CapAttribute == maxHealthAttribute)
            {
                return;
            }

            Debug.LogWarning(
                $"[HealthAttributeComponent] {name}의 현재 체력 정의({healthAttribute.Id})가 최대 체력 정의를 " +
                "상한으로 삼고 있지 않아 최대치 변화가 현재 체력에 반영되지 않는다.",
                this);
        }

        /// <summary>
        /// 체력 어트리뷰트의 변화를 받아 피해와 사망을 알린다.
        /// </summary>
        /// <param name="change">어트리뷰트 변화 알림이다.</param>
        private void OnAttributeChanged(AttributeChangedEvent change)
        {
            if (change.Definition != healthAttribute || change.Context.Cause is AttributeSetSnapshot)
            {
                return;
            }

            var previous = change.PreviousValue;
            var current = change.CurrentValue;

            // 원인이 밝혀진 감소만 피해다. 최대치가 줄어 넘친 몫이 잘리는 것은 맞은 것이 아니므로 알리지 않는다.
            if (current < previous && change.Context.HasCause)
            {
                var amount = AttributeValue.ToInt(previous) - Mathf.Clamp(AttributeValue.ToInt(current), 0, MaxHealth);
                if (amount > 0)
                {
                    Publish(_damagePublisher, new DamageAppliedEvent(gameObject, change.Context.Instigator, amount, CurrentHealth));
                }
            }

            if (previous > 0f && current <= 0f && !HasDeadStateTag())
            {
                AnnounceDeath(change.Context.Instigator);
            }
        }

        /// <summary>사망을 알림 발행자와 어빌리티 시스템에 알린다.</summary>
        /// <param name="instigator">사망을 유발한 GameObject이며 알 수 없으면 null이다.</param>
        /// <remarks>
        /// 어빌리티 시스템에 먼저 보내고 알림 발행자에게는 마지막에 보낸다. 알림을 받은 쪽이 이 오브젝트를 그 자리에서
        /// 파괴할 수 있으므로(부서지는 엄폐물이 그렇다), 발행한 뒤에는 이 컴포넌트를 다시 만지지 않는다.
        /// </remarks>
        private void AnnounceDeath(GameObject instigator)
        {
            var deathEvent = new DeathEvent(gameObject, instigator);

            var deathTag = DeathEventTag;
            if (deathTag.IsValid && TryGetComponent<GameplayAbilitySystemComponent>(out var abilitySystem))
            {
                abilitySystem.SendGameplayEvent(deathTag, deathEvent);
            }

            Publish(_deathPublisher, deathEvent);
        }

        /// <summary>죽은 상태 태그를 이미 가지고 있는지 확인한다. 태그를 담는 곳이 없으면 없는 것으로 본다.</summary>
        /// <returns>가지고 있으면 true이다.</returns>
        private bool HasDeadStateTag()
        {
            var deadTag = DeadStateTag;
            if (!deadTag.IsValid)
            {
                return false;
            }

            if (TryGetComponent<GameplayEffectComponent>(out var effectComponent))
            {
                return effectComponent.Runner.Tags.HasTag(deadTag);
            }

            return TryGetComponent<GameplayTagComponent>(out var tagComponent) && tagComponent.Container.HasTag(deadTag);
        }

        /// <summary>발행자가 있으면 알리고, 없으면 처음 한 번 오류를 남긴다. 알림이 조용히 사라지지 않게 하기 위함이다.</summary>
        /// <typeparam name="TEvent">알림의 형식이다.</typeparam>
        /// <param name="publisher">알림 발행자이며 없으면 null이다.</param>
        /// <param name="message">알릴 내용이다.</param>
        private void Publish<TEvent>(IPublisher<TEvent> publisher, TEvent message)
        {
            if (publisher != null)
            {
                publisher.Publish(message);
                return;
            }

            if (_hasWarnedMissingPublisher)
            {
                return;
            }

            _hasWarnedMissingPublisher = true;
            Debug.LogError(
                $"[HealthAttributeComponent] {name}에 알림 발행자가 주입되지 않아 피해와 사망 알림이 나가지 않는다. " +
                "스폰한 쪽이 계층에 주입했는지 확인해야 한다.",
                this);
        }
    }
}
