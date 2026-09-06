using System;
using HS.Framework.AI.BehaviourTree;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>두 값을 어떻게 견줄지이다.</summary>
    public enum ContextComparison
    {
        /// <summary>같으면 참이다.</summary>
        Equal,

        /// <summary>다르면 참이다.</summary>
        NotEqual,

        /// <summary>앞이 뒤보다 작으면 참이다. 수에만 뜻이 있다.</summary>
        Less,

        /// <summary>앞이 뒤보다 작거나 같으면 참이다. 수에만 뜻이 있다.</summary>
        LessOrEqual,

        /// <summary>앞이 뒤보다 크면 참이다. 수에만 뜻이 있다.</summary>
        Greater,

        /// <summary>앞이 뒤보다 크거나 같으면 참이다. 수에만 뜻이 있다.</summary>
        GreaterOrEqual
    }

    /// <summary>견주는 규칙을 한곳에 둔다.</summary>
    internal static class ContextComparisons
    {
        /// <summary>두 수를 견준다.</summary>
        /// <param name="left">앞의 수이다.</param>
        /// <param name="comparison">견주는 방법이다.</param>
        /// <param name="right">뒤의 수이다.</param>
        /// <returns>규칙에 맞으면 참이다.</returns>
        internal static bool Compare(double left, ContextComparison comparison, double right)
            => comparison switch
            {
                ContextComparison.Equal => Math.Abs(left - right) <= double.Epsilon,
                ContextComparison.NotEqual => Math.Abs(left - right) > double.Epsilon,
                ContextComparison.Less => left < right,
                ContextComparison.LessOrEqual => left <= right,
                ContextComparison.Greater => left > right,
                ContextComparison.GreaterOrEqual => left >= right,
                _ => false
            };

        /// <summary>수가 아닌 두 값을 견준다. 같고 다름만 뜻이 있다.</summary>
        /// <param name="left">앞의 값이다.</param>
        /// <param name="comparison">견주는 방법이다.</param>
        /// <param name="right">뒤의 값이다.</param>
        /// <returns>규칙에 맞으면 참이다. 크기를 견주면 언제나 거짓이다.</returns>
        internal static bool Compare(object left, ContextComparison comparison, object right)
            => comparison switch
            {
                ContextComparison.Equal => Equals(left, right),
                ContextComparison.NotEqual => !Equals(left, right),
                _ => false
            };
    }

    /// <summary>문맥의 두 키에 담긴 값을 견주어 맞을 때만 아래를 실행하게 한다.</summary>
    /// <remarks>
    /// 둘 다 수이면 크기까지 견주고, 아니면 같고 다름만 본다. 둘 다 없으면 같은 것으로 본다.
    /// 어느 키가 바뀌어도 끊을 수 있도록 둘 다 지켜본다.
    /// </remarks>
    public sealed class CompareContextValuesBehaviour : ObservingConditionBehaviour
    {
        /// <summary>앞의 키이다.</summary>
        public string LeftKey { get; }

        /// <summary>뒤의 키이다.</summary>
        public string RightKey { get; }

        /// <summary>견주는 방법이다.</summary>
        public ContextComparison Comparison { get; }

        /// <summary>견줄 두 키와 방법, 끊을 범위를 지정한다.</summary>
        /// <param name="leftKey">앞의 문맥 키이다.</param>
        /// <param name="rightKey">뒤의 문맥 키이다.</param>
        /// <param name="comparison">견주는 방법이다.</param>
        /// <param name="abortScope">값이 바뀌었을 때 무엇을 끊을지이다.</param>
        public CompareContextValuesBehaviour(
            string leftKey,
            string rightKey,
            ContextComparison comparison = ContextComparison.Equal,
            BehaviourAbortScope abortScope = BehaviourAbortScope.None)
            : base(abortScope, RequireKey(leftKey, nameof(leftKey)), RequireKey(rightKey, nameof(rightKey)))
        {
            LeftKey = leftKey;
            RightKey = rightKey;
            Comparison = comparison;
        }

        /// <inheritdoc />
        protected override bool Evaluate(in BehaviourTickContext context)
        {
            var values = context.Context;
            if (ContextValueReader.TryGetNumber(values, LeftKey, out var left)
                && ContextValueReader.TryGetNumber(values, RightKey, out var right))
            {
                return ContextComparisons.Compare(left, Comparison, right);
            }

            values.TryGetValue<object>(LeftKey, out var leftValue);
            values.TryGetValue<object>(RightKey, out var rightValue);
            return ContextComparisons.Compare(leftValue, Comparison, rightValue);
        }
    }

    /// <summary>문맥의 키에 담긴 수를 정해진 수와 견주어 맞을 때만 아래를 실행하게 한다.</summary>
    /// <remarks>값이 없거나 수가 아니면 막는다.</remarks>
    public sealed class ContextNumberConditionBehaviour : ObservingConditionBehaviour
    {
        /// <summary>지켜보는 키이다.</summary>
        public string Key { get; }

        /// <summary>견주는 방법이다.</summary>
        public ContextComparison Comparison { get; }

        /// <summary>견줄 수이다.</summary>
        public float Value { get; }

        /// <summary>지켜볼 키와 견줄 수, 끊을 범위를 지정한다.</summary>
        /// <param name="key">확인할 문맥 키이다.</param>
        /// <param name="comparison">견주는 방법이다.</param>
        /// <param name="value">견줄 수이다.</param>
        /// <param name="abortScope">값이 바뀌었을 때 무엇을 끊을지이다.</param>
        public ContextNumberConditionBehaviour(
            string key,
            ContextComparison comparison,
            float value,
            BehaviourAbortScope abortScope = BehaviourAbortScope.None)
            : base(abortScope, RequireKey(key, nameof(key)))
        {
            Key = key;
            Comparison = comparison;
            Value = value;
        }

        /// <inheritdoc />
        protected override bool Evaluate(in BehaviourTickContext context)
            => ContextValueReader.TryGetNumber(context.Context, Key, out var number)
               && ContextComparisons.Compare(number, Comparison, Value);
    }
}
