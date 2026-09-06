using System;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>자식의 성공과 실패를 뒤집는 자리의 설명이다.</summary>
    /// <remarks>감쌀 자식은 트리에서 자기 아래 첫 자식이므로 여기서 정할 것이 없다.</remarks>
    [Serializable]
    public sealed class InverterDefinition : BehaviourNodeDefinition
    {
        /// <inheritdoc />
        public override string DisplayName => "Inverter";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context) => new InverterBehaviour();
    }

    /// <summary>자식이 끝나기만 하면 성공으로 바꾸는 자리의 설명이다.</summary>
    [Serializable]
    public sealed class SucceederDefinition : BehaviourNodeDefinition
    {
        /// <inheritdoc />
        public override string DisplayName => "Succeeder";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context) => new SucceederBehaviour();
    }

    /// <summary>자식을 정해진 횟수만큼, 또는 끝없이 되풀이하는 자리의 설명이다.</summary>
    [Serializable]
    public sealed class RepeaterDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Min(0)]
        [Tooltip("되풀이할 횟수이다. 0이면 끝없이 되풀이한다.")]
        private int repeatCount = RepeaterBehaviour.InfiniteRepeats;

        /// <inheritdoc />
        public override string DisplayName
            => repeatCount == RepeaterBehaviour.InfiniteRepeats ? "Repeat ∞" : $"Repeat ×{repeatCount}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => new RepeaterBehaviour(repeatCount);
    }
}
