using HS.Framework.AI.BehaviourTree;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>에셋에 적힌 것을 실제로 도는 자리로 만들 때 필요한 것들이다.</summary>
    /// <remarks>
    /// 에셋은 무엇을 할지만 적어 두고, 그것을 누가 하는지는 모른다. 이동 수단이나 탐지기 같은
    /// 것은 유닛마다 다르므로 만들 때 받아야 한다.
    /// </remarks>
    public readonly struct BehaviourBuildContext
    {
        /// <summary>이 트리를 쓸 유닛이다. 필요한 구성요소는 여기서 찾는다.</summary>
        public GameObject Owner { get; }

        /// <summary>노드들이 값을 주고받을 곳이다.</summary>
        public IBehaviourContext Context { get; }

        /// <summary>만들 때 필요한 것을 모은다.</summary>
        /// <param name="owner">이 트리를 쓸 유닛이다.</param>
        /// <param name="context">값을 주고받을 곳이다.</param>
        public BehaviourBuildContext(GameObject owner, IBehaviourContext context)
        {
            Owner = owner;
            Context = context;
        }
    }
}
