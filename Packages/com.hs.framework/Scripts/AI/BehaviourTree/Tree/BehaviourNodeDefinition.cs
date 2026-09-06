using System;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>에셋에 저장되는 한 자리의 설명이며, 실제로 도는 자리를 만들어 낸다.</summary>
    /// <remarks>
    /// <para>
    /// <b>설명과 실행을 나눈 까닭.</b> 도는 자리는 이동 수단이나 탐지기를 들고 있는데, 그런 것은
    /// 유닛마다 다르고 에셋에 담을 수 없다. 에셋은 무엇을 할지만 적고, 누가 할지는 만들 때 받는다.
    /// </para>
    /// <para>
    /// 분기를 공급하는 컴포넌트를 유닛에 붙이는 방식이라면 붙어 있다는 사실 자체가 설정이 되는데,
    /// 여기서는 트리에 그려져 있는 것이 곧 설정이다.
    /// </para>
    /// </remarks>
    [Serializable]
    public abstract class BehaviourNodeDefinition
    {
        /// <summary>편집기에서 이 자리를 부를 이름이다.</summary>
        public abstract string DisplayName { get; }

        /// <summary>실제로 도는 자리를 만든다.</summary>
        /// <param name="context">만들 때 필요한 것들이다.</param>
        /// <returns>만들어진 자리이며, 만들 수 없으면 null이다.</returns>
        public abstract IBehaviour CreateBehaviour(in BehaviourBuildContext context);
    }

    /// <summary>자식을 순서대로 실행하는 자리이다.</summary>
    [Serializable]
    public sealed class SequenceDefinition : BehaviourNodeDefinition
    {
        /// <inheritdoc />
        public override string DisplayName => "Sequence";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context) => new SequenceBehaviour();
    }

    /// <summary>자식을 순서대로 시도하는 자리이다.</summary>
    [Serializable]
    public sealed class SelectorDefinition : BehaviourNodeDefinition
    {
        /// <inheritdoc />
        public override string DisplayName => "Selector";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context) => new SelectorBehaviour();
    }

    /// <summary>문맥의 그 키를 보고 아래를 막거나 통과시키는 자리이다.</summary>
    [Serializable]
    public sealed class ContextValueConditionDefinition : BehaviourNodeDefinition
    {
        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("지켜볼 문맥 키이다.")]
        private string key;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("값이 있어야 통과시키면 켠다. 끄면 없어야 통과시킨다.")]
        private bool requiresValue = true;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("값이 바뀌었을 때 무엇을 끊을지이다.")]
        private BehaviourAbortScope abortScope = BehaviourAbortScope.None;

        /// <inheritdoc />
        public override string DisplayName => string.IsNullOrWhiteSpace(key) ? "Condition" : $"Condition: {key}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(key)
                ? null
                : new ContextValueConditionBehaviour(key, requiresValue, abortScope);
    }
}
