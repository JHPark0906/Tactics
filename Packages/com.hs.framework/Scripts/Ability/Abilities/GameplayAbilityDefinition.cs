using System.Collections.Generic;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using UnityEngine;

namespace HS.Framework.Ability.Abilities
{
    /// <summary>
    /// 어빌리티를 식별하는 태그와 활성화 조건, 코스트와 쿨다운을 담는 정의이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>행위는 파생 클래스가 정한다.</b> 이 클래스는 활성화 조건과 비용만 담고,
    /// 실제로 무엇을 하는지는 <see cref="CreateAbility"/>가 만들어 주는 <see cref="GameplayAbility"/>가 정한다.
    /// 게임은 이 에셋을 상속해 자기 어빌리티를 만든다. 효과만 적용하고 끝나는 흔한 경우에는
    /// <see cref="ApplyEffectsAbilityDefinition"/>을 그대로 쓸 수 있다.
    /// </para>
    /// <para>
    /// <b>쿨다운과 코스트에 전용 장치를 두지 않았다.</b> 쿨다운은 태그를 부여하는 지속 효과이고
    /// 코스트는 즉시 효과이다. 쿨다운 중인지는 쿨다운 효과가 부여하는 태그를 대상이 가지고 있는지로 판정하므로,
    /// 차단 태그 목록에 쿨다운 태그를 따로 적어 둘 필요가 없다. 적어 두는 것을 잊어 쿨다운이 무력해지는
    /// 흔한 사고를 구조로 막는다.
    /// </para>
    /// <para>
    /// <b>코스트는 미리 확인하고 통과할 때만 낸다.</b> 적용해 보고 모자라면 되돌리는 방식은
    /// 되돌리는 도중 다른 효과와 순서가 얽히므로 쓰지 않는다.
    /// </para>
    /// <para>
    /// <b>어빌리티끼리의 관계는 어빌리티 태그로 표현한다.</b> 식별 태그와 <see cref="AssetTags"/>가 이 어빌리티가
    /// 무엇인지를 말하고, <see cref="CancelAbilitiesWithTags"/>와 <see cref="BlockAbilitiesWithTags"/>가
    /// 그 이름을 기준으로 다른 어빌리티를 밀어내거나 막는다. 대상의 상태 태그로 표현하는 배타
    /// (활성 태그와 차단 태그)와 달리 이쪽은 어빌리티 자체를 가리키므로, "이동 계열을 전부 끊는다"처럼
    /// 상대가 어떤 상태 태그를 쓰는지 몰라도 계열 이름 하나로 표현할 수 있다.
    /// </para>
    /// <para>
    /// <b>스스로 시작하는 어빌리티는 트리거로 표현한다.</b> 게임플레이 이벤트를 받거나 특정 태그를 얻는 순간
    /// 활성화되는 어빌리티는 바깥에서 활성화 요청을 부르지 않아도 되므로, 그 조건을 정의에 적는다.
    /// 트리거로 시작하더라도 활성화 조건은 보통과 똑같이 검사된다.
    /// </para>
    /// </remarks>
    public abstract class GameplayAbilityDefinition : ScriptableObject
    {
        [Tooltip("이 어빌리티를 식별하는 태그이다. 활성화를 요청할 때 이 이름으로 찾는다. 예: Ability.Attack")]
        [SerializeField]
        private string abilityTagName;

        [Tooltip("이 어빌리티를 설명하는 태그이다. 식별 태그는 언제나 포함되므로 계열을 나타내는 태그만 더 적는다. 예: Ability.Movement")]
        [SerializeField]
        private List<string> assetTagNames = new();

        [Tooltip("활성 중인 동안 대상에게 부여할 태그이다. 다른 어빌리티가 이 태그를 차단 태그로 삼으면 서로 배타가 된다.")]
        [SerializeField]
        private List<string> activeTagNames = new();

        [Tooltip("대상이 이 태그를 모두 가지고 있어야 활성화할 수 있다. 계층 일치를 따른다.")]
        [SerializeField]
        private List<string> requiredTagNames = new();

        [Tooltip("대상이 이 태그를 하나라도 가지고 있으면 활성화할 수 없다. 계층 일치를 따른다.")]
        [SerializeField]
        private List<string> blockedTagNames = new();

        [Tooltip("활성화하는 순간 취소할 다른 어빌리티의 태그이다. 어빌리티 태그가 이 목록의 태그이거나 그 하위이면 취소된다. 예: Ability.Movement")]
        [SerializeField]
        private List<string> cancelAbilitiesWithTagNames = new();

        [Tooltip("활성 중인 동안 활성화를 막을 다른 어빌리티의 태그이다. 어빌리티 태그가 이 목록의 태그이거나 그 하위이면 막힌다.")]
        [SerializeField]
        private List<string> blockAbilitiesWithTagNames = new();

        [Tooltip("이 이벤트 태그를 받으면 활성화를 시도한다. 계층 일치를 따른다. 예: Event.Death")]
        [SerializeField]
        private List<string> triggerEventTagNames = new();

        [Tooltip("대상이 이 태그를 얻는 순간 활성화를 시도한다. 계층 일치를 따른다. 예: State.Stunned")]
        [SerializeField]
        private List<string> triggerOnTagGainedNames = new();

        [Tooltip("대상이 이 태그를 가진 동안 활성 상태를 유지한다. 얻으면 활성화하고 잃으면 취소한다. 부여 시점에 이미 가지고 있으면 곧바로 활성화한다.")]
        [SerializeField]
        private List<string> triggerWhileTagPresentNames = new();

        [Tooltip("부여되는 순간 활성화를 시도할지 여부이다. 패시브처럼 계속 도는 어빌리티에 쓴다.")]
        [SerializeField]
        private bool activateOnGrant;

        [Tooltip("활성화될 때와 끝날 때 연출 계층에 보낼 큐 태그이다. 예: Cue.Ability.Dash")]
        [SerializeField]
        private List<string> cueTagNames = new();

        [Tooltip("활성화할 때 낼 비용이다. 즉시 효과를 연결하며, 낼 수 없으면 활성화 자체가 거부된다.")]
        [SerializeField]
        private GameplayEffectDefinition costEffect;

        [Tooltip("활성화할 때 걸 쿨다운이다. 태그를 부여하는 지속 효과를 연결하면 그 태그가 남아 있는 동안 재사용이 막힌다.")]
        [SerializeField]
        private GameplayEffectDefinition cooldownEffect;

        [Tooltip("활성 상태가 이 시간(초)을 넘으면 강제로 종료한다. 0이면 제한이 없다. " +
                 "주 종료 조건은 어빌리티가 매 틱 스스로 판단하는 것이며, 이 값은 그 판단이 어긋났을 때를 대비한 그물이다.")]
        [SerializeField]
        [Min(0f)]
        private float maxActiveDuration;

        /// <summary>이름에서 해석한 태그를 담아 두는 지연 생성 캐시이다.</summary>
        private GameplayTag? _abilityTag;
        private List<GameplayTag> _assetTags;
        private List<GameplayTag> _activeTags;
        private List<GameplayTag> _requiredTags;
        private List<GameplayTag> _blockedTags;
        private List<GameplayTag> _cancelAbilitiesWithTags;
        private List<GameplayTag> _blockAbilitiesWithTags;
        private List<GameplayTag> _triggerEventTags;
        private List<GameplayTag> _triggerOnTagGained;
        private List<GameplayTag> _triggerWhileTagPresent;
        private List<GameplayTag> _cueTags;

        /// <summary>이 어빌리티를 식별하는 태그이며, 이름이 잘못되었으면 유효하지 않은 값이다.</summary>
        public GameplayTag AbilityTag
        {
            get
            {
                if (_abilityTag.HasValue)
                {
                    return _abilityTag.Value;
                }

                GameplayTag.TryParse(abilityTagName, out var tag);
                _abilityTag = tag;
                return tag;
            }
        }

        /// <summary>활성 중인 동안 부여할 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> ActiveTags => _activeTags ??= ResolveTags(activeTagNames);

        /// <summary>활성화에 필요한 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> RequiredTags => _requiredTags ??= ResolveTags(requiredTagNames);

        /// <summary>활성화를 막는 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> BlockedTags => _blockedTags ??= ResolveTags(blockedTagNames);

        /// <summary>
        /// 이 어빌리티를 설명하는 태그 목록이며 식별 태그를 언제나 포함한다.
        /// 다른 어빌리티의 취소·차단 목록과 견주는 것은 이 목록이다.
        /// </summary>
        public IReadOnlyList<GameplayTag> AssetTags => _assetTags ??= ResolveAssetTags();

        /// <summary>활성화하는 순간 취소할 다른 어빌리티의 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> CancelAbilitiesWithTags =>
            _cancelAbilitiesWithTags ??= ResolveTags(cancelAbilitiesWithTagNames);

        /// <summary>활성 중인 동안 활성화를 막을 다른 어빌리티의 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> BlockAbilitiesWithTags =>
            _blockAbilitiesWithTags ??= ResolveTags(blockAbilitiesWithTagNames);

        /// <summary>받으면 활성화를 시도할 게임플레이 이벤트 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> TriggerEventTags => _triggerEventTags ??= ResolveTags(triggerEventTagNames);

        /// <summary>대상이 얻는 순간 활성화를 시도할 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> TriggerOnTagGained => _triggerOnTagGained ??= ResolveTags(triggerOnTagGainedNames);

        /// <summary>대상이 가진 동안 활성 상태를 유지할 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> TriggerWhileTagPresent =>
            _triggerWhileTagPresent ??= ResolveTags(triggerWhileTagPresentNames);

        /// <summary>부여되는 순간 활성화를 시도하는지 여부이다.</summary>
        public bool ActivateOnGrant => activateOnGrant;

        /// <summary>활성화될 때와 끝날 때 연출 계층에 보낼 큐 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> CueTags => _cueTags ??= ResolveTags(cueTagNames);

        /// <summary>이 어빌리티가 스스로 시작하는 조건을 하나라도 가지는지 여부이다.</summary>
        public bool HasTriggers =>
            TriggerEventTags.Count > 0 || TriggerOnTagGained.Count > 0 || TriggerWhileTagPresent.Count > 0;

        /// <summary>
        /// 이 어빌리티의 태그 가운데 하나라도 지정한 태그 목록의 어느 것이거나 그 하위인지 확인한다.
        /// 취소·차단 목록이 이 어빌리티를 가리키는지 물을 때 쓴다.
        /// </summary>
        /// <param name="patterns">견줄 태그 목록이며 보통 계열을 대표하는 상위 이름이다.</param>
        /// <returns>하나라도 일치하면 true이다.</returns>
        public bool MatchesAnyAbilityTag(IEnumerable<GameplayTag> patterns)
        {
            if (patterns == null)
            {
                return false;
            }

            foreach (var pattern in patterns)
            {
                foreach (var assetTag in AssetTags)
                {
                    if (assetTag.Matches(pattern))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>활성화 비용으로 적용할 효과이며 없으면 null이다.</summary>
        public GameplayEffectDefinition CostEffect => costEffect;

        /// <summary>활성화 시 걸 쿨다운 효과이며 없으면 null이다.</summary>
        public GameplayEffectDefinition CooldownEffect => cooldownEffect;

        /// <summary>활성 상태의 최대 지속 시간(초)이며 0이면 제한이 없다.</summary>
        public float MaxActiveDuration => Mathf.Max(0f, maxActiveDuration);

        /// <summary>강제 종료 시간 제한이 설정되어 있는지 여부이다.</summary>
        public bool HasMaxActiveDuration => MaxActiveDuration > 0f;

        /// <summary>
        /// 이 정의로 실행할 어빌리티 인스턴스를 만든다.
        /// 부여할 때 한 번 호출하므로 인스턴스는 액터마다 따로 만들어진다.
        /// </summary>
        /// <returns>만든 어빌리티 인스턴스이다.</returns>
        public abstract GameplayAbility CreateAbility();

        /// <summary>
        /// 정의가 실행 가능한 형태인지 검사한다.
        /// </summary>
        /// <param name="errorMessage">검증에 실패한 첫 원인이며 성공하면 null이다.</param>
        /// <returns>문제가 없으면 true이다.</returns>
        public virtual bool TryValidate(out string errorMessage)
        {
            if (!AbilityTag.IsValid)
            {
                errorMessage = $"어빌리티 태그 '{abilityTagName}'이 유효한 형식이 아니다.";
                return false;
            }

            if (!TryValidateTagNames(assetTagNames, "어빌리티 태그", out errorMessage) ||
                !TryValidateTagNames(activeTagNames, "활성 태그", out errorMessage) ||
                !TryValidateTagNames(requiredTagNames, "필요 태그", out errorMessage) ||
                !TryValidateTagNames(blockedTagNames, "차단 태그", out errorMessage) ||
                !TryValidateTagNames(cancelAbilitiesWithTagNames, "취소 어빌리티 태그", out errorMessage) ||
                !TryValidateTagNames(blockAbilitiesWithTagNames, "차단 어빌리티 태그", out errorMessage) ||
                !TryValidateTagNames(triggerEventTagNames, "트리거 이벤트 태그", out errorMessage) ||
                !TryValidateTagNames(triggerOnTagGainedNames, "태그 획득 트리거", out errorMessage) ||
                !TryValidateTagNames(triggerWhileTagPresentNames, "태그 보유 트리거", out errorMessage) ||
                !TryValidateTagNames(cueTagNames, "큐 태그", out errorMessage))
            {
                return false;
            }

            if (costEffect != null && !costEffect.IsInstant)
            {
                errorMessage = "코스트 효과는 즉시 효과여야 한다. 남아 있는 효과를 비용으로 쓰면 되돌릴 시점이 없다.";
                return false;
            }

            if (cooldownEffect != null && cooldownEffect.GrantedTags.Count == 0)
            {
                errorMessage = "쿨다운 효과는 태그를 부여해야 한다. 부여하는 태그가 없으면 재사용을 막을 수단이 없다.";
                return false;
            }

            // 연결한 효과 자체가 잘못되어 있으면 그 효과를 거는 순간이 아니라 여기서 드러나야 한다.
            if (costEffect != null && !costEffect.TryValidate(out var costError))
            {
                errorMessage = $"코스트 효과 {costEffect.name}: {costError}";
                return false;
            }

            if (cooldownEffect != null && !cooldownEffect.TryValidate(out var cooldownError))
            {
                errorMessage = $"쿨다운 효과 {cooldownEffect.name}: {cooldownError}";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>이름 목록을 태그로 해석한다. 형식이 잘못된 이름은 건너뛴다.</summary>
        /// <param name="names">해석할 이름 목록이다.</param>
        /// <returns>해석한 태그 목록이다.</returns>
        protected static List<GameplayTag> ResolveTags(List<string> names)
        {
            var tags = new List<GameplayTag>(names.Count);
            foreach (var tagName in names)
            {
                if (GameplayTag.TryParse(tagName, out var tag))
                {
                    tags.Add(tag);
                }
            }

            return tags;
        }

        /// <summary>이름 목록이 모두 유효한 태그 형식인지 검사한다.</summary>
        /// <param name="names">검사할 이름 목록이다.</param>
        /// <param name="listLabel">진단 메시지에 쓸 목록 이름이다.</param>
        /// <param name="errorMessage">검증에 실패한 원인이며 성공하면 null이다.</param>
        /// <returns>모두 유효하면 true이다.</returns>
        protected static bool TryValidateTagNames(List<string> names, string listLabel, out string errorMessage)
        {
            foreach (var tagName in names)
            {
                if (!GameplayTag.TryParse(tagName, out _))
                {
                    errorMessage = $"{listLabel} '{tagName}'은 유효한 태그 형식이 아니다.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 필요한 값을 설정한다. 파생 클래스의 조립 메서드가 호출한다.
        /// </summary>
        /// <param name="tagName">어빌리티를 식별하는 태그 이름이다.</param>
        /// <param name="cost">활성화 비용 효과이다.</param>
        /// <param name="cooldown">쿨다운 효과이다.</param>
        /// <param name="activeTags">활성 중 부여할 태그 이름이다.</param>
        /// <param name="requiredTags">활성화에 필요한 태그 이름이다.</param>
        /// <param name="blockedTags">활성화를 막는 태그 이름이다.</param>
        /// <param name="maxDuration">활성 상태의 최대 지속 시간(초)이다.</param>
        /// <param name="assetTags">이 어빌리티를 설명하는 태그 이름이다.</param>
        /// <param name="cancelAbilitiesWithTags">활성화하는 순간 취소할 다른 어빌리티의 태그 이름이다.</param>
        /// <param name="blockAbilitiesWithTags">활성 중인 동안 막을 다른 어빌리티의 태그 이름이다.</param>
        /// <param name="triggerEventTags">받으면 활성화를 시도할 이벤트 태그 이름이다.</param>
        /// <param name="triggerOnTagGained">대상이 얻는 순간 활성화를 시도할 태그 이름이다.</param>
        /// <param name="triggerWhileTagPresent">대상이 가진 동안 활성 상태를 유지할 태그 이름이다.</param>
        /// <param name="activateOnGranted">부여되는 순간 활성화를 시도할지 여부이다.</param>
        /// <param name="cueTags">활성화될 때와 끝날 때 보낼 큐 태그 이름이다.</param>
        protected void ConfigureRuntime(
            string tagName,
            GameplayEffectDefinition cost = null,
            GameplayEffectDefinition cooldown = null,
            IEnumerable<string> activeTags = null,
            IEnumerable<string> requiredTags = null,
            IEnumerable<string> blockedTags = null,
            float maxDuration = 0f,
            IEnumerable<string> assetTags = null,
            IEnumerable<string> cancelAbilitiesWithTags = null,
            IEnumerable<string> blockAbilitiesWithTags = null,
            IEnumerable<string> triggerEventTags = null,
            IEnumerable<string> triggerOnTagGained = null,
            IEnumerable<string> triggerWhileTagPresent = null,
            bool activateOnGranted = false,
            IEnumerable<string> cueTags = null)
        {
            abilityTagName = tagName;
            costEffect = cost;
            cooldownEffect = cooldown;
            maxActiveDuration = Mathf.Max(0f, maxDuration);
            activateOnGrant = activateOnGranted;
            AddNames(assetTagNames, assetTags);
            AddNames(activeTagNames, activeTags);
            AddNames(requiredTagNames, requiredTags);
            AddNames(blockedTagNames, blockedTags);
            AddNames(cancelAbilitiesWithTagNames, cancelAbilitiesWithTags);
            AddNames(blockAbilitiesWithTagNames, blockAbilitiesWithTags);
            AddNames(triggerEventTagNames, triggerEventTags);
            AddNames(triggerOnTagGainedNames, triggerOnTagGained);
            AddNames(triggerWhileTagPresentNames, triggerWhileTagPresent);
            AddNames(cueTagNames, cueTags);
            InvalidateTagCache();
        }

        /// <summary>식별 태그 뒤에 설명 태그를 이어 붙여 어빌리티 태그 목록을 만든다.</summary>
        /// <returns>식별 태그를 앞에 둔 어빌리티 태그 목록이다.</returns>
        private List<GameplayTag> ResolveAssetTags()
        {
            var tags = new List<GameplayTag>(assetTagNames.Count + 1);
            var identity = AbilityTag;
            if (identity.IsValid)
            {
                tags.Add(identity);
            }

            foreach (var tag in ResolveTags(assetTagNames))
            {
                if (!tags.Contains(tag))
                {
                    tags.Add(tag);
                }
            }

            return tags;
        }

        /// <summary>이름 목록을 대상 목록에 더한다.</summary>
        /// <param name="target">더할 대상 목록이다.</param>
        /// <param name="names">더할 이름이며 null이면 아무것도 더하지 않는다.</param>
        private static void AddNames(List<string> target, IEnumerable<string> names)
        {
            if (names != null)
            {
                target.AddRange(names);
            }
        }

        /// <summary>태그 해석 캐시를 비운다.</summary>
        private void InvalidateTagCache()
        {
            _abilityTag = null;
            _assetTags = null;
            _activeTags = null;
            _requiredTags = null;
            _blockedTags = null;
            _cancelAbilitiesWithTags = null;
            _blockAbilitiesWithTags = null;
            _triggerEventTags = null;
            _triggerOnTagGained = null;
            _triggerWhileTagPresent = null;
            _cueTags = null;
        }

        /// <summary>에디터에서 값이 바뀌면 태그 해석 캐시를 무효화한다.</summary>
        protected virtual void OnValidate()
        {
            InvalidateTagCache();
        }
    }
}
