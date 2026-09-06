using System;
using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Tags;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Effects
{
    /// <summary>
    /// 한 대상의 어트리뷰트와 태그에 게임플레이 효과를 적용하고, 시간을 흘려 만료와 주기 실행을 처리한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>시계를 갖지 않는다.</b> 흐른 시간을 <see cref="Tick"/>의 인자로 받으므로 이 클래스는 시간을 읽지 않는다.
    /// 덕분에 테스트가 시간을 원하는 만큼 정확히 흘릴 수 있고, 실제 게임에서는 얇은 컴포넌트가
    /// <c>Time.deltaTime</c>을 넣어 준다. 시간 제공자를 인자로 받는 프로젝트 관례와 같은 목적이며,
    /// 효과는 경과 시간만 필요해 제공자보다 더 단순한 형태로 충분하다.
    /// </para>
    /// <para>
    /// <b>즉시와 지속의 처리가 다르다.</b> 즉시 효과는 어트리뷰트의 기본값을 바꾸고 기록을 남기지 않는다.
    /// 지속과 무한 효과는 수정자를 얹고 기록을 남겼다가, 만료나 제거 시 그 기록을 출처로 삼아 한꺼번에 걷어낸다.
    /// 주기가 있는 효과는 수정자를 얹지 않고 주기마다 기본값을 실행한다.
    /// </para>
    /// <para>
    /// <b>부여한 태그도 함께 되돌린다.</b> 태그 컨테이너가 부여 횟수를 세므로, 같은 태그를 부여한 효과가 둘일 때
    /// 하나가 만료돼도 다른 하나가 남아 있는 동안에는 태그가 유지된다.
    /// 쿨다운은 이 성질만으로 성립하므로 별도의 장치를 두지 않는다.
    /// </para>
    /// <para>
    /// <b>진행 조건이 깨진 효과는 걷지 않고 억제한다.</b> 대상의 태그가 바뀔 때마다 진행 요구·차단 태그를 가진 효과를 다시 보고,
    /// 조건이 깨지면 수정자와 부여 태그를 잠시 걷되 기록과 지속 시간은 그대로 둔다. 조건이 되돌아오면 다시 얹는다.
    /// 그래서 대상의 태그 변화를 구독하며, 다 쓰면 <see cref="Dispose"/>로 놓는다.
    /// </para>
    /// <para>
    /// 내부 상태를 잠금 없이 관리하므로 메인 스레드에서만 사용해야 한다.
    /// </para>
    /// </remarks>
    public sealed class GameplayEffectRunner : IDisposable
    {
        /// <summary>
        /// 주기 실행 간격의 하한(초)이다.
        /// 실수로 0에 가까운 주기를 지정해도 한 번의 <see cref="Tick"/>에서 실행 횟수가 폭주하지 않게 막는다.
        /// </summary>
        public const float MinimumPeriod = 0.001f;

        /// <summary>효과가 값을 바꾸는 대상 어트리뷰트 집합이다.</summary>
        private readonly AttributeSet _attributes;

        /// <summary>효과가 태그를 부여하는 대상 컨테이너이다.</summary>
        private readonly GameplayTagContainer _tags;

        /// <summary>유지되고 있는 효과 목록이다.</summary>
        private readonly List<ActiveGameplayEffect> _activeEffects = new();

        /// <summary>시간을 흘리는 동안 목록이 바뀌어도 안전하도록 쓰는 순회용 버퍼이다.</summary>
        private readonly List<ActiveGameplayEffect> _tickBuffer = new();

        /// <summary>변화를 알리는 내부 스트림이다.</summary>
        private readonly Subject<GameplayEffectChange> _changed = new();

        /// <summary>내부 Subject를 감춘 읽기 전용 스트림이다.</summary>
        private readonly Observable<GameplayEffectChange> _changedObservable;

        /// <summary>대상의 태그 변화를 지켜보며 진행 조건을 다시 보는 구독이다.</summary>
        private readonly IDisposable _tagSubscription;

        /// <summary>진행 조건을 다시 보는 중인지 여부이며, 그 안에서 일어난 태그 변화는 끝난 뒤 한 번 더 본다.</summary>
        private bool _isRefreshingInhibition;

        /// <summary>진행 조건을 다시 보는 중에 태그가 또 바뀌었는지 여부이다.</summary>
        private bool _isInhibitionDirty;

        /// <summary>놓았는지 여부이다.</summary>
        private bool _isDisposed;
        private bool _notificationsClosed;
        private bool _isTicking;
        private int _resourceChangeDepth;
        private int _removalDepth;

        /// <summary>효과 실행기를 생성한다.</summary>
        /// <param name="attributes">효과가 값을 바꿀 어트리뷰트 집합이다.</param>
        /// <param name="tags">효과가 태그를 부여할 컨테이너이며, 지정하지 않으면 새로 만든다.</param>
        /// <exception cref="ArgumentNullException">어트리뷰트 집합이 null이면 발생한다.</exception>
        public GameplayEffectRunner(AttributeSet attributes, GameplayTagContainer tags = null)
        {
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _tags = tags ?? new GameplayTagContainer();
            _changedObservable = _changed.AsObservable();
            _tagSubscription = _tags.Changed.Subscribe(_ => RefreshInhibition());
        }

        /// <summary>
        /// 유지 중인 효과를 모두 제거하고 태그 구독과 스트림을 놓는다. 이후에는 아무것도 적용하지 않는다.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            List<Exception> failures = null;
            CaptureCleanupFailure(() => RemoveAll(), ref failures);
            CaptureCleanupFailure(_tagSubscription.Dispose, ref failures);
            if (_removalDepth == 0)
            {
                CaptureCleanupFailure(CloseNotifications, ref failures);
            }
            ThrowCleanupFailures(failures);
        }

        /// <summary>효과가 값을 바꾸는 어트리뷰트 집합이다.</summary>
        public AttributeSet Attributes => _attributes;

        /// <summary>효과가 태그를 부여하는 컨테이너이다.</summary>
        public GameplayTagContainer Tags => _tags;

        /// <summary>
        /// 연출 계층으로 큐를 보낼 디스패처이며 없으면 큐를 보내지 않는다. 조립하는 쪽이 액터마다 연결한다.
        /// </summary>
        public GameplayCueDispatcher CueDispatcher { get; set; }

        /// <summary>큐에 실을 이 실행기의 액터이며 규칙만 검증하는 자리에서는 null이다.</summary>
        public GameObject CueTarget { get; set; }

        /// <summary>효과 정의에 적힌 큐 태그를 모두 디스패처로 보낸다. 디스패처가 없거나 큐 태그가 없으면 아무것도 하지 않는다.</summary>
        /// <param name="definition">큐 태그를 가진 효과 정의이다.</param>
        /// <param name="kind">어떤 순간인지이다.</param>
        /// <param name="effect">큐를 일으킨 효과 기록이며 즉시 효과이면 null이다.</param>
        /// <param name="context">큐를 일으킨 순간의 사정이며 없으면 null이다.</param>
        private void DispatchCues(
            GameplayEffectDefinition definition,
            GameplayCueEventKind kind,
            ActiveGameplayEffect effect,
            GameplayEffectContext context,
            Func<bool> canContinue = null)
        {
            var dispatcher = CueDispatcher;
            if (dispatcher == null || definition.CueTags.Count == 0)
            {
                return;
            }

            var cueTags = kind == GameplayCueEventKind.Removed && effect != null
                ? new List<GameplayTag>(effect.AppliedCueTags) : definition.CueTags;
            if (kind == GameplayCueEventKind.Removed && effect != null)
            {
                effect.AppliedCueTags.Clear();
            }

            List<Exception> failures = null;
            foreach (var cueTag in cueTags)
            {
                if (kind != GameplayCueEventKind.Removed &&
                    (!CanContinue(canContinue) || (effect != null && (!effect.IsActive || effect.IsInhibited))))
                {
                    break;
                }

                if (kind == GameplayCueEventKind.Applied && effect != null)
                {
                    effect.AppliedCueTags.Add(cueTag);
                }

                if (kind == GameplayCueEventKind.Removed)
                {
                    CaptureCleanupFailure(() => dispatcher.Dispatch(
                        new GameplayCueEvent(cueTag, kind, CueTarget, effect, context)), ref failures);
                }
                else
                {
                    dispatcher.Dispatch(new GameplayCueEvent(cueTag, kind, CueTarget, effect, context));
                }
            }

            ThrowCleanupFailures(failures);
        }

        /// <summary>효과가 적용되거나 실행되거나 제거될 때마다 흐르는 스트림이다.</summary>
        public Observable<GameplayEffectChange> Changed => _changedObservable;

        /// <summary>유지되고 있는 효과 목록이다.</summary>
        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects => _activeEffects;

        /// <summary>유지되고 있는 효과 수이다.</summary>
        public int ActiveEffectCount => _activeEffects.Count;

        /// <summary>
        /// 대상의 상태만 보고 이 효과를 적용할 수 있는지 확인한다.
        /// 필요 태그를 모두 가지고 있고 차단 태그를 하나도 가지고 있지 않으며, 유지 중인 효과가 면역을 걸어 두지 않았어야 한다.
        /// </summary>
        /// <param name="definition">확인할 효과 정의이다.</param>
        /// <returns>적용할 수 있으면 true이다.</returns>
        public bool CanApply(GameplayEffectDefinition definition)
        {
            if (_isDisposed || definition == null)
            {
                return false;
            }

            return _tags.HasAll(definition.RequiredTags) && !_tags.HasAny(definition.BlockedTags) && !IsImmuneTo(definition);
        }

        /// <summary>
        /// 유지 중인 효과 가운데 하나라도 이 효과의 태그에 면역을 걸어 두었는지 확인한다.
        /// </summary>
        /// <param name="definition">확인할 효과 정의이다.</param>
        /// <returns>면역이면 true이다.</returns>
        public bool IsImmuneTo(GameplayEffectDefinition definition)
        {
            if (definition == null || definition.AssetTags.Count == 0)
            {
                return false;
            }

            for (var index = 0; index < _activeEffects.Count; index++)
            {
                var immunityTags = _activeEffects[index].Definition.ImmunityTags;
                if (immunityTags.Count > 0 && definition.MatchesAnyAssetTag(immunityTags))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 효과 태그가 지정한 태그이거나 그 하위인 효과가 유지 중인지 확인한다.
        /// </summary>
        /// <param name="assetTags">찾을 효과를 가리키는 태그 목록이며 보통 계열을 대표하는 상위 이름이다.</param>
        /// <returns>하나라도 유지 중이면 true이다.</returns>
        public bool HasActiveEffectWithTags(IEnumerable<GameplayTag> assetTags)
        {
            if (assetTags == null)
            {
                return false;
            }

            for (var index = 0; index < _activeEffects.Count; index++)
            {
                if (_activeEffects[index].Definition.MatchesAnyAssetTag(assetTags))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 효과 태그가 지정한 태그이거나 그 하위인 유지 중 효과를 모두 제거하고 되돌린다.
        /// </summary>
        /// <param name="assetTags">걷어낼 효과를 가리키는 태그 목록이며 보통 계열을 대표하는 상위 이름이다.</param>
        /// <returns>제거한 효과 수이다.</returns>
        public int RemoveAllWithTags(IEnumerable<GameplayTag> assetTags)
        {
            if (assetTags == null || _activeEffects.Count == 0)
            {
                return 0;
            }

            return RemoveWhere(effect => effect.Definition.MatchesAnyAssetTag(assetTags));
        }

        /// <summary>
        /// 효과를 대상에게 적용한다.
        /// </summary>
        /// <param name="definition">적용할 효과 정의이다.</param>
        /// <param name="source">이 효과를 적용한 원인이며, 한꺼번에 걷어낼 때 사용한다.</param>
        /// <returns>
        /// 유지되기 시작한 효과 기록이며, 즉시 효과이거나 태그 조건에 막혀 적용하지 못했으면 null이다.
        /// </returns>
        public ActiveGameplayEffect Apply(GameplayEffectDefinition definition, object source = null)
        {
            return Apply(definition, source, null);
        }

        /// <summary>
        /// 적용하는 순간의 사정을 밝혀 효과를 대상에게 적용한다.
        /// </summary>
        /// <remarks>
        /// 사정에서 크기를 읽는 수정자는 이 순간 한 번 크기가 정해진다. 읽을 곳이 없는 수정자는 경고를 남기고 건너뛴다.
        /// 가해자는 이 효과가 바꾸는 어트리뷰트의 변화 원인에 실려 필터와 변화 알림에 닿는다.
        /// </remarks>
        /// <param name="definition">적용할 효과 정의이다.</param>
        /// <param name="source">이 효과를 적용한 원인이며, 한꺼번에 걷어낼 때 사용한다.</param>
        /// <param name="context">적용하는 순간의 사정이며 없으면 null이다.</param>
        /// <returns>
        /// 유지되기 시작한 효과 기록이며, 즉시 효과이거나 태그 조건에 막혀 적용하지 못했으면 null이다.
        /// </returns>
        public ActiveGameplayEffect Apply(GameplayEffectDefinition definition, object source, GameplayEffectContext context)
        {
            TryApply(definition, out var effect, source, context);
            return effect;
        }

        /// <summary>
        /// 적용 결과를 명시적으로 돌려준다. 즉시 효과 성공은 Executed이고, 조건에 막히면 Rejected이다.
        /// 콜백 중 제거되면 Cancelled이며 이미 실행된 기본값 변화는 되돌리지 않는다.
        /// </summary>
        public GameplayEffectApplicationResult TryApply(GameplayEffectDefinition definition,
            out ActiveGameplayEffect effect, object source = null, GameplayEffectContext context = null)
        {
            return TryApply(definition, out effect, source, context, null);
        }

        // 어빌리티 준비가 취소되면 같은 비용/쿨다운 적용의 나머지 작업도 멈춘다.
        internal GameplayEffectApplicationResult TryApply(GameplayEffectDefinition definition,
            out ActiveGameplayEffect effect, object source, GameplayEffectContext context, Func<bool> canContinue)
        {
            effect = null;
            if (!CanApply(definition) || !CanContinue(canContinue))
            {
                return GameplayEffectApplicationResult.Rejected;
            }

            // 걷어낼 것을 먼저 걷어낸다. 정화가 걷어낸 디버프의 수정자가 이 효과의 계산에 남아 있지 않게 하기 위해서이다.
            if (definition.RemoveEffectsWithTags.Count > 0)
            {
                RemoveAllWithTags(definition.RemoveEffectsWithTags);
            }

            // 제거 알림도 대상의 조건이나 이 요청의 수명주기를 바꿀 수 있다.
            if (!CanApply(definition) || !CanContinue(canContinue))
            {
                return GameplayEffectApplicationResult.Rejected;
            }

            if (definition.IsInstant)
            {
                if (!ExecuteModifiers(definition, context, source ?? definition, canContinue: canContinue))
                {
                    return GameplayEffectApplicationResult.Cancelled;
                }

                PublishChange(new GameplayEffectChange(definition, null, GameplayEffectChangeKind.Executed));
                if (CanContinue(canContinue))
                {
                    DispatchCues(definition, GameplayCueEventKind.Executed, null, context, canContinue);
                }

                return CanContinue(canContinue)
                    ? GameplayEffectApplicationResult.Executed : GameplayEffectApplicationResult.Cancelled;
            }

            if (definition.Stacking.IsStacking)
            {
                var existing = FindStack(definition, source);
                if (existing != null)
                {
                    if (existing.IsChangingResources)
                    {
                        return GameplayEffectApplicationResult.Rejected;
                    }

                    effect = existing;
                    AddStack(existing);
                    return existing.IsActive && CanContinue(canContinue)
                        ? GameplayEffectApplicationResult.Applied : GameplayEffectApplicationResult.Cancelled;
                }
            }

            effect = new ActiveGameplayEffect(definition, source, context);
            _activeEffects.Add(effect);
            var appliedEffect = effect;
            try
            {
                // 적용하는 순간 진행 조건이 깨져 있으면 억제된 채 시작한다.
                effect.IsInhibited = !MeetsOngoingRequirements(definition);
                if (!effect.IsInhibited)
                {
                    EnableEffect(effect, canContinue);
                }

                if (!effect.IsActive || !CanContinue(canContinue))
                {
                    Remove(effect);
                    return GameplayEffectApplicationResult.Cancelled;
                }

                PublishChange(new GameplayEffectChange(definition, effect, GameplayEffectChangeKind.Applied));
                if (effect.IsActive && !effect.IsInhibited && CanContinue(canContinue))
                {
                    DispatchCues(definition, GameplayCueEventKind.Applied, effect, context, canContinue);
                }

                if (effect.IsActive && !effect.IsInhibited && CanContinue(canContinue) &&
                    definition.HasPeriod && definition.ExecuteOnApplication)
                {
                    ExecutePeriod(effect, canContinue);
                }

                if (!effect.IsActive || !CanContinue(canContinue))
                {
                    Remove(effect);
                    return GameplayEffectApplicationResult.Cancelled;
                }

                return GameplayEffectApplicationResult.Applied;
            }
            catch (Exception failure)
            {
                List<Exception> failures = new() { failure };
                CaptureCleanupFailure(() => Remove(appliedEffect), ref failures);
                ThrowCleanupFailures(failures);
                throw;
            }
        }

        /// <summary>대상의 태그가 이 효과의 진행 조건을 만족하는지 확인한다. 조건이 없으면 언제나 만족이다.</summary>
        /// <param name="definition">확인할 효과 정의이다.</param>
        /// <returns>만족하면 true이다.</returns>
        public bool MeetsOngoingRequirements(GameplayEffectDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            return !definition.HasOngoingRequirements ||
                   (_tags.HasAll(definition.OngoingRequiredTags) && !_tags.HasAny(definition.OngoingBlockedTags));
        }

        /// <summary>
        /// 진행 조건을 가진 유지 중 효과를 모두 다시 보고 억제 상태를 맞춘다.
        /// 억제를 바꾸는 것 자체가 태그를 바꾸므로, 다시 보는 도중의 변화는 끝난 뒤 한 번 더 본다.
        /// </summary>
        private void RefreshInhibition()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_isRefreshingInhibition || _resourceChangeDepth > 0)
            {
                _isInhibitionDirty = true;
                return;
            }

            _isRefreshingInhibition = true;
            try
            {
                do
                {
                    _isInhibitionDirty = false;
                    var candidates = new List<ActiveGameplayEffect>(_activeEffects);
                    foreach (var effect in candidates)
                    {
                        if (!effect.IsActive || !effect.Definition.HasOngoingRequirements)
                        {
                            continue;
                        }

                        var shouldInhibit = !MeetsOngoingRequirements(effect.Definition);
                        if (shouldInhibit != effect.IsInhibited)
                        {
                            SetInhibited(effect, shouldInhibit);
                        }
                    }
                }
                while (_isInhibitionDirty);
            }
            finally
            {
                _isRefreshingInhibition = false;
            }
        }

        /// <summary>효과의 억제 상태를 바꾸고 그에 맞게 수정자와 부여 태그를 걷거나 다시 얹은 뒤 알린다.</summary>
        /// <param name="effect">상태를 바꿀 효과 기록이다.</param>
        /// <param name="inhibited">억제할지 여부이다.</param>
        private void SetInhibited(ActiveGameplayEffect effect, bool inhibited)
        {
            effect.IsInhibited = inhibited;
            List<Exception> failures = null;
            if (inhibited)
            {
                CaptureCleanupFailure(() => DisableEffect(effect), ref failures);
            }
            else
            {
                try
                {
                    EnableEffect(effect);
                }
                catch (Exception failure)
                {
                    failures = new List<Exception> { failure };
                    CaptureCleanupFailure(() => Remove(effect), ref failures);
                }
            }

            if (effect.IsActive && effect.IsInhibited == inhibited)
            {
                CaptureCleanupFailure(() => PublishChange(new GameplayEffectChange(
                    effect.Definition, effect, GameplayEffectChangeKind.InhibitionChanged)), ref failures);
                CaptureCleanupFailure(() => DispatchCues(
                    effect.Definition,
                    inhibited ? GameplayCueEventKind.Removed : GameplayCueEventKind.Applied,
                    effect,
                    effect.Context), ref failures);
            }

            ThrowCleanupFailures(failures);
        }

        /// <summary>효과가 작용하도록 층수만큼 수정자를 얹고 태그를 부여한다.</summary>
        /// <param name="effect">작용시킬 효과 기록이다.</param>
        private void EnableEffect(ActiveGameplayEffect effect, Func<bool> canContinue = null)
        {
            var version = ++effect.ResourceVersion;
            BeginResourceChange(effect);
            try
            {
                var definition = effect.Definition;
                if (definition.UsesLingeringModifiers)
                {
                    for (var layer = 0; layer < effect.StackCount && CanChangeResources(effect, version, canContinue); layer++)
                    {
                        AddLingeringModifiers(definition, effect, canContinue);
                    }
                }

                foreach (var tag in definition.GrantedTags)
                {
                    if (!CanChangeResources(effect, version, canContinue))
                    {
                        break;
                    }

                    effect.GrantedTags.Add(tag);
                    _tags.AddTag(tag);
                }
            }
            finally
            {
                EndResourceChange(effect);
            }
        }

        /// <summary>효과의 작용을 멈추도록 수정자와 부여 태그를 걷는다. 기록과 지속 시간은 그대로 둔다.</summary>
        /// <param name="effect">작용을 멈출 효과 기록이다.</param>
        private void DisableEffect(ActiveGameplayEffect effect)
        {
            effect.ResourceVersion++;
            var handles = effect.TakeStackModifiers();
            var tags = new List<GameplayTag>(effect.GrantedTags);
            effect.GrantedTags.Clear();
            List<Exception> failures = null;
            BeginResourceChange(effect);
            try
            {
                // AddModifier의 알림 중이면 아직 손잡이가 반환되지 않은 수정자도 있다.
                CaptureCleanupFailure(() => _attributes.RemoveModifiersFrom(effect), ref failures);
                foreach (var handle in handles)
                {
                    CaptureCleanupFailure(handle.Dispose, ref failures);
                }

                foreach (var tag in tags)
                {
                    CaptureCleanupFailure(() => _tags.RemoveTag(tag), ref failures);
                }
            }
            finally
            {
                EndResourceChange(effect);
            }

            ThrowCleanupFailures(failures);
        }

        /// <summary>
        /// 유지되던 효과를 제거하고 얹었던 수정자와 부여한 태그를 되돌린다.
        /// </summary>
        /// <param name="effect">제거할 효과 기록이다.</param>
        /// <returns>실제로 제거했으면 true이며, 이미 제거되었거나 이 실행기의 것이 아니면 false이다.</returns>
        public bool Remove(ActiveGameplayEffect effect)
        {
            if (effect == null || !effect.IsActive || !_activeEffects.Remove(effect))
            {
                return false;
            }

            RevertEffects(new[] { effect });
            return true;
        }

        /// <summary>
        /// 지정한 원인이 적용한 효과를 모두 제거한다.
        /// </summary>
        /// <param name="source">걷어낼 효과의 원인이며 null이면 아무것도 하지 않는다.</param>
        /// <returns>제거한 효과 수이다.</returns>
        public int RemoveAllFrom(object source)
        {
            if (source == null)
            {
                return 0;
            }

            return RemoveWhere(effect => ReferenceEquals(effect.Source, source));
        }

        /// <summary>
        /// 유지되고 있는 효과를 모두 제거한다.
        /// </summary>
        /// <returns>제거한 효과 수이다.</returns>
        public int RemoveAll()
        {
            var removedEffects = new List<ActiveGameplayEffect>(_activeEffects);
            _activeEffects.Clear();
            RevertEffects(removedEffects);
            return removedEffects.Count;
        }

        /// <summary>조건에 맞는 유지 중 효과를 먼저 모두 골라 목록에서 뺀 뒤, 그것들만 되돌린다.</summary>
        /// <remarks>
        /// <b>고르기와 되돌리기를 나누는 것이 계약이다.</b> 되돌리기가 내는 걷힘 알림 안에서 구독자가 같은 실행기의
        /// 다른 효과를 빼거나 더할 수 있다. 살아 있는 목록을 돌며 되돌리면 그 순간 목록이 짧아져 다음 인덱스가
        /// 범위 밖이 된다. 모두 걷기(<see cref="RemoveAll"/>)와 같은 꼴이며, 되돌리는 차례는 나중에 적용된 것부터다.
        /// 골라 둔 효과를 구독자가 먼저 빼려 해도 이미 목록에 없으므로 두 번 되돌려지지 않는다.
        /// </remarks>
        /// <param name="predicate">걷을 효과를 고르는 조건이다.</param>
        /// <returns>제거한 효과 수이다.</returns>
        private int RemoveWhere(Func<ActiveGameplayEffect, bool> predicate)
        {
            List<ActiveGameplayEffect> removedEffects = null;
            for (var index = _activeEffects.Count - 1; index >= 0; index--)
            {
                var effect = _activeEffects[index];
                if (!predicate(effect))
                {
                    continue;
                }

                removedEffects ??= new List<ActiveGameplayEffect>();
                removedEffects.Add(effect);
                _activeEffects.RemoveAt(index);
            }

            if (removedEffects == null)
            {
                return 0;
            }

            RevertEffects(removedEffects);
            return removedEffects.Count;
        }

        /// <summary>
        /// 시간을 흘려 주기 실행과 만료를 처리한다.
        /// </summary>
        /// <param name="deltaTime">흐른 시간(초)이며 0 이하이면 아무 일도 하지 않는다.</param>
        public void Tick(float deltaTime)
        {
            if (_isDisposed || _isTicking || deltaTime <= 0f || _activeEffects.Count == 0)
            {
                return;
            }

            // 실행과 제거 알림을 받은 쪽이 효과를 더하거나 뺄 수 있으므로 순회 대상을 먼저 확정한다.
            _isTicking = true;
            try
            {
                _tickBuffer.Clear();
                _tickBuffer.AddRange(_activeEffects);
                foreach (var effect in _tickBuffer)
                {
                    if (!effect.IsActive || effect.IsChangingResources)
                    {
                        continue;
                    }

                    AdvanceEffect(effect, deltaTime);
                }
            }
            finally
            {
                _tickBuffer.Clear();
                _isTicking = false;
            }
        }

        /// <summary>
        /// 효과 하나에 시간을 흘려 주기 실행과 만료를 처리한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 주기 실행은 효과가 살아 있는 동안에만 일어난다. 한 스텝이 남은 지속 시간보다 크면
        /// 만료 시각까지의 시간으로만 주기를 세어 스텝 크기에 관계없이 같은 횟수를 실행한다.
        /// </para>
        /// <para>
        /// <b>경계는 실행하는 쪽으로 정한다.</b> 주기가 만료 시각과 정확히 겹치면 그 실행은 나간다.
        /// 3초 지속·1초 주기는 적용 시 한 번에 더해 세 번 실행하며, 스텝을 어떻게 쪼개도 같다.
        /// 반대로 정하면 딱 떨어지는 설정에서 마지막 한 번이 사라져 설정한 사람의 셈과 어긋난다.
        /// </para>
        /// <para>
        /// 만료 정책이 지속 시간을 갱신하면 남은 스텝 시간도 이어서 처리한다.
        /// </para>
        /// </remarks>
        /// <param name="effect">시간을 흘릴 효과 기록이다.</param>
        /// <param name="deltaTime">흐른 시간(초)이며 0보다 크다.</param>
        private void AdvanceEffect(ActiveGameplayEffect effect, float deltaTime)
        {
            var remainingStep = deltaTime;
            while (remainingStep > 0f && effect.IsActive)
            {
                var endsOnTime = effect.Definition.DurationPolicy == GameplayEffectDurationPolicy.Duration;
                var slice = endsOnTime
                    ? Mathf.Clamp(effect.RemainingTime, 0f, remainingStep)
                    : remainingStep;

                // 억제된 동안에는 주기 실행을 쉬되 지속 시간은 그대로 흐른다.
                if (effect.Definition.HasPeriod && !effect.IsInhibited)
                {
                    TickPeriod(effect, slice);
                }

                remainingStep -= slice;
                if (!endsOnTime || !effect.IsActive)
                {
                    return;
                }

                effect.RemainingTime -= slice;
                if (effect.RemainingTime > 0f)
                {
                    return;
                }

                Expire(effect);

                // 갱신되지 않는 만료라면 여기서 끝나고, 갱신되었어도 나아간 것이 없으면 멈춘다.
                if (slice <= 0f)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// 지속 시간이 다한 효과를 쌓임 규칙에 따라 걷는다.
        /// 쌓이지 않았거나 한 층뿐이면 통째로 걷고, 여러 층이면 만료 정책이 정한다.
        /// </summary>
        /// <param name="effect">지속 시간이 다한 효과 기록이다.</param>
        private void Expire(ActiveGameplayEffect effect)
        {
            var settings = effect.Definition.Stacking;
            if (!settings.IsStacking || effect.StackCount <= 1)
            {
                Remove(effect);
                return;
            }

            switch (settings.ExpirationPolicy)
            {
                case GameplayEffectStackExpirationPolicy.RemoveSingleStackAndRefreshDuration:
                    effect.StackCount--;
                    BeginResourceChange(effect);
                    List<Exception> failures = null;
                    try
                    {
                        CaptureCleanupFailure(effect.PopStackModifiers, ref failures);
                        if (effect.IsActive)
                        {
                            effect.RemainingTime = effect.Definition.Duration;
                            CaptureCleanupFailure(() => PublishChange(new GameplayEffectChange(
                                effect.Definition, effect, GameplayEffectChangeKind.StackChanged)), ref failures);
                        }
                    }
                    finally
                    {
                        CaptureCleanupFailure(() => EndResourceChange(effect), ref failures);
                    }

                    ThrowCleanupFailures(failures);
                    break;

                case GameplayEffectStackExpirationPolicy.RefreshDuration:
                    effect.RemainingTime = effect.Definition.Duration;
                    break;

                default:
                    Remove(effect);
                    break;
            }
        }

        /// <summary>같은 정의로 쌓을 수 있는 유지 중 효과를 찾는다.</summary>
        /// <param name="definition">찾을 효과 정의이다.</param>
        /// <param name="source">이번 적용의 원인이며, 건 것마다 쌓는 규칙에서 견준다.</param>
        /// <returns>층을 더할 기록이며 없으면 null이다.</returns>
        private ActiveGameplayEffect FindStack(GameplayEffectDefinition definition, object source)
        {
            var bySource = definition.Stacking.Policy == GameplayEffectStackingPolicy.AggregateBySource;
            for (var index = 0; index < _activeEffects.Count; index++)
            {
                var effect = _activeEffects[index];
                if (effect.Definition != definition)
                {
                    continue;
                }

                if (!bySource || ReferenceEquals(effect.Source, source))
                {
                    return effect;
                }
            }

            return null;
        }

        /// <summary>
        /// 유지 중인 기록에 층을 더한다. 한도에 닿았으면 층은 더하지 않되 지속 시간과 주기 타이머는 정책대로 갱신한다.
        /// </summary>
        /// <param name="effect">층을 더할 기록이다.</param>
        private void AddStack(ActiveGameplayEffect effect)
        {
            BeginResourceChange(effect);
            try
            {
                var definition = effect.Definition;
                var settings = definition.Stacking;
                if (settings.CanGrow(effect.StackCount))
                {
                    effect.StackCount++;

                    // 억제된 동안에는 얹지 않는다. 억제가 풀릴 때 층수만큼 다시 얹는다.
                    if (definition.UsesLingeringModifiers && !effect.IsInhibited)
                    {
                        AddLingeringModifiers(definition, effect);
                    }
                }

                if (!effect.IsActive || _isDisposed)
                {
                    return;
                }

                if (settings.DurationRefreshPolicy == GameplayEffectStackDurationRefreshPolicy.RefreshOnSuccessfulApplication &&
                    definition.DurationPolicy == GameplayEffectDurationPolicy.Duration)
                {
                    effect.RemainingTime = definition.Duration;
                }

                if (settings.PeriodResetPolicy == GameplayEffectStackPeriodResetPolicy.ResetOnSuccessfulApplication &&
                    definition.HasPeriod)
                {
                    effect.PeriodAccumulator = 0f;
                }

                PublishChange(new GameplayEffectChange(definition, effect, GameplayEffectChangeKind.StackChanged));
            }
            catch (Exception failure)
            {
                List<Exception> failures = new() { failure };
                CaptureCleanupFailure(() => Remove(effect), ref failures);
                ThrowCleanupFailures(failures);
                throw;
            }
            finally
            {
                EndResourceChange(effect);
            }
        }

        /// <summary>쌓인 시간만큼 주기 실행을 반복한다.</summary>
        /// <param name="effect">처리할 효과 기록이다.</param>
        /// <param name="deltaTime">흐른 시간(초)이다.</param>
        private void TickPeriod(ActiveGameplayEffect effect, float deltaTime)
        {
            var period = Mathf.Max(MinimumPeriod, effect.Definition.Period);
            effect.PeriodAccumulator += deltaTime;
            while (effect.IsActive && !effect.IsInhibited && effect.PeriodAccumulator >= period)
            {
                effect.PeriodAccumulator -= period;
                ExecutePeriod(effect);
            }
        }

        /// <summary>주기 실행을 한 번 수행하고 알린다. 적용할 때의 사정을 그대로 다시 쓰며, 쌓인 층수만큼 반복한다.</summary>
        /// <param name="effect">실행할 효과 기록이다.</param>
        private void ExecutePeriod(ActiveGameplayEffect effect, Func<bool> canContinue = null)
        {
            var version = effect.ResourceVersion;
            Func<bool> continuePeriod = () => CanChangeResources(effect, version, canContinue);
            if (!ExecuteModifiers(effect.Definition, effect.Context, effect, effect.StackCount, continuePeriod))
            {
                return;
            }

            PublishChange(new GameplayEffectChange(effect.Definition, effect, GameplayEffectChangeKind.Executed));
            if (continuePeriod())
            {
                DispatchCues(effect.Definition, GameplayCueEventKind.Executed, effect, effect.Context, continuePeriod);
            }
        }

        /// <summary>
        /// 수정자 목록을 어트리뷰트의 기본값에 실행한다.
        /// 즉시 효과와 주기 실행이 쓰는 경로이며, 이 변화는 효과가 사라져도 되돌아가지 않는다.
        /// </summary>
        /// <param name="definition">실행할 효과 정의이다.</param>
        /// <param name="context">적용하는 순간의 사정이며 없으면 null이다.</param>
        /// <param name="cause">어트리뷰트 변화의 원인으로 밝힐 것이다. 사정이 건 것을 밝히면 그쪽이 우선한다.</param>
        /// <param name="repeatCount">목록을 실행할 횟수이며 쌓인 층수이다.</param>
        private bool ExecuteModifiers(
            GameplayEffectDefinition definition,
            GameplayEffectContext context,
            object cause,
            int repeatCount = 1,
            Func<bool> canContinue = null)
        {
            var changeContext = context != null
                ? context.ToAttributeChangeContext(cause)
                : new AttributeChangeContext(cause);

            for (var repeat = 0; repeat < Mathf.Max(1, repeatCount); repeat++)
            {
                foreach (var modifier in definition.Modifiers)
                {
                    if (!CanContinue(canContinue))
                    {
                        return false;
                    }

                    if (!TryResolveMagnitude(definition, modifier, context, out var magnitude))
                    {
                        continue;
                    }

                    switch (modifier.Operation)
                    {
                        case AttributeModifierOperation.Add:
                            _attributes.AddToBaseValue(modifier.Attribute, magnitude, changeContext);
                            break;

                        case AttributeModifierOperation.Multiply:
                            _attributes.SetBaseValue(
                                modifier.Attribute,
                                _attributes.GetBaseValue(modifier.Attribute) * magnitude,
                                changeContext);
                            break;

                        case AttributeModifierOperation.Override:
                            _attributes.SetBaseValue(modifier.Attribute, magnitude, changeContext);
                            break;
                    }
                }
            }

            return CanContinue(canContinue);
        }

        /// <summary>
        /// 되돌릴 수 있는 수정자 한 벌을 얹는다. 효과 기록 자체를 출처로 삼아 만료 시 한 번에 걷어낼 수 있게 하고,
        /// 층 단위로도 되돌릴 수 있도록 손잡이를 기록에 남긴다.
        /// 사정에서 읽는 크기는 여기서 한 번 정해지고 그 뒤 원천이 바뀌어도 따라가지 않는다.
        /// </summary>
        /// <param name="definition">적용할 효과 정의이다.</param>
        /// <param name="effect">수정자의 출처가 될 효과 기록이다.</param>
        private void AddLingeringModifiers(GameplayEffectDefinition definition, ActiveGameplayEffect effect,
            Func<bool> canContinue = null)
        {
            var version = effect.ResourceVersion;
            var handles = new List<IDisposable>(definition.Modifiers.Count);
            effect.PushStackModifiers(handles);
            foreach (var modifier in definition.Modifiers)
            {
                if (!CanChangeResources(effect, version, canContinue))
                {
                    break;
                }

                if (!TryResolveMagnitude(definition, modifier, effect.Context, out var magnitude))
                {
                    continue;
                }

                var handle = _attributes.AddModifier(modifier.Attribute, modifier.Operation, magnitude, effect);
                if (!CanChangeResources(effect, version, canContinue))
                {
                    handle.Dispose();
                    break;
                }

                handles.Add(handle);
            }
        }

        /// <summary>
        /// 수정자의 크기를 정한다. 유효하지 않은 수정자는 조용히 건너뛰고, 읽을 곳이 없는 수정자는 경고를 남기고 건너뛴다.
        /// </summary>
        /// <param name="definition">수정자가 속한 효과 정의이며 경고에 이름을 쓴다.</param>
        /// <param name="modifier">크기를 정할 수정자이다.</param>
        /// <param name="context">적용하는 순간의 사정이며 없으면 null이다.</param>
        /// <param name="magnitude">정해진 크기이다.</param>
        /// <returns>크기를 정했으면 true이다.</returns>
        private bool TryResolveMagnitude(
            GameplayEffectDefinition definition,
            GameplayEffectModifier modifier,
            GameplayEffectContext context,
            out float magnitude)
        {
            magnitude = 0f;
            if (modifier == null || !modifier.IsValid)
            {
                return false;
            }

            if (modifier.TryResolveMagnitude(context, _attributes, out magnitude))
            {
                return true;
            }

            Debug.LogWarning(
                $"[GameplayEffectRunner] {definition.name}의 수정자 {modifier}가 크기를 읽을 곳이 없어 건너뛴다. " +
                "적용하는 쪽이 사정에 그 값을 실었는지 확인해야 한다.",
                definition);
            return false;
        }

        private bool CanContinue(Func<bool> canContinue)
        {
            return !_isDisposed && (canContinue == null || canContinue());
        }

        private bool CanChangeResources(ActiveGameplayEffect effect, int version, Func<bool> canContinue = null)
        {
            return effect.IsActive && !effect.IsInhibited && effect.ResourceVersion == version && CanContinue(canContinue);
        }

        private void BeginResourceChange(ActiveGameplayEffect effect)
        {
            _resourceChangeDepth++;
            effect.ResourceChangeDepth++;
        }

        private void EndResourceChange(ActiveGameplayEffect effect)
        {
            effect.ResourceChangeDepth--;
            _resourceChangeDepth--;
            if (_resourceChangeDepth == 0 && _isInhibitionDirty && !_isRefreshingInhibition)
            {
                RefreshInhibition();
            }
        }

        private void PublishChange(GameplayEffectChange change)
        {
            if (!_notificationsClosed)
            {
                _changed.OnNext(change);
            }
        }

        private void CloseNotifications()
        {
            if (_notificationsClosed)
            {
                return;
            }

            _notificationsClosed = true;
            _changed.Dispose();
        }

        private void RevertEffects(IReadOnlyList<ActiveGameplayEffect> effects)
        {
            _removalDepth++;
            List<Exception> failures = null;
            try
            {
                foreach (var effect in effects)
                {
                    CaptureCleanupFailure(() => RevertEffect(effect), ref failures);
                }
            }
            finally
            {
                _removalDepth--;
                // 제거 콜백 안에서 Dispose되어도 바깥 배치가 보낼 Removed는 모두 전달한다.
                if (_isDisposed && _removalDepth == 0)
                {
                    CaptureCleanupFailure(CloseNotifications, ref failures);
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
                throw new AggregateException("효과 정리를 마쳤지만 일부 콜백이 실패했다.", failures).Flatten();
            }
        }

        /// <summary>
        /// 효과가 남긴 것을 모두 되돌리고 제거를 알린다.
        /// 수정자는 효과 기록을 출처로 한 번에 걷어내고, 부여한 태그는 부여한 만큼만 회수한다.
        /// </summary>
        /// <param name="effect">되돌릴 효과 기록이다.</param>
        private void RevertEffect(ActiveGameplayEffect effect)
        {
            effect.IsActive = false;
            List<Exception> failures = null;
            CaptureCleanupFailure(() => DisableEffect(effect), ref failures);
            CaptureCleanupFailure(() => PublishChange(
                new GameplayEffectChange(effect.Definition, effect, GameplayEffectChangeKind.Removed)), ref failures);
            CaptureCleanupFailure(() => DispatchCues(
                effect.Definition, GameplayCueEventKind.Removed, effect, effect.Context), ref failures);
            ThrowCleanupFailures(failures);
        }
    }
}
