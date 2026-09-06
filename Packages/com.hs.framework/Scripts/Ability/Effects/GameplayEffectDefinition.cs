using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using UnityEngine;

namespace HS.Framework.Ability.Effects
{
    /// <summary>
    /// 어트리뷰트를 바꾸고 태그를 부여하는 게임플레이 효과의 정의이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>쿨다운을 위한 장치는 따로 없다.</b> 태그를 부여하는 지속 효과가 곧 쿨다운이며,
    /// 위층의 활성화 조건이 그 태그를 확인해 재사용을 막는다. 쿨다운 전용 기계를 따로 만들면
    /// 같은 일을 하는 장치가 둘이 되어 서로 어긋난다.
    /// </para>
    /// <para>
    /// <b>주기가 있는 효과는 수정자를 얹지 않는다.</b> 주기를 지정하면 주기가 돌아올 때마다
    /// 수정자 목록을 기본값에 실행한다. 초당 피해처럼 누적되어야 하는 변화이기 때문이며,
    /// 이때 수정자를 함께 얹으면 같은 값이 두 번 반영된다.
    /// 주기가 없는 지속·무한 효과만 수정자를 얹었다가 만료 시 뗀다.
    /// </para>
    /// <para>
    /// <b>부여 태그는 유지되는 효과에만 의미가 있다.</b> 즉시 효과는 남아 있지 않아 태그를 회수할 주체가 없으므로
    /// 부여 태그를 무시한다. 이 조합은 <see cref="TryValidate"/>가 문제로 보고한다.
    /// </para>
    /// <para>
    /// <b>효과끼리의 관계는 효과 태그로 표현한다.</b> <see cref="AssetTags"/>가 이 효과가 무엇인지를 말하고,
    /// <see cref="RemoveEffectsWithTags"/>가 적용하는 순간 걷어낼 효과를, <see cref="ImmunityTags"/>가 유지되는 동안
    /// 막을 효과를 그 이름으로 가리킨다. 정화가 디버프 계열을 걷어내고 무적이 피해 계열을 막는 것이 이 형태이며,
    /// 대상의 상태 태그(부여·요구·차단)와 달리 상대 효과가 어떤 상태 태그를 쓰는지 몰라도 계열 이름 하나로 표현할 수 있다.
    /// </para>
    /// <para>
    /// <b>쌓임은 규칙으로 정한다.</b> 같은 효과를 다시 적용했을 때 별개로 둘지, 대상마다 하나로 쌓을지, 건 것마다 쌓을지와
    /// 한도·지속 시간 갱신·주기 리셋·만료 시 처리가 <see cref="Stacking"/>에 있다. 쌓이는 효과는 층마다 수정자 한 벌씩 얹히고
    /// 주기 실행은 층수만큼 반복되며, 부여 태그는 한 번만 부여된다.
    /// </para>
    /// <para>
    /// <b>적용 조건과 진행 조건은 다르다.</b> 필요 태그와 차단 태그는 적용하는 순간 한 번 본다. 진행 요구 태그와 진행 차단 태그는
    /// 유지되는 동안 계속 보며, 조건이 깨지면 효과를 걷지 않고 <b>억제</b>한다. 수정자와 부여 태그를 잠시 걷되 지속 시간은 계속
    /// 흐르고, 조건이 되돌아오면 다시 얹는다. 땅에 있는 동안만 작용하는 버프, 침묵당하면 멈추는 지속 효과가 이 형태이다.
    /// </para>
    /// <para>
    /// 태그 이름은 문자열로 적되 <see cref="GameplayTag"/> 형식 검증을 거친다.
    /// 프로젝트가 어떤 태그를 쓰는지는 게임이 정하므로 프레임워크는 이름을 알지 않는다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "HS/Ability/Gameplay Effect", fileName = "GameplayEffect")]
    public class GameplayEffectDefinition : ScriptableObject
    {
        [Tooltip("효과가 얼마나 오래 남는지 정한다. 즉시는 기본값을 바꾸고, 지속과 무한은 수정자를 얹었다가 뗀다.")]
        [SerializeField]
        private GameplayEffectDurationPolicy durationPolicy = GameplayEffectDurationPolicy.Instant;

        [Tooltip("지속 정책이 Duration일 때 유지되는 시간(초)이다. 다른 정책에서는 쓰이지 않는다.")]
        [SerializeField]
        [Min(0f)]
        private float duration = 1f;

        [Tooltip("0보다 크면 이 간격(초)마다 수정자 목록을 기본값에 실행한다. 초당 피해 같은 것에 쓴다. " +
                 "주기를 지정하면 수정자를 얹지 않으므로 만료 시 되돌아가지 않는다.")]
        [SerializeField]
        [Min(0f)]
        private float period;

        [Tooltip("주기가 있는 효과를 적용하는 순간에 첫 실행을 할지 여부이다. 끄면 첫 주기가 지난 뒤부터 실행한다.")]
        [SerializeField]
        private bool executeOnApplication = true;

        [Tooltip("같은 효과를 다시 적용했을 때 쌓는 규칙이다. 즉시 효과에서는 쓰이지 않는다.")]
        [SerializeField]
        private GameplayEffectStackingSettings stacking;

        [Tooltip("이 효과가 바꾸는 어트리뷰트 목록이다.")]
        [SerializeField]
        private List<GameplayEffectModifier> modifiers = new();

        [Tooltip("이 효과가 유지되는 동안 대상에게 부여할 태그 이름이다. 즉시 효과에서는 쓰이지 않는다.")]
        [SerializeField]
        private List<string> grantedTagNames = new();

        [Tooltip("대상이 이 태그를 모두 가지고 있어야 효과를 적용한다. 계층 일치를 따른다.")]
        [SerializeField]
        private List<string> requiredTagNames = new();

        [Tooltip("대상이 이 태그를 하나라도 가지고 있으면 효과를 적용하지 않는다. 계층 일치를 따른다.")]
        [SerializeField]
        private List<string> blockedTagNames = new();

        [Tooltip("이 효과 자체를 설명하는 태그이다. 다른 효과가 이것을 보고 이 효과를 걷어내거나 막는다. 예: Effect.Debuff.Slow")]
        [SerializeField]
        private List<string> assetTagNames = new();

        [Tooltip("적용하는 순간 걷어낼 다른 효과의 태그이다. 효과 태그가 이 목록의 태그이거나 그 하위인 유지 중 효과가 제거된다. 예: Effect.Debuff")]
        [SerializeField]
        private List<string> removeEffectsWithTagNames = new();

        [Tooltip("이 효과가 유지되는 동안 적용을 막을 다른 효과의 태그이다. 효과 태그가 이 목록의 태그이거나 그 하위인 효과는 적용되지 않는다.")]
        [SerializeField]
        private List<string> immunityTagNames = new();

        [Tooltip("유지되는 동안 대상이 이 태그를 모두 가지고 있어야 효과가 작용한다. 잃으면 수정자와 부여 태그를 잠시 걷고, 되찾으면 다시 얹는다.")]
        [SerializeField]
        private List<string> ongoingRequiredTagNames = new();

        [Tooltip("유지되는 동안 대상이 이 태그를 하나라도 가지면 효과가 작용을 멈춘다. 잃으면 다시 작용한다.")]
        [SerializeField]
        private List<string> ongoingBlockedTagNames = new();

        [Tooltip("유지되는 동안 대상에게 부여할 어빌리티이다. 효과가 걷히거나 억제되면 부여도 거둔다. 즉시 효과에서는 쓰이지 않는다.")]
        [SerializeField]
        private List<GameplayAbilityDefinition> grantedAbilities = new();

        [Tooltip("이 효과가 작용하기 시작하고, 실행되고, 걷힐 때 연출 계층에 보낼 큐 태그이다. 예: Cue.Damage.Fire")]
        [SerializeField]
        private List<string> cueTagNames = new();

        /// <summary>이름 목록에서 해석한 큐 태그를 담아 두는 지연 생성 캐시이다.</summary>
        private List<GameplayTag> _cueTags;

        /// <summary>이름 목록에서 해석한 태그를 담아 두는 지연 생성 캐시이다.</summary>
        private List<GameplayTag> _grantedTags;
        private List<GameplayTag> _requiredTags;
        private List<GameplayTag> _blockedTags;
        private List<GameplayTag> _assetTags;
        private List<GameplayTag> _removeEffectsWithTags;
        private List<GameplayTag> _immunityTags;
        private List<GameplayTag> _ongoingRequiredTags;
        private List<GameplayTag> _ongoingBlockedTags;

        /// <summary>효과가 얼마나 오래 남는지 정하는 정책이다.</summary>
        public GameplayEffectDurationPolicy DurationPolicy => durationPolicy;

        /// <summary>지속 효과가 유지되는 시간(초)이다.</summary>
        public float Duration => Mathf.Max(0f, duration);

        /// <summary>주기 실행 간격(초)이며 0이면 주기가 없다.</summary>
        public float Period => Mathf.Max(0f, period);

        /// <summary>주기 실행을 사용하는지 여부이다.</summary>
        public bool HasPeriod => Period > 0f;

        /// <summary>주기가 있는 효과를 적용하는 순간에 첫 실행을 할지 여부이다.</summary>
        public bool ExecuteOnApplication => executeOnApplication;

        /// <summary>같은 효과를 다시 적용했을 때 쌓는 규칙이다.</summary>
        public GameplayEffectStackingSettings Stacking => stacking;

        /// <summary>한 번 적용하고 끝나는 효과인지 여부이다.</summary>
        public bool IsInstant => durationPolicy == GameplayEffectDurationPolicy.Instant;

        /// <summary>
        /// 수정자를 얹었다가 되돌리는 효과인지 여부이다.
        /// 주기가 있는 효과는 기본값을 실행하므로 수정자를 얹지 않는다.
        /// </summary>
        public bool UsesLingeringModifiers => !IsInstant && !HasPeriod;

        /// <summary>이 효과가 바꾸는 어트리뷰트 목록이다.</summary>
        public IReadOnlyList<GameplayEffectModifier> Modifiers => modifiers;

        /// <summary>유지되는 동안 부여할 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> GrantedTags
        {
            get
            {
                _grantedTags ??= ResolveTags(grantedTagNames);
                return _grantedTags;
            }
        }

        /// <summary>적용에 필요한 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> RequiredTags
        {
            get
            {
                _requiredTags ??= ResolveTags(requiredTagNames);
                return _requiredTags;
            }
        }

        /// <summary>적용을 막는 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> BlockedTags
        {
            get
            {
                _blockedTags ??= ResolveTags(blockedTagNames);
                return _blockedTags;
            }
        }

        /// <summary>이 효과 자체를 설명하는 태그 목록이다. 다른 효과의 제거·면역 목록과 견주는 것은 이 목록이다.</summary>
        public IReadOnlyList<GameplayTag> AssetTags => _assetTags ??= ResolveTags(assetTagNames);

        /// <summary>적용하는 순간 걷어낼 다른 효과의 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> RemoveEffectsWithTags => _removeEffectsWithTags ??= ResolveTags(removeEffectsWithTagNames);

        /// <summary>이 효과가 유지되는 동안 적용을 막을 다른 효과의 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> ImmunityTags => _immunityTags ??= ResolveTags(immunityTagNames);

        /// <summary>유지되는 동안 대상이 모두 가지고 있어야 효과가 작용하는 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> OngoingRequiredTags => _ongoingRequiredTags ??= ResolveTags(ongoingRequiredTagNames);

        /// <summary>유지되는 동안 대상이 하나라도 가지면 효과가 작용을 멈추는 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> OngoingBlockedTags => _ongoingBlockedTags ??= ResolveTags(ongoingBlockedTagNames);

        /// <summary>유지되는 동안 대상의 태그를 지켜봐야 하는 효과인지 여부이다.</summary>
        public bool HasOngoingRequirements => OngoingRequiredTags.Count > 0 || OngoingBlockedTags.Count > 0;

        /// <summary>유지되는 동안 대상에게 부여할 어빌리티 목록이다. 부여와 회수는 어빌리티 시스템이 효과 변화를 보고 한다.</summary>
        public IReadOnlyList<GameplayAbilityDefinition> GrantedAbilities => grantedAbilities;

        /// <summary>작용하기 시작하고 실행되고 걷힐 때 연출 계층에 보낼 큐 태그 목록이다.</summary>
        public IReadOnlyList<GameplayTag> CueTags => _cueTags ??= ResolveTags(cueTagNames);

        /// <summary>
        /// 이 효과의 태그 가운데 하나라도 지정한 태그 목록의 어느 것이거나 그 하위인지 확인한다.
        /// 제거·면역 목록이 이 효과를 가리키는지 물을 때 쓴다.
        /// </summary>
        /// <param name="patterns">견줄 태그 목록이며 보통 계열을 대표하는 상위 이름이다.</param>
        /// <returns>하나라도 일치하면 true이다.</returns>
        public bool MatchesAnyAssetTag(IEnumerable<GameplayTag> patterns)
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

        /// <summary>
        /// 정의가 실행 가능한 형태인지 검사한다.
        /// </summary>
        /// <param name="errorMessage">검증에 실패한 첫 원인이며 성공하면 null이다.</param>
        /// <returns>문제가 없으면 true이다.</returns>
        public virtual bool TryValidate(out string errorMessage)
        {
            for (var index = 0; index < modifiers.Count; index++)
            {
                if (modifiers[index] == null)
                {
                    errorMessage = $"{index}번 수정자가 비어 있다.";
                    return false;
                }

                if (!modifiers[index].TryValidate(out var modifierError))
                {
                    errorMessage = $"{index}번 수정자: {modifierError}";
                    return false;
                }
            }

            if (!TryValidateTagNames(grantedTagNames, "부여 태그", out errorMessage) ||
                !TryValidateTagNames(requiredTagNames, "필요 태그", out errorMessage) ||
                !TryValidateTagNames(blockedTagNames, "차단 태그", out errorMessage) ||
                !TryValidateTagNames(assetTagNames, "효과 태그", out errorMessage) ||
                !TryValidateTagNames(removeEffectsWithTagNames, "제거 효과 태그", out errorMessage) ||
                !TryValidateTagNames(immunityTagNames, "면역 태그", out errorMessage) ||
                !TryValidateTagNames(ongoingRequiredTagNames, "진행 요구 태그", out errorMessage) ||
                !TryValidateTagNames(ongoingBlockedTagNames, "진행 차단 태그", out errorMessage) ||
                !TryValidateTagNames(cueTagNames, "큐 태그", out errorMessage))
            {
                return false;
            }

            if (IsInstant && immunityTagNames.Count > 0)
            {
                errorMessage = "즉시 효과는 남아 있지 않아 면역을 유지할 주체가 없으므로 면역 태그를 가질 수 없다.";
                return false;
            }

            if (IsInstant && (ongoingRequiredTagNames.Count > 0 || ongoingBlockedTagNames.Count > 0))
            {
                errorMessage = "즉시 효과는 남아 있지 않으므로 진행 조건을 가질 수 없다. 적용 조건은 필요 태그와 차단 태그로 적는다.";
                return false;
            }

            for (var index = 0; index < grantedAbilities.Count; index++)
            {
                if (grantedAbilities[index] == null)
                {
                    errorMessage = $"{index}번 부여 어빌리티가 비어 있다.";
                    return false;
                }

                if (!grantedAbilities[index].TryValidate(out var abilityError))
                {
                    errorMessage = $"부여 어빌리티 {grantedAbilities[index].name}: {abilityError}";
                    return false;
                }
            }

            if (IsInstant && grantedAbilities.Count > 0)
            {
                errorMessage = "즉시 효과는 남아 있지 않아 부여한 어빌리티를 거둘 주체가 없으므로 어빌리티를 부여할 수 없다.";
                return false;
            }

            if (IsInstant && grantedTagNames.Count > 0)
            {
                errorMessage = "즉시 효과는 남아 있지 않아 부여한 태그를 회수할 수 없으므로 부여 태그를 가질 수 없다.";
                return false;
            }

            if (IsInstant && HasPeriod)
            {
                errorMessage = "즉시 효과는 한 번만 실행되므로 주기를 가질 수 없다.";
                return false;
            }

            if (IsInstant && stacking.IsStacking)
            {
                errorMessage = "즉시 효과는 남아 있지 않으므로 쌓을 수 없다.";
                return false;
            }

            if (durationPolicy == GameplayEffectDurationPolicy.Duration && Duration <= 0f)
            {
                errorMessage = "지속 효과의 지속 시간은 0보다 커야 한다.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 효과 정의를 만든다.
        /// </summary>
        /// <param name="durationPolicy">지속 정책이다.</param>
        /// <param name="effectModifiers">바꿀 어트리뷰트 목록이다.</param>
        /// <param name="duration">지속 시간(초)이다.</param>
        /// <param name="period">주기 실행 간격(초)이며 0이면 주기가 없다.</param>
        /// <param name="grantedTags">유지되는 동안 부여할 태그 이름이다.</param>
        /// <param name="requiredTags">적용에 필요한 태그 이름이다.</param>
        /// <param name="blockedTags">적용을 막는 태그 이름이다.</param>
        /// <returns>만든 효과 정의이다.</returns>
        public static GameplayEffectDefinition CreateRuntime(
            GameplayEffectDurationPolicy durationPolicy,
            IEnumerable<GameplayEffectModifier> effectModifiers = null,
            float duration = 0f,
            float period = 0f,
            IEnumerable<string> grantedTags = null,
            IEnumerable<string> requiredTags = null,
            IEnumerable<string> blockedTags = null,
            IEnumerable<string> assetTags = null,
            IEnumerable<string> removeEffectsWithTags = null,
            IEnumerable<string> immunityTags = null,
            GameplayEffectStackingSettings stacking = default,
            IEnumerable<string> ongoingRequiredTags = null,
            IEnumerable<string> ongoingBlockedTags = null,
            IEnumerable<GameplayAbilityDefinition> abilities = null,
            IEnumerable<string> cueTags = null)
        {
            var definition = CreateInstance<GameplayEffectDefinition>();
            definition.ConfigureRuntime(
                durationPolicy,
                effectModifiers,
                duration,
                period,
                grantedTags,
                requiredTags,
                blockedTags,
                assetTags,
                removeEffectsWithTags,
                immunityTags,
                stacking,
                ongoingRequiredTags,
                ongoingBlockedTags,
                abilities,
                cueTags);
            return definition;
        }

        /// <summary>
        /// 이 정의의 내용을 코드에서 정한다. 파생 클래스가 에셋을 처음 만들 때 모양을 미리 채우거나
        /// 테스트용 정의를 조립할 때 호출한다. 이전 수정자와 태그 이름은 지우고 새로 채운다.
        /// </summary>
        /// <param name="policy">지속 정책이다.</param>
        /// <param name="effectModifiers">바꿀 어트리뷰트 목록이다.</param>
        /// <param name="effectDuration">지속 시간(초)이다.</param>
        /// <param name="effectPeriod">주기 실행 간격(초)이며 0이면 주기가 없다.</param>
        /// <param name="grantedTags">유지되는 동안 부여할 태그 이름이다.</param>
        /// <param name="requiredTags">적용에 필요한 태그 이름이다.</param>
        /// <param name="blockedTags">적용을 막는 태그 이름이다.</param>
        /// <param name="assetTags">이 효과 자체를 설명하는 태그 이름이다.</param>
        /// <param name="removeEffectsWithTags">적용하는 순간 걷어낼 다른 효과의 태그 이름이다.</param>
        /// <param name="immunityTags">유지되는 동안 적용을 막을 다른 효과의 태그 이름이다.</param>
        /// <param name="stackingSettings">같은 효과를 다시 적용했을 때 쌓는 규칙이다.</param>
        /// <param name="ongoingRequiredTags">유지되는 동안 대상이 모두 가지고 있어야 작용하는 태그 이름이다.</param>
        /// <param name="ongoingBlockedTags">유지되는 동안 대상이 하나라도 가지면 작용을 멈추는 태그 이름이다.</param>
        /// <param name="abilities">유지되는 동안 대상에게 부여할 어빌리티이다.</param>
        /// <param name="cueTags">작용하기 시작하고 실행되고 걷힐 때 보낼 큐 태그 이름이다.</param>
        protected void ConfigureRuntime(
            GameplayEffectDurationPolicy policy,
            IEnumerable<GameplayEffectModifier> effectModifiers = null,
            float effectDuration = 0f,
            float effectPeriod = 0f,
            IEnumerable<string> grantedTags = null,
            IEnumerable<string> requiredTags = null,
            IEnumerable<string> blockedTags = null,
            IEnumerable<string> assetTags = null,
            IEnumerable<string> removeEffectsWithTags = null,
            IEnumerable<string> immunityTags = null,
            GameplayEffectStackingSettings stackingSettings = default,
            IEnumerable<string> ongoingRequiredTags = null,
            IEnumerable<string> ongoingBlockedTags = null,
            IEnumerable<GameplayAbilityDefinition> abilities = null,
            IEnumerable<string> cueTags = null)
        {
            durationPolicy = policy;
            duration = Mathf.Max(0f, effectDuration);
            period = Mathf.Max(0f, effectPeriod);
            stacking = stackingSettings;
            modifiers.Clear();
            if (effectModifiers != null)
            {
                modifiers.AddRange(effectModifiers);
            }

            grantedAbilities.Clear();
            if (abilities != null)
            {
                grantedAbilities.AddRange(abilities);
            }

            grantedTagNames.Clear();
            requiredTagNames.Clear();
            blockedTagNames.Clear();
            assetTagNames.Clear();
            removeEffectsWithTagNames.Clear();
            immunityTagNames.Clear();
            ongoingRequiredTagNames.Clear();
            ongoingBlockedTagNames.Clear();
            cueTagNames.Clear();
            AddNames(grantedTagNames, grantedTags);
            AddNames(requiredTagNames, requiredTags);
            AddNames(blockedTagNames, blockedTags);
            AddNames(assetTagNames, assetTags);
            AddNames(removeEffectsWithTagNames, removeEffectsWithTags);
            AddNames(immunityTagNames, immunityTags);
            AddNames(ongoingRequiredTagNames, ongoingRequiredTags);
            AddNames(ongoingBlockedTagNames, ongoingBlockedTags);
            AddNames(cueTagNames, cueTags);
            InvalidateTagCache();
        }

        /// <summary>태그 해석 캐시를 비운다.</summary>
        private void InvalidateTagCache()
        {
            _grantedTags = null;
            _requiredTags = null;
            _blockedTags = null;
            _assetTags = null;
            _removeEffectsWithTags = null;
            _immunityTags = null;
            _ongoingRequiredTags = null;
            _ongoingBlockedTags = null;
            _cueTags = null;
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

        /// <summary>이름 목록을 태그로 해석한다. 형식이 잘못된 이름은 건너뛴다.</summary>
        /// <param name="names">해석할 이름 목록이다.</param>
        /// <returns>해석한 태그 목록이다.</returns>
        private static List<GameplayTag> ResolveTags(List<string> names)
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
        private static bool TryValidateTagNames(List<string> names, string listLabel, out string errorMessage)
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

        /// <summary>에디터에서 목록이 바뀌면 태그 해석 캐시를 무효화한다.</summary>
        protected virtual void OnValidate()
        {
            InvalidateTagCache();
        }
    }
}
