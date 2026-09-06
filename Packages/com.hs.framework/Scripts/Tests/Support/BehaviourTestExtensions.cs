using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;

namespace HS.Framework.Tests.Support
{
    /// <summary>자리 하나만 떼어 놓고 실행해 보는 편의 수단이다.</summary>
    /// <remarks>
    /// 자리를 실행하려면 자기가 트리의 어디인지가 있어야 한다. 잎 하나를 확인하려는 검사가
    /// 그때마다 트리를 짓는 것은 검사가 보려는 것과 상관없는 준비이므로 여기로 모은다.
    /// 자식을 보는 자리는 이렇게 확인할 수 없다. 그런 것은 트리를 지어 확인해야 한다.
    /// </remarks>
    public static class BehaviourTestExtensions
    {
        /// <summary>그 자리를 홀로 한 번 실행한다.</summary>
        /// <param name="behaviour">실행할 자리이다.</param>
        /// <param name="context">노드들이 값을 주고받는 곳이다.</param>
        /// <returns>실행 결과이다.</returns>
        public static BehaviourStatus Tick(this IBehaviour behaviour, IBehaviourContext context)
        {
            var tree = new BehaviourTreeInstance();
            tree.SetRoot(behaviour);
            return tree.Tick(context);
        }
    }
}
