using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Attributes
{
    /// <summary>
    /// 어트리뷰트의 기본값과 수정자 목록을 보관하고 현재값을 계산한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>진실의 원천은 여기 하나다.</b> 어트리뷰트의 값을 다른 곳이 따로 들고 있으면 두 값이 어긋나므로,
    /// 소비자는 값을 복사해 두지 말고 필요할 때마다 <see cref="GetCurrentValue"/>로 읽거나
    /// <see cref="Changed"/>를 구독해 갱신한다.
    /// </para>
    /// <para>
    /// <b>계산 순서는 더하기 → 곱하기 → 덮어쓰기이며 그 뒤에 하한과 상한을 적용한다.</b>
    /// 곧 <c>(기본값 + 더하기 합) × 곱하기 곱</c>을 구하고, 덮어쓰기 수정자가 있으면 그 값으로 대체한 다음
    /// 하한과 상한으로 자른다. 이 순서는 <see cref="AttributeModifierOperation"/>에 고정되어 있으며 바꾸지 않는다.
    /// </para>
    /// <para>
    /// <b>기본값과 수정자의 역할이 다르다.</b> 피해나 회복처럼 오래 남아야 하는 변화는 기본값을 바꾸고,
    /// 버프나 장비처럼 원인이 사라지면 되돌아가야 하는 변화는 수정자로 얹는다.
    /// 그래서 저장 대상은 기본값이다. 수정자는 효과와 장비가 다시 만들어 주므로 저장하지 않는다.
    /// 저장 형태는 <see cref="AttributeSetSnapshot"/>이 제공한다.
    /// </para>
    /// <para>
    /// <b>기본값이 바뀌는 자리에 끼어들 수 있다.</b> <see cref="AddBaseValueFilter"/>로 등록한 필터는
    /// 즉시 효과·직접 쓰기 같은 기본값 변화가 적용되기 직전에 제안된 값을 고친다. 피해를 가로채는 보호막,
    /// 체력이 0에 닿는 것을 지켜보는 사망 처리처럼 값이 바뀌는 순간에 개입해야 하는 것들이 이 자리를 쓰며,
    /// 변화의 원인은 <see cref="AttributeChangeContext"/>로 필터와 <see cref="Changed"/> 알림에 함께 실린다.
    /// </para>
    /// <para>
    /// 이 집합은 어빌리티나 효과를 전제하지 않으므로, 장비와 버프의 스탯 계산만으로도 단독으로 쓸 수 있다.
    /// </para>
    /// </remarks>
    public sealed class AttributeSet : IDisposable
    {
        private readonly Dictionary<AttributeDefinition, AttributeEntry> _entries = new();
        private readonly Dictionary<AttributeDefinition, List<AttributeDefinition>> _dependents = new();
        private readonly Dictionary<AttributeDefinition, List<IAttributeBaseValueFilter>> _baseValueFilters = new();
        private readonly Subject<AttributeChangedEvent> _changed = new();
        private readonly Observable<AttributeChangedEvent> _changedObservable;
        private bool _isDisposed;

        /// <summary>빈 어트리뷰트 집합을 생성한다.</summary>
        public AttributeSet()
        {
            _changedObservable = _changed.AsObservable();
        }

        /// <summary>어트리뷰트의 현재값이 바뀔 때마다 흐르는 스트림이다.</summary>
        public Observable<AttributeChangedEvent> Changed => _changedObservable;

        /// <summary>이 집합이 담고 있는 어트리뷰트 정의를 열거한다.</summary>
        public IReadOnlyCollection<AttributeDefinition> Definitions => _entries.Keys;

        /// <summary>담고 있는 어트리뷰트 수이다.</summary>
        public int Count => _entries.Count;

        /// <summary>지정한 어트리뷰트를 담고 있는지 확인한다.</summary>
        /// <param name="definition">확인할 어트리뷰트 정의이다.</param>
        /// <returns>담고 있으면 true이다.</returns>
        public bool Contains(AttributeDefinition definition)
        {
            return definition != null && _entries.ContainsKey(definition);
        }

        /// <summary>
        /// 어트리뷰트를 집합에 추가한다. 이미 있으면 아무것도 하지 않는다.
        /// 상한 어트리뷰트가 자기 자신으로 돌아오는 순환을 이루면 추가하지 않고 진단을 남긴다.
        /// </summary>
        /// <remarks>
        /// 시작 기본값은 하한과 상한 안으로 넣고 시작한다. 상한 어트리뷰트가 아직 집합에 없으면 그것이 추가되는 순간
        /// 다시 넣으므로, 어느 쪽을 먼저 나열하든 기본값이 같다. 넣지 않고 두면 상한을 먼저 나열한 경우에만
        /// 기본값이 상한 위에 남아, 나열 순서가 저장되는 값을 바꾼다.
        /// </remarks>
        /// <param name="definition">추가할 어트리뷰트 정의이다.</param>
        /// <param name="baseValue">시작 기본값이며, 지정하지 않으면 정의의 기본값을 쓴다.</param>
        /// <returns>새로 추가했으면 true이다.</returns>
        public bool AddAttribute(AttributeDefinition definition, float? baseValue = null)
        {
            if (definition == null || _entries.ContainsKey(definition))
            {
                return false;
            }

            if (HasCapCycle(definition))
            {
                Debug.LogError(
                    $"[AttributeSet] {definition.Id}의 상한 연결이 순환을 이루어 추가하지 않는다.", definition);
                return false;
            }

            var entry = new AttributeEntry(ClampBaseToBounds(definition, baseValue ?? definition.DefaultBaseValue));
            _entries.Add(definition, entry);
            RegisterDependency(definition);
            entry.LastCapValue = ResolveCapValue(definition);
            entry.CurrentValue = Compute(definition, entry);

            // 이 어트리뷰트를 상한으로 기다리던 쪽이 이미 있을 수 있으므로 상한이 생긴 사실을 알린다.
            RefreshDependents(definition);
            return true;
        }

        /// <summary>
        /// 어트리뷰트를 집합에서 제거한다. 이 어트리뷰트를 상한으로 삼던 어트리뷰트는 상한이 풀린다.
        /// </summary>
        /// <param name="definition">제거할 어트리뷰트 정의이다.</param>
        /// <returns>실제로 제거했으면 true이다.</returns>
        public bool RemoveAttribute(AttributeDefinition definition)
        {
            if (definition == null || !_entries.Remove(definition))
            {
                return false;
            }

            UnregisterDependency(definition);
            RefreshDependents(definition);
            return true;
        }

        /// <summary>지정한 어트리뷰트의 기본값을 가져오며, 담고 있지 않으면 0을 반환한다.</summary>
        /// <param name="definition">조회할 어트리뷰트 정의이다.</param>
        /// <returns>기본값이다.</returns>
        public float GetBaseValue(AttributeDefinition definition)
        {
            return TryGetEntry(definition, out var entry) ? entry.BaseValue : 0f;
        }

        /// <summary>지정한 어트리뷰트의 기본값을 조회한다.</summary>
        /// <param name="definition">조회할 어트리뷰트 정의이다.</param>
        /// <param name="baseValue">찾은 기본값이며 없으면 0이다.</param>
        /// <returns>담고 있으면 true이다.</returns>
        public bool TryGetBaseValue(AttributeDefinition definition, out float baseValue)
        {
            var found = TryGetEntry(definition, out var entry);
            baseValue = found ? entry.BaseValue : 0f;
            return found;
        }

        /// <summary>
        /// 기본값을 지정한 값으로 바꾼다.
        /// 피해와 회복처럼 원인이 사라져도 남아야 하는 변화가 이 경로를 쓴다.
        /// </summary>
        /// <param name="definition">바꿀 어트리뷰트 정의이다.</param>
        /// <param name="value">새 기본값이다.</param>
        public void SetBaseValue(AttributeDefinition definition, float value)
        {
            SetBaseValue(definition, value, AttributeChangeContext.None);
        }

        /// <summary>
        /// 원인을 밝혀 기본값을 지정한 값으로 바꾼다.
        /// 등록된 필터가 제안된 값을 검토한 뒤 적용되며, 원인은 필터와 변화 알림에 그대로 실린다.
        /// </summary>
        /// <remarks>
        /// 필터가 현재값을 돌려주면 이번 변화는 없던 것이 되어 알림도 나가지 않는다.
        /// 필터가 돌려준 값도 하한과 상한 안으로 넣으므로 필터가 범위를 지킬 책임은 없다.
        /// </remarks>
        /// <param name="definition">바꿀 어트리뷰트 정의이다.</param>
        /// <param name="value">새 기본값이다.</param>
        /// <param name="context">이 변화의 원인이다.</param>
        public void SetBaseValue(AttributeDefinition definition, float value, in AttributeChangeContext context)
        {
            if (!TryGetEntry(definition, out var entry))
            {
                return;
            }

            var proposedValue = ClampBaseToBounds(definition, value);
            proposedValue = ApplyBaseValueFilters(definition, entry.BaseValue, proposedValue, context);
            proposedValue = ClampBaseToBounds(definition, proposedValue);
            if (Mathf.Approximately(entry.BaseValue, proposedValue))
            {
                return;
            }

            entry.BaseValue = proposedValue;
            Recompute(definition, entry, context);
        }

        /// <summary>기본값에 지정한 양을 더한다. 음수를 넣으면 뺀다.</summary>
        /// <param name="definition">바꿀 어트리뷰트 정의이다.</param>
        /// <param name="delta">더할 양이다.</param>
        public void AddToBaseValue(AttributeDefinition definition, float delta)
        {
            AddToBaseValue(definition, delta, AttributeChangeContext.None);
        }

        /// <summary>원인을 밝혀 기본값에 지정한 양을 더한다. 음수를 넣으면 뺀다.</summary>
        /// <param name="definition">바꿀 어트리뷰트 정의이다.</param>
        /// <param name="delta">더할 양이다.</param>
        /// <param name="context">이 변화의 원인이다.</param>
        public void AddToBaseValue(AttributeDefinition definition, float delta, in AttributeChangeContext context)
        {
            if (TryGetEntry(definition, out var entry))
            {
                SetBaseValue(definition, entry.BaseValue + delta, context);
            }
        }

        /// <summary>
        /// 기본값이 바뀌기 직전에 끼어드는 필터를 등록한다. 같은 어트리뷰트에 여럿을 등록하면 등록한 순서로 부른다.
        /// </summary>
        /// <param name="definition">지켜볼 어트리뷰트 정의이다.</param>
        /// <param name="filter">끼어들 필터이다.</param>
        /// <returns>해제하면 이 필터만 빼는 손잡이이며, 등록하지 못했으면 아무 일도 하지 않는 손잡이이다.</returns>
        public IDisposable AddBaseValueFilter(AttributeDefinition definition, IAttributeBaseValueFilter filter)
        {
            if (definition == null || filter == null)
            {
                return Disposable.Empty;
            }

            if (!_baseValueFilters.TryGetValue(definition, out var filters))
            {
                filters = new List<IAttributeBaseValueFilter>();
                _baseValueFilters.Add(definition, filters);
            }

            filters.Add(filter);
            return new FilterHandle(this, definition, filter);
        }

        /// <summary>등록한 필터를 뺀다.</summary>
        /// <param name="definition">필터가 지켜보던 어트리뷰트 정의이다.</param>
        /// <param name="filter">뺄 필터이다.</param>
        /// <returns>실제로 뺐으면 true이다.</returns>
        public bool RemoveBaseValueFilter(AttributeDefinition definition, IAttributeBaseValueFilter filter)
        {
            if (definition == null || filter == null || !_baseValueFilters.TryGetValue(definition, out var filters))
            {
                return false;
            }

            var removed = filters.Remove(filter);
            if (filters.Count == 0)
            {
                _baseValueFilters.Remove(definition);
            }

            return removed;
        }

        /// <summary>지정한 어트리뷰트에 등록된 필터 수를 가져온다.</summary>
        /// <param name="definition">조회할 어트리뷰트 정의이다.</param>
        /// <returns>필터 수이며 없으면 0이다.</returns>
        public int GetBaseValueFilterCount(AttributeDefinition definition)
        {
            return definition != null && _baseValueFilters.TryGetValue(definition, out var filters) ? filters.Count : 0;
        }

        /// <summary>지정한 어트리뷰트의 현재값을 가져오며, 담고 있지 않으면 0을 반환한다.</summary>
        /// <param name="definition">조회할 어트리뷰트 정의이다.</param>
        /// <returns>수정자와 상한까지 반영한 현재값이다.</returns>
        public float GetCurrentValue(AttributeDefinition definition)
        {
            return TryGetEntry(definition, out var entry) ? entry.CurrentValue : 0f;
        }

        /// <summary>지정한 어트리뷰트의 현재값을 조회한다.</summary>
        /// <param name="definition">조회할 어트리뷰트 정의이다.</param>
        /// <param name="currentValue">찾은 현재값이며 없으면 0이다.</param>
        /// <returns>담고 있으면 true이다.</returns>
        public bool TryGetCurrentValue(AttributeDefinition definition, out float currentValue)
        {
            var found = TryGetEntry(definition, out var entry);
            currentValue = found ? entry.CurrentValue : 0f;
            return found;
        }

        /// <summary>
        /// 현재값을 정수로 가져온다. 변환 규칙은 <see cref="AttributeValue.ToInt"/> 하나로 통일되어 있다.
        /// </summary>
        /// <param name="definition">조회할 어트리뷰트 정의이다.</param>
        /// <returns>정수로 변환한 현재값이다.</returns>
        public int GetCurrentValueAsInt(AttributeDefinition definition)
        {
            return AttributeValue.ToInt(GetCurrentValue(definition));
        }

        /// <summary>
        /// 수정자를 얹고, 그 수정자만 정확히 되돌릴 수 있는 손잡이를 돌려준다.
        /// </summary>
        /// <remarks>
        /// 손잡이를 해제하면 이 수정자 하나만 사라지고 다른 수정자는 그대로 남는다.
        /// 지속 효과가 만료될 때 자기가 얹은 것만 걷어내는 경로가 이것이다.
        /// 여러 수정자를 한 원인이 얹었다면 <paramref name="source"/>를 함께 지정해
        /// <see cref="RemoveModifiersFrom"/>으로 한꺼번에 걷어낼 수도 있다.
        /// </remarks>
        /// <param name="definition">수정자를 얹을 어트리뷰트 정의이다.</param>
        /// <param name="operation">수정자가 값을 바꾸는 방식이다.</param>
        /// <param name="magnitude">수정자의 크기이다.</param>
        /// <param name="source">이 수정자를 얹은 원인이며, 출처별로 걷어낼 때 사용한다.</param>
        /// <returns>해제하면 이 수정자만 제거하는 손잡이이며, 얹지 못했으면 아무 일도 하지 않는 손잡이이다.</returns>
        public IDisposable AddModifier(
            AttributeDefinition definition,
            AttributeModifierOperation operation,
            float magnitude,
            object source = null)
        {
            if (!TryGetEntry(definition, out var entry))
            {
                return Disposable.Empty;
            }

            var modifier = new AttributeModifier(operation, magnitude, source);
            entry.Modifiers.Add(modifier);
            Recompute(definition, entry);
            return new ModifierHandle(this, definition, modifier);
        }

        /// <summary>
        /// 지정한 출처가 얹은 수정자를 모두 걷어낸다.
        /// </summary>
        /// <param name="source">걷어낼 수정자의 출처이며 null이면 아무것도 하지 않는다.</param>
        /// <returns>걷어낸 수정자 수이다.</returns>
        public int RemoveModifiersFrom(object source)
        {
            if (source == null)
            {
                return 0;
            }

            // 값 변화를 받은 쪽이 어트리뷰트를 더하거나 뺄 수 있으므로 순회 대상을 먼저 확정한다.
            var snapshot = new List<KeyValuePair<AttributeDefinition, AttributeEntry>>(_entries);
            var removedCount = 0;
            foreach (var pair in snapshot)
            {
                var modifiers = pair.Value.Modifiers;
                var removedHere = modifiers.RemoveAll(modifier => ReferenceEquals(modifier.Source, source));
                if (removedHere <= 0)
                {
                    continue;
                }

                removedCount += removedHere;

                // 앞선 알림에서 집합을 떠난 어트리뷰트는 다시 계산해 알리지 않는다.
                if (_entries.TryGetValue(pair.Key, out var currentEntry) && ReferenceEquals(currentEntry, pair.Value))
                {
                    Recompute(pair.Key, pair.Value);
                }
            }

            return removedCount;
        }

        /// <summary>지정한 어트리뷰트에 얹혀 있는 수정자 수를 가져온다.</summary>
        /// <param name="definition">조회할 어트리뷰트 정의이다.</param>
        /// <returns>수정자 수이며 담고 있지 않으면 0이다.</returns>
        public int GetModifierCount(AttributeDefinition definition)
        {
            return TryGetEntry(definition, out var entry) ? entry.Modifiers.Count : 0;
        }

        /// <summary>
        /// 저장에 쓸 기본값 묶음을 캡처한다. 수정자는 원인이 다시 만들어 주므로 담지 않는다.
        /// </summary>
        /// <returns>캡처한 스냅숏이다.</returns>
        public AttributeSetSnapshot CaptureSnapshot()
        {
            return AttributeSetSnapshot.Capture(this);
        }

        /// <summary>
        /// 스냅숏의 기본값을 현재 집합에 되돌린다.
        /// 집합에 없는 식별자는 건너뛰고, 스냅숏에 없는 어트리뷰트는 그대로 둔다.
        /// </summary>
        /// <param name="snapshot">복원할 스냅숏이다.</param>
        /// <returns>실제로 되돌린 어트리뷰트 수이다.</returns>
        public int RestoreSnapshot(AttributeSetSnapshot snapshot)
        {
            return snapshot == null ? 0 : snapshot.RestoreTo(this);
        }

        /// <summary>식별자가 일치하는 어트리뷰트 정의를 찾는다.</summary>
        /// <param name="id">찾을 식별자이다.</param>
        /// <param name="definition">찾은 정의이며 없으면 null이다.</param>
        /// <returns>찾았으면 true이다.</returns>
        public bool TryFindDefinition(string id, out AttributeDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                foreach (var candidate in _entries.Keys)
                {
                    if (string.Equals(candidate.Id, id, StringComparison.Ordinal))
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        /// <summary>값 변화 스트림을 닫는다. 이후의 변경은 통지되지 않는다.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _changed.Dispose();
        }

        /// <summary>지정한 수정자 하나를 걷어낸다.</summary>
        /// <param name="definition">수정자가 얹힌 어트리뷰트 정의이다.</param>
        /// <param name="modifier">걷어낼 수정자이다.</param>
        private void RemoveModifier(AttributeDefinition definition, AttributeModifier modifier)
        {
            if (!TryGetEntry(definition, out var entry) || !entry.Modifiers.Remove(modifier))
            {
                return;
            }

            Recompute(definition, entry);
        }

        /// <summary>등록된 필터를 등록 순서로 불러 제안된 기본값을 검토한다.</summary>
        /// <remarks>
        /// 필터는 실행 중 자신이나 다른 필터를 등록·해제할 수 있으므로 호출 시작 시점의 목록을 등록 순서로 순회한다.
        /// </remarks>
        /// <param name="definition">바뀌려는 어트리뷰트 정의이다.</param>
        /// <param name="currentBaseValue">지금의 기본값이다.</param>
        /// <param name="proposedBaseValue">바꾸려는 기본값이다.</param>
        /// <param name="context">이 변화의 원인이다.</param>
        /// <returns>필터를 모두 지난 기본값이다.</returns>
        private float ApplyBaseValueFilters(
            AttributeDefinition definition,
            float currentBaseValue,
            float proposedBaseValue,
            in AttributeChangeContext context)
        {
            if (!_baseValueFilters.TryGetValue(definition, out var filters) || filters.Count == 0)
            {
                return proposedBaseValue;
            }

            // 필터가 다른 필터를 등록하거나 뺄 수 있으므로 순회 대상을 먼저 확정한다.
            var snapshot = filters.ToArray();
            var value = proposedBaseValue;
            foreach (var filter in snapshot)
            {
                value = filter.FilterBaseValue(definition, currentBaseValue, value, in context);
            }

            return value;
        }

        /// <summary>현재값을 다시 계산하고, 값이 바뀌었으면 통지한 뒤 상한으로 삼은 쪽까지 갱신한다.</summary>
        /// <param name="definition">다시 계산할 어트리뷰트 정의이다.</param>
        /// <param name="entry">해당 어트리뷰트의 항목이다.</param>
        private void Recompute(AttributeDefinition definition, AttributeEntry entry)
        {
            Recompute(definition, entry, AttributeChangeContext.None);
        }

        /// <summary>현재값을 다시 계산하고, 값이 바뀌었으면 원인과 함께 통지한 뒤 상한으로 삼은 쪽까지 갱신한다.</summary>
        /// <param name="definition">다시 계산할 어트리뷰트 정의이다.</param>
        /// <param name="entry">해당 어트리뷰트의 항목이다.</param>
        /// <param name="context">이 변화의 원인이며 알림에 실린다.</param>
        private void Recompute(AttributeDefinition definition, AttributeEntry entry, in AttributeChangeContext context)
        {
            var previousValue = entry.CurrentValue;
            var nextValue = Compute(definition, entry);
            if (Mathf.Approximately(previousValue, nextValue))
            {
                return;
            }

            entry.CurrentValue = nextValue;
            if (!_isDisposed)
            {
                _changed.OnNext(new AttributeChangedEvent(definition, previousValue, nextValue, context));
            }

            RefreshDependents(definition);
        }

        /// <summary>이 어트리뷰트를 상한으로 삼는 어트리뷰트들에 상한 변화를 반영한다.</summary>
        /// <param name="capDefinition">상한 역할을 하는 어트리뷰트 정의이다.</param>
        private void RefreshDependents(AttributeDefinition capDefinition)
        {
            if (!_dependents.TryGetValue(capDefinition, out var dependents))
            {
                return;
            }

            // 반영 도중 목록이 바뀔 수 있으므로 복사본을 훑는다.
            var snapshot = dependents.ToArray();
            foreach (var dependent in snapshot)
            {
                if (!TryGetEntry(dependent, out var entry))
                {
                    continue;
                }

                ApplyCapChange(dependent, entry);
                Recompute(dependent, entry);
            }
        }

        /// <summary>상한이 바뀐 만큼 기본값을 정책에 맞게 손본다.</summary>
        /// <param name="definition">상한을 따르는 어트리뷰트 정의이다.</param>
        /// <param name="entry">해당 어트리뷰트의 항목이다.</param>
        private void ApplyCapChange(AttributeDefinition definition, AttributeEntry entry)
        {
            var previousCap = entry.LastCapValue;
            var currentCap = ResolveCapValue(definition);
            entry.LastCapValue = currentCap;
            if (!currentCap.HasValue)
            {
                return;
            }

            if (definition.CapPolicy == AttributeCapPolicy.PreserveRatio
                && previousCap.HasValue
                && previousCap.Value > 0f)
            {
                entry.BaseValue *= currentCap.Value / previousCap.Value;
            }

            entry.BaseValue = ClampBaseToBounds(definition, entry.BaseValue);
        }

        /// <summary>기본값을 하한과 상한 안으로 넣는다.</summary>
        /// <param name="definition">대상 어트리뷰트 정의이다.</param>
        /// <param name="value">넣을 값이다.</param>
        /// <returns>범위 안으로 넣은 값이다.</returns>
        private float ClampBaseToBounds(AttributeDefinition definition, float value)
        {
            var clamped = Mathf.Max(value, definition.MinValue);
            var cap = ResolveCapValue(definition);
            return cap.HasValue ? Mathf.Min(clamped, cap.Value) : clamped;
        }

        /// <summary>
        /// 계산 순서에 따라 현재값을 구한다.
        /// 더하기를 모두 합쳐 더하고, 곱하기를 모두 곱한 뒤, 덮어쓰기가 있으면 그 값으로 대체하고,
        /// 마지막에 하한과 상한으로 자른다.
        /// </summary>
        /// <param name="definition">계산할 어트리뷰트 정의이다.</param>
        /// <param name="entry">해당 어트리뷰트의 항목이다.</param>
        /// <returns>계산한 현재값이다.</returns>
        private float Compute(AttributeDefinition definition, AttributeEntry entry)
        {
            var additive = 0f;
            var multiplier = 1f;
            var hasOverride = false;
            var overrideValue = 0f;

            var modifiers = entry.Modifiers;
            for (var index = 0; index < modifiers.Count; index++)
            {
                var modifier = modifiers[index];
                switch (modifier.Operation)
                {
                    case AttributeModifierOperation.Add:
                        additive += modifier.Magnitude;
                        break;
                    case AttributeModifierOperation.Multiply:
                        multiplier *= modifier.Magnitude;
                        break;
                    case AttributeModifierOperation.Override:
                        // 나중에 얹은 덮어쓰기가 이긴다.
                        hasOverride = true;
                        overrideValue = modifier.Magnitude;
                        break;
                }
            }

            var value = hasOverride ? overrideValue : (entry.BaseValue + additive) * multiplier;
            value = Mathf.Max(value, definition.MinValue);
            var cap = ResolveCapValue(definition);
            return cap.HasValue ? Mathf.Min(value, cap.Value) : value;
        }

        /// <summary>
        /// 이 어트리뷰트에 적용할 상한을 구한다.
        /// 상한 어트리뷰트가 집합에 있으면 그 현재값을, 없으면 정의의 고정 상한을 쓰며, 둘 다 없으면 상한이 없다.
        /// </summary>
        /// <param name="definition">상한을 구할 어트리뷰트 정의이다.</param>
        /// <returns>상한 값이며 상한이 없으면 null이다.</returns>
        private float? ResolveCapValue(AttributeDefinition definition)
        {
            var capAttribute = definition.CapAttribute;
            if (capAttribute != null)
            {
                return TryGetEntry(capAttribute, out var capEntry) ? capEntry.CurrentValue : null;
            }

            return definition.HasMaxValue ? definition.MaxValue : null;
        }

        /// <summary>상한 관계를 역방향 색인에 등록한다.</summary>
        /// <param name="definition">등록할 어트리뷰트 정의이다.</param>
        private void RegisterDependency(AttributeDefinition definition)
        {
            var capAttribute = definition.CapAttribute;
            if (capAttribute == null)
            {
                return;
            }

            if (!_dependents.TryGetValue(capAttribute, out var dependents))
            {
                dependents = new List<AttributeDefinition>();
                _dependents.Add(capAttribute, dependents);
            }

            dependents.Add(definition);
        }

        /// <summary>상한 관계를 역방향 색인에서 뺀다.</summary>
        /// <param name="definition">해제할 어트리뷰트 정의이다.</param>
        private void UnregisterDependency(AttributeDefinition definition)
        {
            var capAttribute = definition.CapAttribute;
            if (capAttribute != null && _dependents.TryGetValue(capAttribute, out var dependents))
            {
                dependents.Remove(definition);
            }
        }

        /// <summary>상한 연결이 자기 자신으로 돌아오는지 확인한다.</summary>
        /// <param name="definition">확인할 어트리뷰트 정의이다.</param>
        /// <returns>순환이면 true이다.</returns>
        private static bool HasCapCycle(AttributeDefinition definition)
        {
            var visited = new HashSet<AttributeDefinition>();
            for (var current = definition; current != null; current = current.CapAttribute)
            {
                if (!visited.Add(current))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>정의에 해당하는 항목을 찾는다.</summary>
        /// <param name="definition">찾을 어트리뷰트 정의이다.</param>
        /// <param name="entry">찾은 항목이며 없으면 null이다.</param>
        /// <returns>찾았으면 true이다.</returns>
        private bool TryGetEntry(AttributeDefinition definition, out AttributeEntry entry)
        {
            if (definition == null)
            {
                entry = null;
                return false;
            }

            return _entries.TryGetValue(definition, out entry);
        }

        /// <summary>어트리뷰트 하나의 기본값, 수정자, 계산 결과를 담는다.</summary>
        private sealed class AttributeEntry
        {
            public AttributeEntry(float baseValue)
            {
                BaseValue = baseValue;
            }

            /// <summary>수정자를 반영하기 전의 값이며 저장 대상이다.</summary>
            public float BaseValue { get; set; }

            /// <summary>마지막으로 계산한 현재값이다.</summary>
            public float CurrentValue { get; set; }

            /// <summary>직전에 관찰한 상한이며 비율 유지 정책이 사용한다.</summary>
            public float? LastCapValue { get; set; }

            /// <summary>얹혀 있는 수정자 목록이며 얹은 순서를 유지한다.</summary>
            public List<AttributeModifier> Modifiers { get; } = new();
        }

        /// <summary>기본값 위에 얹혀 값을 바꾸는 수정자 하나이다.</summary>
        private readonly struct AttributeModifier : IEquatable<AttributeModifier>
        {
            public AttributeModifier(AttributeModifierOperation operation, float magnitude, object source)
            {
                Operation = operation;
                Magnitude = magnitude;
                Source = source;
                Token = new object();
            }

            /// <summary>값을 바꾸는 방식이다.</summary>
            public AttributeModifierOperation Operation { get; }

            /// <summary>수정자의 크기이다.</summary>
            public float Magnitude { get; }

            /// <summary>이 수정자를 얹은 원인이며 없으면 null이다.</summary>
            public object Source { get; }

            /// <summary>
            /// 같은 값을 가진 수정자끼리 구분하기 위한 표식이다.
            /// 이것이 없으면 크기와 연산이 같은 두 수정자를 걷어낼 때 엉뚱한 쪽이 지워진다.
            /// </summary>
            public object Token { get; }

            /// <inheritdoc />
            public bool Equals(AttributeModifier other) => ReferenceEquals(Token, other.Token);

            /// <inheritdoc />
            public override bool Equals(object obj) => obj is AttributeModifier other && Equals(other);

            /// <inheritdoc />
            public override int GetHashCode() => Token != null ? Token.GetHashCode() : 0;
        }

        /// <summary>해제하면 자신이 얹은 수정자 하나만 걷어내는 손잡이이다. 중복 해제는 무시된다.</summary>
        private sealed class ModifierHandle : IDisposable
        {
            private readonly AttributeDefinition _definition;
            private readonly AttributeModifier _modifier;
            private AttributeSet _owner;

            public ModifierHandle(AttributeSet owner, AttributeDefinition definition, AttributeModifier modifier)
            {
                _owner = owner;
                _definition = definition;
                _modifier = modifier;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                var owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                owner.RemoveModifier(_definition, _modifier);
            }
        }

        /// <summary>등록한 필터 하나를 정확히 되돌리는 손잡이이다.</summary>
        private sealed class FilterHandle : IDisposable
        {
            private readonly AttributeDefinition _definition;
            private readonly IAttributeBaseValueFilter _filter;
            private AttributeSet _owner;

            public FilterHandle(AttributeSet owner, AttributeDefinition definition, IAttributeBaseValueFilter filter)
            {
                _owner = owner;
                _definition = definition;
                _filter = filter;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                var owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                owner.RemoveBaseValueFilter(_definition, _filter);
            }
        }
    }
}
