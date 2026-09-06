using System;
using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Abilities
{
    /// <summary>
    /// 한 액터가 무엇을 할 수 있는지 보유하고, 활성화 판정과 진행, 종료를 처리한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>여러 어빌리티가 동시에 활성화될 수 있다.</b> 프레임워크는 어떤 조합이 말이 되는지 알지 못하며,
    /// 그것은 게임마다 다르다. 동시 활성을 하나로 묶어 버리면 "이동하며 조준" 같은 조합을 표현할 수 없고,
    /// 반대로 배타가 필요한 조합은 이미 있는 태그로 표현할 수 있다.
    /// 어빌리티가 활성 중 부여하는 태그를 다른 어빌리티가 차단 태그로 삼으면 그 둘은 서로 배타가 된다.
    /// 새 장치를 만들지 않고 배타를 표현할 수 있으므로 굳이 시스템이 정책을 정하지 않는다.
    /// 다만 <b>같은 어빌리티를 두 번 겹쳐 활성화하지는 않는다</b> — 그것은 게임 규칙이 아니라
    /// 한 인스턴스가 잡은 자원을 두 번 잡게 되는 문제이기 때문이다.
    /// </para>
    /// <para>
    /// <b>시계를 갖지 않는다.</b> 효과 계층과 같은 이유로 흐른 시간을 <see cref="Tick"/>의 인자로 받는다.
    /// </para>
    /// <para>
    /// <b>어빌리티끼리의 취소와 차단은 어빌리티 태그로 푼다.</b> 활성화되는 어빌리티는 정의에 적힌 태그에 맞는
    /// 다른 활성 어빌리티를 먼저 취소하고, 활성 중인 동안에는 정의에 적힌 태그에 맞는 어빌리티의 활성화를 막는다.
    /// 차단은 부여 횟수를 세므로 같은 태그를 막는 어빌리티 둘 중 하나가 끝나도 다른 하나가 남아 있는 동안 차단이 유지된다.
    /// </para>
    /// <para>
    /// <b>어빌리티는 이벤트와 태그로도 시작된다.</b> <see cref="SendGameplayEvent(GameplayEventData)"/>로 보낸 이벤트는
    /// 그 이벤트를 트리거로 적은 어빌리티를 활성화하고, 대상이 태그를 얻거나 잃는 것도 정의의 트리거에 따라
    /// 활성화와 취소를 일으킨다. 어느 길로 시작하든 활성화 조건은 같은 검사를 거친다.
    /// </para>
    /// <para>
    /// 내부 상태를 잠금 없이 관리하므로 메인 스레드에서만 사용해야 한다.
    /// 대상의 태그 변화를 구독하므로 다 쓰면 <see cref="Dispose"/>로 놓는다.
    /// </para>
    /// </remarks>
    public sealed class GameplayAbilitySystem : IDisposable
    {
        /// <summary>효과 적용과 태그 보유를 담당하는 실행기이다.</summary>
        private readonly GameplayEffectRunner _effects;

        /// <summary>부여된 어빌리티를 식별 태그로 찾기 위한 사전이다.</summary>
        private readonly Dictionary<GameplayTag, GameplayAbility> _grantedAbilities = new();

        /// <summary>활성 중인 어빌리티 목록이다.</summary>
        private readonly List<GameplayAbility> _activeAbilities = new();

        // 알림과 사용자 콜백이 돌아오는 동안 같은 인스턴스의 수명주기를 다시 시작하지 않는다.
        private readonly HashSet<GameplayAbility> _transitioningAbilities = new();
        private readonly Dictionary<GameplayAbility, Activation> _activations = new();

        private sealed class Activation
        {
            public readonly List<GameplayTag> ActiveTags = new();
            public readonly List<GameplayTag> BlockedTags = new();
            public bool IsStarting = true;
            public bool HasStarted;
            public bool HasAppliedCues;
            public bool WasCleared;
            public GameplayAbilityEndReason? EndReason;
        }

        /// <summary>시간을 흘리는 동안 목록이 바뀌어도 안전하도록 쓰는 순회용 버퍼이다.</summary>
        private readonly List<GameplayAbility> _tickBuffer = new();

        /// <summary>활성 중인 어빌리티가 차단하고 있는 어빌리티 태그이며 부여 횟수를 센다.</summary>
        private readonly GameplayTagContainer _blockedAbilityTags = new();

        /// <summary>변화를 알리는 내부 스트림이다.</summary>
        private readonly Subject<GameplayAbilityChange> _changed = new();

        /// <summary>내부 Subject를 감춘 읽기 전용 스트림이다.</summary>
        private readonly Observable<GameplayAbilityChange> _changedObservable;

        /// <summary>받은 게임플레이 이벤트를 알리는 내부 스트림이다.</summary>
        private readonly Subject<GameplayEventData> _events = new();

        /// <summary>내부 Subject를 감춘 읽기 전용 이벤트 스트림이다.</summary>
        private readonly Observable<GameplayEventData> _eventsObservable;

        /// <summary>대상의 태그 변화를 지켜보며 태그 트리거를 돌리는 구독이다.</summary>
        private readonly IDisposable _tagSubscription;

        /// <summary>효과의 변화를 지켜보며 효과가 부여하는 어빌리티를 주고 거두는 구독이다.</summary>
        private readonly IDisposable _effectSubscription;

        /// <summary>효과별로 실제 부여한 인스턴스와 그 부여의 표식을 기억한다.</summary>
        private readonly Dictionary<ActiveGameplayEffect, List<EffectAbilityGrant>> _abilitiesGrantedByEffects = new();

        private readonly struct EffectAbilityGrant
        {
            public readonly GameplayAbility Ability;
            public readonly object Token;

            public EffectAbilityGrant(GameplayAbility ability)
            {
                Ability = ability;
                Token = ability.GrantToken;
            }
        }

        /// <summary>놓았는지 여부이다.</summary>
        private bool _isDisposed;
        private bool _isClearing;
        private bool _isTicking;
        private int _pendingClearEnds;
        private bool IsClearing => _isClearing || _pendingClearEnds > 0;

        /// <summary>어빌리티 시스템을 생성한다.</summary>
        /// <param name="effects">효과 적용과 태그 보유를 담당할 실행기이다.</param>
        /// <param name="owner">
        /// 이 어빌리티들이 몸으로 삼는 액터이며 지정하지 않으면 null이다.
        /// 세계를 만지는 어빌리티가 자기 유닛의 다른 구성요소를 찾는 경로이며,
        /// 규칙만 검증하는 자리에서는 필요 없으므로 선택 사항으로 둔다.
        /// </param>
        /// <exception cref="ArgumentNullException">실행기가 null이면 발생한다.</exception>
        public GameplayAbilitySystem(GameplayEffectRunner effects, GameObject owner = null)
        {
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            Owner = owner;
            _changedObservable = _changed.AsObservable();
            _eventsObservable = _events.AsObservable();
            _tagSubscription = _effects.Tags.Changed.Subscribe(OnTagChanged);
            _effectSubscription = _effects.Changed.Subscribe(OnEffectChanged);
        }

        /// <summary>
        /// 이 어빌리티들이 몸으로 삼는 액터이며, 지정하지 않았으면 null이다.
        /// 세계에 개입하는 어빌리티는 여기서 자기 유닛의 다른 구성요소를 찾는다.
        /// </summary>
        public GameObject Owner { get; }

        /// <summary>효과 적용과 태그 보유를 담당하는 실행기이다.</summary>
        public GameplayEffectRunner Effects => _effects;

        /// <summary>이 액터의 어트리뷰트 집합이다.</summary>
        public AttributeSet Attributes => _effects.Attributes;

        /// <summary>이 액터의 태그 컨테이너이다.</summary>
        public GameplayTagContainer Tags => _effects.Tags;

        /// <summary>어빌리티가 부여되거나 활성화되거나 종료될 때마다 흐르는 스트림이다.</summary>
        public Observable<GameplayAbilityChange> Changed => _changedObservable;

        /// <summary>
        /// 이 시스템이 받은 게임플레이 이벤트가 흐르는 스트림이다.
        /// 어빌리티가 아닌 것이 같은 이벤트에 반응해야 할 때 여기를 구독한다.
        /// </summary>
        public Observable<GameplayEventData> Events => _eventsObservable;

        /// <summary>활성 중인 어빌리티가 지금 차단하고 있는 어빌리티 태그를 열거한다.</summary>
        public IEnumerable<GameplayTag> BlockedAbilityTags => _blockedAbilityTags.Tags;

        /// <summary>부여된 어빌리티를 열거한다.</summary>
        public IEnumerable<GameplayAbility> GrantedAbilities => _grantedAbilities.Values;

        /// <summary>부여된 어빌리티 수이다.</summary>
        public int GrantedAbilityCount => _grantedAbilities.Count;

        /// <summary>활성 중인 어빌리티 목록이다.</summary>
        public IReadOnlyList<GameplayAbility> ActiveAbilities => _activeAbilities;

        /// <summary>
        /// 어빌리티를 부여한다. 같은 식별 태그가 이미 부여되어 있으면 아무것도 하지 않는다.
        /// </summary>
        /// <remarks>
        /// 정의는 부여하는 자리에서 검증한다. 검증에 실패한 정의를 그대로 들이면 지속 효과 코스트는 되돌아와 공짜가 되고,
        /// 태그 없는 쿨다운은 한 번도 막지 않는데, 둘 다 오류 없이 굴러가서 눈으로는 잡히지 않는다.
        /// 그래서 실패한 정의는 오류를 남기고 거부한다.
        /// </remarks>
        /// <param name="definition">부여할 어빌리티 정의이다.</param>
        /// <returns>만들어진 어빌리티 인스턴스이며, 부여하지 못했으면 null이다.</returns>
        public GameplayAbility GrantAbility(GameplayAbilityDefinition definition)
        {
            return GrantAbility(definition, null, null);
        }

        private GameplayAbility GrantAbility(GameplayAbilityDefinition definition, List<EffectAbilityGrant> effectGrants,
            Func<bool> canGrant)
        {
            if (_isDisposed || IsClearing || definition == null)
            {
                return null;
            }

            if (!definition.TryValidate(out var validationError))
            {
                Debug.LogError(
                    $"[GameplayAbilitySystem] {definition.name}이 검증에 실패해 부여하지 못했다: {validationError}",
                    definition);
                return null;
            }

            var abilityTag = definition.AbilityTag;
            if (!abilityTag.IsValid)
            {
                Debug.LogError($"[GameplayAbilitySystem] {definition.name}의 어빌리티 태그가 유효하지 않아 부여하지 못했다.", definition);
                return null;
            }

            if (_grantedAbilities.ContainsKey(abilityTag))
            {
                return null;
            }

            var ability = definition.CreateAbility();
            if (ability == null)
            {
                Debug.LogError($"[GameplayAbilitySystem] {definition.name}이 어빌리티 인스턴스를 만들지 못했다.", definition);
                return null;
            }

            if (_isDisposed || IsClearing || _grantedAbilities.ContainsKey(abilityTag) ||
                (canGrant != null && !canGrant()) ||
                _transitioningAbilities.Contains(ability) || _activations.ContainsKey(ability) ||
                (ability.System != null && !ReferenceEquals(ability.System, this)))
            {
                return null;
            }

            ability.Definition = definition;
            ability.System = this;
            var grantToken = new object();
            ability.GrantToken = grantToken;
            _grantedAbilities.Add(abilityTag, ability);
            // 부여 알림 안에서 원인 효과가 제거되어도 이번 인스턴스를 회수할 수 있어야 한다.
            effectGrants?.Add(new EffectAbilityGrant(ability));
            PublishChange(new GameplayAbilityChange(definition, GameplayAbilityChangeKind.Granted));

            // 부여 즉시 도는 어빌리티와, 유지 조건 태그를 이미 갖춘 어빌리티는 여기서 시작한다.
            if (ReferenceEquals(ability.GrantToken, grantToken) && IsStillGranted(ability) && (definition.ActivateOnGrant ||
                (definition.TriggerWhileTagPresent.Count > 0 && Tags.HasAny(definition.TriggerWhileTagPresent))))
            {
                Activate(ability, GameplayEventData.None);
            }

            return ability;
        }

        /// <summary>
        /// 어빌리티 부여를 취소한다. 활성 중이면 먼저 취소해 잡고 있던 것을 놓게 한다.
        /// </summary>
        /// <param name="abilityTag">취소할 어빌리티의 식별 태그이다.</param>
        /// <returns>실제로 취소했으면 true이다.</returns>
        public bool RevokeAbility(GameplayTag abilityTag)
        {
            if (!_grantedAbilities.TryGetValue(abilityTag, out var ability))
            {
                return false;
            }

            // 종료 알림에서 같은 태그를 다시 부여해도 새 인스턴스를 걷지 않도록 먼저 뺀다.
            _grantedAbilities.Remove(abilityTag);
            if (ability.IsActive)
            {
                EndAbility(ability, GameplayAbilityEndReason.Cancelled);
            }

            PublishChange(new GameplayAbilityChange(ability.Definition, GameplayAbilityChangeKind.Revoked));
            return true;
        }

        /// <summary>지정한 어빌리티가 부여되어 있는지 확인한다.</summary>
        /// <param name="abilityTag">확인할 어빌리티의 식별 태그이다.</param>
        /// <returns>부여되어 있으면 true이다.</returns>
        public bool IsGranted(GameplayTag abilityTag)
        {
            return _grantedAbilities.ContainsKey(abilityTag);
        }

        /// <summary>부여된 어빌리티를 식별 태그로 찾는다.</summary>
        /// <param name="abilityTag">찾을 어빌리티의 식별 태그이다.</param>
        /// <param name="ability">찾은 어빌리티이며 없으면 null이다.</param>
        /// <returns>부여되어 있으면 true이다.</returns>
        public bool TryGetAbility(GameplayTag abilityTag, out GameplayAbility ability)
        {
            return _grantedAbilities.TryGetValue(abilityTag, out ability);
        }

        /// <summary>지정한 어빌리티가 지금 활성 중인지 확인한다.</summary>
        /// <param name="abilityTag">확인할 어빌리티의 식별 태그이다.</param>
        /// <returns>활성 중이면 true이다.</returns>
        public bool IsActive(GameplayTag abilityTag)
        {
            return _grantedAbilities.TryGetValue(abilityTag, out var ability) && ability.IsActive;
        }

        /// <summary>
        /// 쿨다운 효과가 부여한 태그가 아직 남아 있는지 확인한다.
        /// 쿨다운 전용 상태를 따로 두지 않고 효과가 부여한 태그로 판정한다.
        /// </summary>
        /// <param name="definition">확인할 어빌리티 정의이다.</param>
        /// <returns>쿨다운이 도는 중이면 true이다.</returns>
        public bool IsOnCooldown(GameplayAbilityDefinition definition)
        {
            return definition != null &&
                   definition.CooldownEffect != null &&
                   Tags.HasAny(definition.CooldownEffect.GrantedTags);
        }

        /// <summary>
        /// 지금 이 어빌리티를 활성화할 수 있는지 판정한다. 상태를 바꾸지 않는다.
        /// </summary>
        /// <param name="abilityTag">확인할 어빌리티의 식별 태그이다.</param>
        /// <returns>활성화할 수 있으면 <see cref="GameplayAbilityActivationResult.Success"/>이다.</returns>
        public GameplayAbilityActivationResult CanActivate(GameplayTag abilityTag)
        {
            if (!_grantedAbilities.TryGetValue(abilityTag, out var ability))
            {
                return GameplayAbilityActivationResult.NotGranted;
            }

            return CanActivate(ability);
        }

        /// <summary>
        /// 어빌리티를 활성화한다.
        /// </summary>
        /// <remarks>
        /// 조건과 코스트를 확인한 뒤 코스트를 내고 쿨다운을 건다. 콜백에서 준비가 취소되거나 효과 조건이
        /// 바뀌면 실패를 돌려준다. 이미 실행한 즉시 코스트와 콜백의 변화는 되돌리지 않는다.
        /// </remarks>
        /// <param name="abilityTag">활성화할 어빌리티의 식별 태그이다.</param>
        /// <returns>활성화 결과이며, 실패해도 예외를 던지지 않는다.</returns>
        public GameplayAbilityActivationResult TryActivate(GameplayTag abilityTag)
        {
            if (!_grantedAbilities.TryGetValue(abilityTag, out var ability))
            {
                return GameplayAbilityActivationResult.NotGranted;
            }

            return Activate(ability, GameplayEventData.None);
        }

        /// <summary>
        /// 게임플레이 이벤트를 보낸다. 그 이벤트를 트리거로 적은 어빌리티가 활성화를 시도하고,
        /// <see cref="Events"/>를 구독한 쪽도 같은 이벤트를 받는다.
        /// </summary>
        /// <param name="eventTag">이벤트 종류를 나타내는 태그이다.</param>
        /// <param name="payload">이벤트가 실어 나르는 것이며 없으면 null이다.</param>
        /// <param name="magnitude">이벤트에 딸린 크기이며 없으면 0이다.</param>
        /// <returns>이 이벤트로 실제로 활성화된 어빌리티 수이다.</returns>
        public int SendGameplayEvent(GameplayTag eventTag, object payload = null, float magnitude = 0f)
        {
            return SendGameplayEvent(new GameplayEventData(eventTag, payload, magnitude));
        }

        /// <summary>
        /// 게임플레이 이벤트를 보낸다. 그 이벤트를 트리거로 적은 어빌리티가 활성화를 시도하고,
        /// <see cref="Events"/>를 구독한 쪽도 같은 이벤트를 받는다.
        /// </summary>
        /// <remarks>
        /// 트리거된 어빌리티도 보통의 활성화 검사를 그대로 거친다. 차단되거나 쿨다운 중이면 활성화되지 않으며,
        /// 그것은 이벤트를 보낸 쪽의 실패가 아니라 그 어빌리티의 조건이다. 반응한 수를 돌려주므로
        /// "아무도 반응하지 않았다"를 보낸 쪽이 알 수 있다.
        /// </remarks>
        /// <param name="eventData">보낼 이벤트이다.</param>
        /// <returns>이 이벤트로 실제로 활성화된 어빌리티 수이며, 이벤트가 유효하지 않으면 0이다.</returns>
        public int SendGameplayEvent(GameplayEventData eventData)
        {
            if (_isDisposed || !eventData.IsValid)
            {
                return 0;
            }

            _events.OnNext(eventData);

            // 반응한 어빌리티가 다른 어빌리티를 부여하거나 취소할 수 있으므로 순회 대상을 먼저 확정한다.
            var activatedCount = 0;
            var candidates = new List<GameplayAbility>(_grantedAbilities.Values);
            foreach (var ability in candidates)
            {
                var definition = ability.Definition;
                if (!MatchesAnyPattern(eventData.EventTag, definition.TriggerEventTags) ||
                    !IsStillGranted(ability))
                {
                    continue;
                }

                if (Activate(ability, eventData) == GameplayAbilityActivationResult.Success)
                {
                    activatedCount++;
                }
            }

            return activatedCount;
        }

        /// <summary>
        /// 어빌리티 태그가 지정한 태그이거나 그 하위인 활성 어빌리티를 모두 취소한다.
        /// </summary>
        /// <param name="abilityTags">취소할 어빌리티를 가리키는 태그 목록이며 보통 계열을 대표하는 상위 이름이다.</param>
        /// <returns>취소한 어빌리티 수이다.</returns>
        public int CancelAbilitiesWithTags(IEnumerable<GameplayTag> abilityTags)
        {
            return CancelAbilitiesWithTags(abilityTags, null);
        }

        /// <summary>
        /// 활성 중인 다른 어빌리티가 이 정의의 활성화를 차단하고 있는지 확인한다.
        /// </summary>
        /// <param name="definition">확인할 어빌리티 정의이다.</param>
        /// <returns>차단되어 있으면 true이다.</returns>
        public bool IsBlockedByActiveAbility(GameplayAbilityDefinition definition)
        {
            if (definition == null || _blockedAbilityTags.DistinctTagCount == 0)
            {
                return false;
            }

            return definition.MatchesAnyAbilityTag(_blockedAbilityTags.Tags);
        }

        /// <summary>
        /// 조건을 검사하고 통과하면 어빌리티를 활성화한다. 요청, 이벤트, 태그 트리거가 모두 이 길을 지난다.
        /// </summary>
        /// <param name="ability">활성화할 어빌리티이다.</param>
        /// <param name="eventData">이 활성화를 일으킨 이벤트이며, 이벤트로 시작되지 않았으면 빈 값이다.</param>
        /// <returns>활성화 결과이다.</returns>
        private GameplayAbilityActivationResult Activate(GameplayAbility ability, GameplayEventData eventData)
        {
            if (_isDisposed || IsClearing || !IsStillGranted(ability))
            {
                return GameplayAbilityActivationResult.NotGranted;
            }

            if (ability.IsActive || !_transitioningAbilities.Add(ability))
            {
                return GameplayAbilityActivationResult.AlreadyActive;
            }

            Activation activation = null;
            ActiveGameplayEffect preparedCooldown = null;
            try
            {
                var result = CanActivate(ability, reserved: true);
                if (result != GameplayAbilityActivationResult.Success)
                {
                    return result;
                }

                if (_isDisposed || IsClearing || !IsStillGranted(ability))
                {
                    return GameplayAbilityActivationResult.NotGranted;
                }

                var definition = ability.Definition;
                activation = new Activation();
                _activations.Add(ability, activation);
                ability.IsActive = true;
                ability.ActiveTime = 0f;
                ability.ActivationCount++;
                ability.TriggeringEvent = eventData;
                _activeAbilities.Add(ability);

                // 이후 모든 외부 호출은 취소나 부여 해제를 일으킬 수 있다.
                if (definition.CancelAbilitiesWithTags.Count > 0)
                {
                    CancelAbilitiesWithTags(definition.CancelAbilitiesWithTags, ability);
                }

                if (!ability.IsActive)
                {
                    return GameplayAbilityActivationResult.Cancelled;
                }

                result = CheckActivationRequirements(definition);
                if (result != GameplayAbilityActivationResult.Success)
                {
                    EndAbility(ability, GameplayAbilityEndReason.Cancelled);
                    return result;
                }

                Func<bool> canPrepare = () => ability.IsActive && IsStillGranted(ability) && !_isDisposed && !IsClearing;
                if (definition.CostEffect != null &&
                    _effects.TryApply(definition.CostEffect, out _, ability, null, canPrepare) !=
                    GameplayEffectApplicationResult.Executed)
                {
                    var failure = ability.IsActive ? GameplayAbilityActivationResult.CostApplicationFailed
                        : GameplayAbilityActivationResult.Cancelled;
                    EndAbility(ability, GameplayAbilityEndReason.Cancelled);
                    return failure;
                }

                if (definition.CooldownEffect != null)
                {
                    var cooldownResult = _effects.TryApply(definition.CooldownEffect, out preparedCooldown,
                        ability, null, canPrepare);
                    if (cooldownResult != GameplayEffectApplicationResult.Applied || preparedCooldown == null ||
                        preparedCooldown.IsInhibited || !IsOnCooldown(definition))
                    {
                        var failure = ability.IsActive ? GameplayAbilityActivationResult.CooldownApplicationFailed
                            : GameplayAbilityActivationResult.Cancelled;
                        EndAbility(ability, GameplayAbilityEndReason.Cancelled);
                        return failure;
                    }
                }

                if (ability.IsActive)
                {
                    foreach (var blockedTag in definition.BlockAbilitiesWithTags)
                    {
                        activation.BlockedTags.Add(blockedTag);
                        _blockedAbilityTags.AddTag(blockedTag);
                    }
                }

                foreach (var activeTag in definition.ActiveTags)
                {
                    if (!ability.IsActive)
                    {
                        break;
                    }

                    activation.ActiveTags.Add(activeTag);
                    Tags.AddTag(activeTag);
                }

                if (ability.IsActive)
                {
                    activation.HasStarted = true;
                    ability.OnActivate();
                    PublishChange(new GameplayAbilityChange(definition, GameplayAbilityChangeKind.Activated));
                    if (ability.IsActive)
                    {
                        activation.HasAppliedCues = true;
                        DispatchCues(definition, GameplayCueEventKind.Applied);
                    }
                }

                if (ability.IsActive && ability.OnTick(0f) == GameplayAbilityTickResult.Finished)
                {
                    EndAbility(ability, GameplayAbilityEndReason.Completed);
                }

                return activation.HasStarted ? GameplayAbilityActivationResult.Success
                    : GameplayAbilityActivationResult.Cancelled;
            }
            catch
            {
                EndAbility(ability, GameplayAbilityEndReason.Cancelled);
                throw;
            }
            finally
            {
                try
                {
                    if (activation != null)
                    {
                        List<Exception> failures = null;
                        if (!activation.HasStarted && preparedCooldown != null)
                        {
                            CaptureCleanupFailure(() => _effects.Remove(preparedCooldown), ref failures);
                        }

                        activation.IsStarting = false;
                        if (activation.EndReason.HasValue)
                        {
                            CaptureCleanupFailure(() => CompleteEnd(ability, activation), ref failures);
                        }

                        ThrowCleanupFailures(failures);
                    }
                }
                finally
                {
                    _transitioningAbilities.Remove(ability);
                }
            }
        }

        /// <summary>어빌리티 태그가 지정한 태그에 맞는 활성 어빌리티를 취소하되, 지정한 하나는 남긴다.</summary>
        /// <param name="abilityTags">취소할 어빌리티를 가리키는 태그 목록이다.</param>
        /// <param name="except">남겨 둘 어빌리티이며 없으면 null이다.</param>
        /// <returns>취소한 어빌리티 수이다.</returns>
        private int CancelAbilitiesWithTags(IEnumerable<GameplayTag> abilityTags, GameplayAbility except)
        {
            if (abilityTags == null || _activeAbilities.Count == 0)
            {
                return 0;
            }

            var cancelledCount = 0;
            var candidates = new List<GameplayAbility>(_activeAbilities);
            foreach (var candidate in candidates)
            {
                if (ReferenceEquals(candidate, except) || !candidate.Definition.MatchesAnyAbilityTag(abilityTags))
                {
                    continue;
                }

                if (EndAbility(candidate, GameplayAbilityEndReason.Cancelled))
                {
                    cancelledCount++;
                }
            }

            return cancelledCount;
        }

        /// <summary>
        /// 대상의 태그 변화에 따라 태그 트리거를 돌린다.
        /// 얻은 태그에 맞는 어빌리티는 활성화하고, 유지 조건 태그를 잃은 활성 어빌리티는 취소한다.
        /// </summary>
        /// <remarks>
        /// 태그 변경 콜백은 다른 어빌리티의 부여와 취소를 일으킬 수 있으므로 부여 목록의 스냅샷을 순회한다.
        /// </remarks>
        /// <param name="change">대상의 태그 변화이다.</param>
        private void OnTagChanged(GameplayTagChange change)
        {
            if (_isDisposed || _grantedAbilities.Count == 0)
            {
                return;
            }

            // 반응한 어빌리티가 태그를 더 바꿀 수 있으므로 순회 대상을 먼저 확정한다.
            var candidates = new List<GameplayAbility>(_grantedAbilities.Values);
            foreach (var ability in candidates)
            {
                var definition = ability.Definition;
                if (change.ChangeKind == GameplayTagChangeKind.Gained)
                {
                    if (IsStillGranted(ability) &&
                        (MatchesAnyPattern(change.Tag, definition.TriggerOnTagGained) ||
                         MatchesAnyPattern(change.Tag, definition.TriggerWhileTagPresent)))
                    {
                        Activate(ability, GameplayEventData.None);
                    }

                    continue;
                }

                // 유지 조건 태그 가운데 하나를 잃어도 다른 하나가 남아 있으면 계속 유지한다.
                if (ability.IsActive &&
                    MatchesAnyPattern(change.Tag, definition.TriggerWhileTagPresent) &&
                    !Tags.HasAny(definition.TriggerWhileTagPresent))
                {
                    EndAbility(ability, GameplayAbilityEndReason.Cancelled);
                }
            }
        }

        /// <summary>어빌리티가 아직 이 시스템에 부여되어 있는지 확인한다. 순회 중에 취소된 것을 거르는 데 쓴다.</summary>
        /// <param name="ability">확인할 어빌리티이다.</param>
        /// <returns>부여되어 있으면 true이다.</returns>
        private bool IsStillGranted(GameplayAbility ability)
        {
            return _grantedAbilities.TryGetValue(ability.Definition.AbilityTag, out var granted) &&
                   ReferenceEquals(granted, ability);
        }

        /// <summary>태그가 목록의 어느 태그이거나 그 하위인지 확인한다.</summary>
        /// <param name="tag">확인할 태그이다.</param>
        /// <param name="patterns">견줄 태그 목록이다.</param>
        /// <returns>하나라도 일치하면 true이다.</returns>
        private static bool MatchesAnyPattern(GameplayTag tag, IReadOnlyList<GameplayTag> patterns)
        {
            for (var index = 0; index < patterns.Count; index++)
            {
                if (tag.Matches(patterns[index]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 활성 중인 어빌리티를 취소한다.
        /// </summary>
        /// <param name="abilityTag">취소할 어빌리티의 식별 태그이다.</param>
        /// <returns>실제로 취소했으면 true이다.</returns>
        public bool CancelAbility(GameplayTag abilityTag)
        {
            return _grantedAbilities.TryGetValue(abilityTag, out var ability) &&
                   EndAbility(ability, GameplayAbilityEndReason.Cancelled);
        }

        /// <summary>
        /// 활성 중인 어빌리티를 모두 취소한다.
        /// </summary>
        /// <returns>취소한 어빌리티 수이다.</returns>
        public int CancelAllAbilities()
        {
            var cancelledAbilities = new List<GameplayAbility>(_activeAbilities);
            List<Exception> failures = null;
            foreach (var ability in cancelledAbilities)
            {
                CaptureCleanupFailure(() => EndAbility(ability, GameplayAbilityEndReason.Cancelled), ref failures);
            }

            ThrowCleanupFailures(failures);
            return cancelledAbilities.Count;
        }

        /// <summary>
        /// 활성 중인 어빌리티를 종료한다.
        /// </summary>
        /// <param name="ability">종료할 어빌리티이다.</param>
        /// <param name="endReason">스스로 끝났는지 취소당했는지이다.</param>
        /// <returns>실제로 종료했으면 true이다.</returns>
        public bool EndAbility(GameplayAbility ability, GameplayAbilityEndReason endReason)
        {
            if (ability == null || !ReferenceEquals(ability.System, this) || !ability.IsActive ||
                !_activations.TryGetValue(ability, out var activation))
            {
                return false;
            }

            ability.IsActive = false;
            ability.LastEndReason = endReason;
            _activeAbilities.Remove(ability);
            activation.EndReason = endReason;

            // OnActivate 안에서 취소되어도 콜백이 반환된 뒤 정리해야 이후에 잡은 자원이 새지 않는다.
            if (!activation.IsStarting)
            {
                _transitioningAbilities.Add(ability);
                try
                {
                    CompleteEnd(ability, activation);
                }
                finally
                {
                    _transitioningAbilities.Remove(ability);
                }
            }

            return true;
        }

        private void CompleteEnd(GameplayAbility ability, Activation activation)
        {
            _activations.Remove(ability);
            List<Exception> failures = null;
            try
            {
                foreach (var blockedTag in activation.BlockedTags)
                {
                    CaptureCleanupFailure(() => _blockedAbilityTags.RemoveTag(blockedTag), ref failures);
                }

                foreach (var activeTag in activation.ActiveTags)
                {
                    CaptureCleanupFailure(() => Tags.RemoveTag(activeTag), ref failures);
                }

                if (activation.HasStarted)
                {
                    var endReason = activation.EndReason.Value;
                    CaptureCleanupFailure(() => ability.OnEnd(endReason), ref failures);
                    CaptureCleanupFailure(() => PublishChange(
                        new GameplayAbilityChange(ability.Definition, GameplayAbilityChangeKind.Ended, endReason)), ref failures);
                }

                if (activation.HasAppliedCues)
                {
                    CaptureCleanupFailure(() => DispatchCues(ability.Definition, GameplayCueEventKind.Removed), ref failures);
                }
            }
            finally
            {
                if (activation.WasCleared)
                {
                    _pendingClearEnds--;
                }
            }

            ThrowCleanupFailures(failures);
        }

        private static void CaptureCleanupFailure(Action cleanup, ref List<Exception> failures)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                failures ??= new List<Exception>();
                failures.Add(exception);
            }
        }

        private static void ThrowCleanupFailures(List<Exception> failures)
        {
            if (failures != null)
            {
                throw new AggregateException("어빌리티 정리를 마쳤지만 일부 종료 콜백이 실패했다.", failures).Flatten();
            }
        }

        private void PublishChange(GameplayAbilityChange change)
        {
            if (!_isDisposed)
            {
                _changed.OnNext(change);
            }
        }

        /// <summary>
        /// 어빌리티 정의에 적힌 큐 태그를 효과 실행기가 연결한 디스패처로 보낸다. 디스패처가 없거나 큐 태그가 없으면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="definition">큐 태그를 가진 어빌리티 정의이다.</param>
        /// <param name="kind">어떤 순간인지이다.</param>
        private void DispatchCues(GameplayAbilityDefinition definition, GameplayCueEventKind kind)
        {
            var dispatcher = _effects.CueDispatcher;
            if (dispatcher == null || definition.CueTags.Count == 0)
            {
                return;
            }

            foreach (var cueTag in definition.CueTags)
            {
                dispatcher.Dispatch(new GameplayCueEvent(cueTag, kind, Owner));
            }
        }

        /// <summary>
        /// 시간을 흘려 활성 중인 어빌리티를 진행시킨다.
        /// </summary>
        /// <param name="deltaTime">흐른 시간(초)이며 0 이하이면 아무 일도 하지 않는다.</param>
        public void Tick(float deltaTime)
        {
            if (_isDisposed || _isTicking || deltaTime <= 0f || _activeAbilities.Count == 0)
            {
                return;
            }

            _isTicking = true;
            try
            {
                _tickBuffer.Clear();
                _tickBuffer.AddRange(_activeAbilities);

                foreach (var ability in _tickBuffer)
                {
                    if (!ability.IsActive || _transitioningAbilities.Contains(ability))
                    {
                        continue;
                    }

                    var activationCount = ability.ActivationCount;
                    ability.ActiveTime += deltaTime;
                    var result = ability.OnTick(deltaTime);
                    if (!ability.IsActive || ability.ActivationCount != activationCount)
                    {
                        continue;
                    }

                    if (result == GameplayAbilityTickResult.Finished)
                    {
                        EndAbility(ability, GameplayAbilityEndReason.Completed);
                        continue;
                    }

                    var definition = ability.Definition;
                    if (definition.HasMaxActiveDuration && ability.ActiveTime >= definition.MaxActiveDuration)
                    {
                        Debug.LogWarning(
                            $"[GameplayAbilitySystem] {definition.name}이 최대 활성 시간 {definition.MaxActiveDuration}초를 " +
                            "넘겨 강제로 종료했다. 어빌리티가 스스로 끝나는 조건을 확인해야 한다.",
                            definition);
                        EndAbility(ability, GameplayAbilityEndReason.Cancelled);
                    }
                }
            }
            finally
            {
                _tickBuffer.Clear();
                _isTicking = false;
            }
        }

        /// <summary>
        /// 부여된 어빌리티를 모두 정리한다. 활성 중인 것은 취소해 잡고 있던 것을 놓게 한다.
        /// </summary>
        public void Clear()
        {
            if (_isClearing)
            {
                return;
            }

            _isClearing = true;
            try
            {
                _grantedAbilities.Clear();
                _abilitiesGrantedByEffects.Clear();
                // 시작 콜백 안에서 Clear가 불리면 종료는 그 콜백 뒤에 온다. 마지막 정리까지
                // Clear의 부여 금지를 유지해 늦게 실행되는 OnEnd가 시스템을 다시 채우지 않게 한다.
                foreach (var activation in _activations.Values)
                {
                    if (!activation.WasCleared)
                    {
                        activation.WasCleared = true;
                        _pendingClearEnds++;
                    }
                }

                CancelAllAbilities();
            }
            finally
            {
                _isClearing = false;
            }
        }

        /// <summary>
        /// 부여된 어빌리티를 모두 정리하고 태그 구독과 스트림을 놓는다. 이후에는 이벤트를 보내도 아무 일도 하지 않는다.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            List<Exception> failures = null;
            CaptureCleanupFailure(Clear, ref failures);
            CaptureCleanupFailure(_tagSubscription.Dispose, ref failures);
            CaptureCleanupFailure(_effectSubscription.Dispose, ref failures);
            _abilitiesGrantedByEffects.Clear();
            CaptureCleanupFailure(_events.Dispose, ref failures);
            CaptureCleanupFailure(_changed.Dispose, ref failures);
            ThrowCleanupFailures(failures);
        }

        /// <summary>
        /// 효과의 변화에 따라 효과가 부여하는 어빌리티를 주고 거둔다.
        /// 효과가 작용하기 시작하면 부여하고, 억제되거나 걷히면 거둔다. 억제가 풀리면 다시 부여한다.
        /// </summary>
        /// <remarks>
        /// 이 시스템이 실제로 새로 부여한 것만 거둔다. 다른 데서 이미 부여되어 있던 어빌리티는 효과가 걷혀도 남는다.
        /// 부여하는 것이 소유권이며, 소유하지 않은 것을 거두면 다른 부여자의 어빌리티가 사라진다.
        /// </remarks>
        /// <param name="change">효과 실행기의 변화 알림이다.</param>
        private void OnEffectChanged(GameplayEffectChange change)
        {
            if (_isDisposed || IsClearing || change.Effect == null || change.Definition.GrantedAbilities.Count == 0)
            {
                return;
            }

            switch (change.ChangeKind)
            {
                case GameplayEffectChangeKind.Applied:
                    if (change.Effect.IsActive && !change.Effect.IsInhibited)
                    {
                        GrantAbilitiesFromEffect(change.Effect);
                    }

                    break;

                case GameplayEffectChangeKind.InhibitionChanged:
                    if (change.Effect.IsInhibited)
                    {
                        RevokeAbilitiesFromEffect(change.Effect);
                    }
                    else if (change.Effect.IsActive)
                    {
                        GrantAbilitiesFromEffect(change.Effect);
                    }

                    break;

                case GameplayEffectChangeKind.Removed:
                    RevokeAbilitiesFromEffect(change.Effect);
                    break;
            }
        }

        /// <summary>효과가 부여하는 어빌리티 가운데 아직 없는 것을 부여하고, 그것을 이 효과의 것으로 기억한다.</summary>
        /// <param name="effect">어빌리티를 부여하는 효과 기록이다.</param>
        private void GrantAbilitiesFromEffect(ActiveGameplayEffect effect)
        {
            if (!effect.IsActive || effect.IsInhibited || _abilitiesGrantedByEffects.ContainsKey(effect))
            {
                return;
            }

            var granted = new List<EffectAbilityGrant>();
            _abilitiesGrantedByEffects.Add(effect, granted);
            foreach (var definition in effect.Definition.GrantedAbilities)
            {
                if (_isDisposed || IsClearing || !effect.IsActive || effect.IsInhibited ||
                    !_abilitiesGrantedByEffects.TryGetValue(effect, out var current) || !ReferenceEquals(current, granted))
                {
                    break;
                }

                if (definition == null || IsGranted(definition.AbilityTag))
                {
                    continue;
                }

                GrantAbility(definition, granted, () => effect.IsActive && !effect.IsInhibited &&
                    _abilitiesGrantedByEffects.TryGetValue(effect, out var owner) && ReferenceEquals(owner, granted));
            }
        }

        /// <summary>효과가 부여했던 어빌리티를 거둔다. 활성 중이면 취소되어 잡고 있던 것을 놓는다.</summary>
        /// <param name="effect">어빌리티를 부여했던 효과 기록이다.</param>
        private void RevokeAbilitiesFromEffect(ActiveGameplayEffect effect)
        {
            if (!_abilitiesGrantedByEffects.TryGetValue(effect, out var granted))
            {
                return;
            }

            _abilitiesGrantedByEffects.Remove(effect);
            List<Exception> failures = null;
            foreach (var grant in granted)
            {
                var ability = grant.Ability;
                if (ReferenceEquals(ability.GrantToken, grant.Token) && IsStillGranted(ability))
                {
                    CaptureCleanupFailure(() => RevokeAbility(ability.Definition.AbilityTag), ref failures);
                }
            }

            ThrowCleanupFailures(failures);
        }

        /// <summary>부여된 어빌리티에 대해 활성화 조건을 차례로 확인한다.</summary>
        /// <param name="ability">확인할 어빌리티이다.</param>
        /// <returns>활성화할 수 있으면 <see cref="GameplayAbilityActivationResult.Success"/>이다.</returns>
        private GameplayAbilityActivationResult CanActivate(GameplayAbility ability, bool reserved = false)
        {
            if (_isDisposed || IsClearing || !IsStillGranted(ability))
            {
                return GameplayAbilityActivationResult.NotGranted;
            }

            if (ability.IsActive || (!reserved && _transitioningAbilities.Contains(ability)))
            {
                return GameplayAbilityActivationResult.AlreadyActive;
            }

            var definition = ability.Definition;
            var result = CheckActivationRequirements(definition);
            if (result != GameplayAbilityActivationResult.Success || !ability.CanActivate())
            {
                return result != GameplayAbilityActivationResult.Success ? result : GameplayAbilityActivationResult.Rejected;
            }

            if (_isDisposed || IsClearing || !IsStillGranted(ability))
            {
                return GameplayAbilityActivationResult.NotGranted;
            }

            return ability.IsActive ? GameplayAbilityActivationResult.AlreadyActive : CheckActivationRequirements(definition);
        }

        private GameplayAbilityActivationResult CheckActivationRequirements(GameplayAbilityDefinition definition)
        {
            if (IsBlockedByActiveAbility(definition))
            {
                return GameplayAbilityActivationResult.BlockedByActiveAbility;
            }

            if (!Tags.HasAll(definition.RequiredTags))
            {
                return GameplayAbilityActivationResult.MissingRequiredTags;
            }

            if (IsOnCooldown(definition))
            {
                return GameplayAbilityActivationResult.OnCooldown;
            }

            if (Tags.HasAny(definition.BlockedTags))
            {
                return GameplayAbilityActivationResult.BlockedByTags;
            }

            if (!CanAffordCost(definition.CostEffect))
            {
                return GameplayAbilityActivationResult.CostNotAffordable;
            }

            if (definition.CostEffect != null && !_effects.CanApply(definition.CostEffect))
            {
                return GameplayAbilityActivationResult.CostApplicationFailed;
            }

            if (definition.CooldownEffect != null &&
                (!_effects.CanApply(definition.CooldownEffect) || !_effects.MeetsOngoingRequirements(definition.CooldownEffect)))
            {
                return GameplayAbilityActivationResult.CooldownApplicationFailed;
            }

            return GameplayAbilityActivationResult.Success;
        }

        /// <summary>
        /// 코스트를 낼 수 있는지 미리 확인한다.
        /// </summary>
        /// <remarks>
        /// 값을 깎는 더하기 수정자만 자원 소모로 보고, 깎은 뒤에도 어트리뷰트의 하한을 지키는지 확인한다.
        /// 곱하기와 덮어쓰기는 "얼마를 낸다"는 뜻이 성립하지 않으므로 낼 수 있는 것으로 본다.
        /// 코스트는 즉시 효과라 기본값을 바꾸므로 판정도 기본값을 기준으로 한다.
        /// 대상이 갖지 않은 어트리뷰트를 깎는 코스트는 낼 수 없는 것으로 본다. 없는 자원은 모자란 자원이며,
        /// 통과시키면 그 코스트는 아무것도 깎지 않은 채 어빌리티가 공짜로 돈다.
        /// 같은 어트리뷰트를 여러 수정자가 소모하면 앞서 검사한 소모를 반영한 잔액으로 다음 것을 확인한다.
        /// </remarks>
        /// <param name="costEffect">확인할 코스트 효과이며 없으면 언제나 낼 수 있다.</param>
        /// <returns>낼 수 있으면 true이다.</returns>
        private bool CanAffordCost(GameplayEffectDefinition costEffect)
        {
            if (costEffect == null)
            {
                return true;
            }

            var remainingValues = new Dictionary<AttributeDefinition, float>();
            foreach (var modifier in costEffect.Modifiers)
            {
                // 사정에서 읽는 크기는 활성화 전에 알 수 없으므로 고정 크기의 소모만 미리 확인한다.
                if (modifier == null || !modifier.IsValid ||
                    modifier.MagnitudeSource != GameplayEffectMagnitudeSource.Scalar ||
                    modifier.Operation != AttributeModifierOperation.Add ||
                    modifier.Magnitude >= 0f)
                {
                    continue;
                }

                var attribute = modifier.Attribute;
                if (!remainingValues.TryGetValue(attribute, out var baseValue) &&
                    !Attributes.TryGetBaseValue(attribute, out baseValue))
                {
                    return false;
                }

                if (baseValue + modifier.Magnitude < attribute.MinValue)
                {
                    return false;
                }

                remainingValues[attribute] = baseValue + modifier.Magnitude;
            }

            return true;
        }
    }
}
