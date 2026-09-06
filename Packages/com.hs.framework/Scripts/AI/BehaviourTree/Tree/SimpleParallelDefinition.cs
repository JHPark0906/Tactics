using System;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>주된 일 하나와 곁에서 도는 가지 하나를 함께 돌리는 자리의 설명이다.</summary>
    /// <remarks>첫 자식이 주된 일이고 둘째 자식이 곁 가지이다. 순서가 곧 역할이다.</remarks>
    [Serializable]
    public sealed class SimpleParallelDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("주된 일이 끝났을 때 곁 가지를 바로 되돌릴지, 스스로 끝날 때까지 기다릴지이다.")]
        private SimpleParallelFinishMode finishMode = SimpleParallelFinishMode.Immediate;

        /// <inheritdoc />
        public override string DisplayName => $"SimpleParallel ({finishMode})";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => new SimpleParallelBehaviour(finishMode);
    }
}
