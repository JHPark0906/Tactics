using System;
using System.Collections.Generic;
using HS.Framework.Ability.Tags;

namespace HS.Framework.Ability.Effects
{
    /// <summary>
    /// 대상에게 적용되어 유지되고 있는 효과 하나의 기록이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 즉시 효과는 남지 않으므로 이 기록을 만들지 않는다. 지속과 무한 효과만 여기에 담긴다.
    /// </para>
    /// <para>
    /// <b>이 인스턴스 자체가 수정자의 출처로 쓰인다.</b> 효과를 적용할 때 얹는 수정자에 이 객체를 출처로 넘겨 두면,
    /// 만료 시 <c>RemoveModifiersFrom</c> 한 번으로 이 효과가 여러 어트리뷰트에 얹은 것을 모두 걷어낼 수 있다.
    /// 쌓지 않는 효과는 같은 정의로 두 번 적용해도 서로 다른 인스턴스가 되므로 하나가 만료돼도 다른 하나는 남는다.
    /// </para>
    /// <para>
    /// <b>쌓이는 효과는 기록 하나가 층수를 센다.</b> 층마다 얹은 수정자의 손잡이를 층 단위로 기억해 두어,
    /// 한 층만 걷을 때 그 층의 수정자만 정확히 되돌린다.
    /// </para>
    /// </remarks>
    public sealed class ActiveGameplayEffect
    {
        /// <summary>층마다 얹은 수정자 손잡이이며, 첫 층이 맨 앞이다.</summary>
        private readonly List<List<IDisposable>> _stackModifierHandles = new();

        // 정의 전체가 아니라 이 기록이 실제로 획득한 것만 되돌린다.
        internal readonly List<GameplayTag> GrantedTags = new();
        internal readonly List<GameplayTag> AppliedCueTags = new();
        internal int ResourceVersion;
        internal int ResourceChangeDepth;
        internal bool IsChangingResources => ResourceChangeDepth > 0;

        /// <summary>활성 효과 기록을 생성한다.</summary>
        /// <param name="definition">적용한 효과 정의이다.</param>
        /// <param name="source">이 효과를 적용한 원인이며 지정하지 않았으면 null이다.</param>
        /// <param name="context">적용하는 순간의 사정이며 없으면 null이다.</param>
        internal ActiveGameplayEffect(GameplayEffectDefinition definition, object source, GameplayEffectContext context)
        {
            Definition = definition;
            Source = source;
            Context = context;
            RemainingTime = definition.DurationPolicy == GameplayEffectDurationPolicy.Duration
                ? definition.Duration
                : 0f;
            IsActive = true;
            StackCount = 1;
        }

        /// <summary>적용한 효과 정의이다.</summary>
        public GameplayEffectDefinition Definition { get; }

        /// <summary>
        /// 이 효과를 적용한 원인이며 지정하지 않았으면 null이다.
        /// 한 원인이 건 효과를 한꺼번에 걷어낼 때 사용한다.
        /// </summary>
        public object Source { get; }

        /// <summary>
        /// 적용하는 순간의 사정이며 없으면 null이다.
        /// 주기 실행이 같은 사정으로 크기를 다시 정하고, 가해자를 어트리뷰트 변화에 실어 보내는 데 쓴다.
        /// </summary>
        public GameplayEffectContext Context { get; }

        /// <summary>
        /// 만료까지 남은 시간(초)이다. 무한 효과에서는 만료가 없으므로 항상 0이며 의미를 갖지 않는다.
        /// </summary>
        public float RemainingTime { get; internal set; }

        /// <summary>주기 실행까지 쌓인 시간(초)이다.</summary>
        internal float PeriodAccumulator { get; set; }

        /// <summary>아직 유지되고 있는지 여부이며, 만료되거나 제거되면 false가 된다.</summary>
        public bool IsActive { get; internal set; }

        /// <summary>쌓인 층수이며 쌓지 않는 효과에서는 언제나 1이다.</summary>
        public int StackCount { get; internal set; }

        /// <summary>
        /// 진행 조건이 깨져 작용을 멈춘 상태인지 여부이다. 억제된 동안 수정자와 부여 태그는 걷혀 있고 주기 실행은 쉬지만,
        /// 효과는 유지되고 지속 시간은 계속 흐른다.
        /// </summary>
        public bool IsInhibited { get; internal set; }

        /// <summary>만료가 정해져 있지 않은 효과인지 여부이다.</summary>
        public bool IsInfinite => Definition.DurationPolicy == GameplayEffectDurationPolicy.Infinite;

        /// <summary>한 층이 얹은 수정자 손잡이를 기억해 둔다.</summary>
        /// <param name="handles">그 층이 얹은 수정자의 손잡이이다.</param>
        internal void PushStackModifiers(List<IDisposable> handles)
        {
            _stackModifierHandles.Add(handles);
        }

        /// <summary>마지막 층이 얹은 수정자를 되돌린다. 기억해 둔 층이 없으면 아무 일도 하지 않는다.</summary>
        internal void PopStackModifiers()
        {
            if (_stackModifierHandles.Count == 0)
            {
                return;
            }

            var lastIndex = _stackModifierHandles.Count - 1;
            var handles = _stackModifierHandles[lastIndex];
            _stackModifierHandles.RemoveAt(lastIndex);
            List<Exception> failures = null;
            foreach (var handle in handles)
            {
                try
                {
                    handle.Dispose();
                }
                catch (Exception exception)
                {
                    failures ??= new List<Exception>();
                    failures.Add(exception);
                }
            }

            if (failures != null)
            {
                throw new AggregateException("층의 수정자를 정리했지만 일부 콜백이 실패했다.", failures).Flatten();
            }
        }

        /// <summary>기억해 둔 층을 모두 비운다. 수정자는 출처로 한 번에 걷어내므로 손잡이는 버리기만 한다.</summary>
        internal void ClearStackModifiers()
        {
            _stackModifierHandles.Clear();
        }

        /// <summary>정리 콜백 전에 손잡이 소유권을 기록에서 떼어 낸다.</summary>
        internal List<IDisposable> TakeStackModifiers()
        {
            var handles = new List<IDisposable>();
            foreach (var layer in _stackModifierHandles)
            {
                handles.AddRange(layer);
            }

            _stackModifierHandles.Clear();
            return handles;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var stateText = IsActive ? (IsInfinite ? "무한" : $"{RemainingTime:0.##}초 남음") : "제거됨";
            var stackText = StackCount > 1 ? $", {StackCount}층" : string.Empty;
            return $"{(Definition != null ? Definition.name : "None")} ({stateText}{stackText})";
        }
    }
}
