using System;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>문맥의 두 키에 담긴 값을 견주어 아래를 막거나 통과시키는 자리의 설명이다.</summary>
    [Serializable]
    public sealed class CompareContextValuesDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("앞의 문맥 키이다.")]
        private string leftKey;

        [SerializeField]
        [Tooltip("뒤의 문맥 키이다.")]
        private string rightKey;

        [SerializeField]
        [Tooltip("견주는 방법이다. 둘 다 수일 때만 크기를 견줄 수 있다.")]
        private ContextComparison comparison = ContextComparison.Equal;

        [SerializeField]
        [Tooltip("어느 값이든 바뀌었을 때 무엇을 끊을지이다.")]
        private BehaviourAbortScope abortScope = BehaviourAbortScope.None;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(leftKey) || string.IsNullOrWhiteSpace(rightKey)
                ? "Compare"
                : $"Compare: {leftKey} {comparison} {rightKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(leftKey) || string.IsNullOrWhiteSpace(rightKey)
                ? null
                : new CompareContextValuesBehaviour(leftKey, rightKey, comparison, abortScope);
    }

    /// <summary>문맥의 키에 담긴 수를 정해진 수와 견주어 아래를 막거나 통과시키는 자리의 설명이다.</summary>
    [Serializable]
    public sealed class ContextNumberConditionDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("지켜볼 문맥 키이다. 수가 담겨 있어야 한다.")]
        private string key;

        [SerializeField]
        [Tooltip("견주는 방법이다.")]
        private ContextComparison comparison = ContextComparison.Equal;

        [SerializeField]
        [Tooltip("견줄 수이다.")]
        private float value;

        [SerializeField]
        [Tooltip("값이 바뀌었을 때 무엇을 끊을지이다.")]
        private BehaviourAbortScope abortScope = BehaviourAbortScope.None;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(key) ? "Number" : $"Number: {key} {comparison} {value:0.##}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(key)
                ? null
                : new ContextNumberConditionBehaviour(key, comparison, value, abortScope);
    }
}
