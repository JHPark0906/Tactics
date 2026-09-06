using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>조건 자리가 지켜보기 시작하는 순간, 그 사이 바뀐 값을 놓치지 않는 것을 고정한다.</summary>
    /// <remarks>
    /// 지켜보기는 실행이 끝난 뒤에 시작된다. 같은 실행 안에서 조건 자리 뒤에 도는 자리가 값을 쓰면
    /// 그 변화는 알림으로 오지 않으므로, 조건이 막았던 값이 통과시키는 값이 되었는데도 뒤엣 형제가
    /// 차례를 쥔 채 조건은 다시 불리지 않는다. 지켜보기 시작할 때 한 번 견주어 그 틈을 막는다.
    /// </remarks>
    public sealed class AbortOnRelevanceTests
    {
        [Test]
        public void AGateThatWasClosedTakesTheTurnWhenALaterSiblingOpensItInTheSameTick()
        {
            var context = new BehaviourContext();
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            var guard = tree.AddChild(root,
                new ContextValueConditionBehaviour("key", true, BehaviourAbortScope.LowerPriority));
            var guarded = tree.AddChild(guard, new ActionBehaviour(_ => BehaviourStatus.Running));
            var writer = tree.AddChild(root,
                new ActionServiceBehaviour(0f, values => values.SetValue("key", 1), () => 0f));
            var fallback = tree.AddChild(writer, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(context);
            Assert.That(tree.IsRunning(fallback), Is.True, "문이 막힌 실행에서는 뒤엣 형제가 돈다.");

            tree.Tick(context);

            Assert.That(tree.IsRunning(guarded), Is.True,
                "뒤엣 형제가 같은 실행에서 연 값은 알림으로 오지 않는다. 지켜보기 시작할 때 견주어 차례를 되찾아야 한다.");
            Assert.That(tree.IsRunning(fallback), Is.False);
        }

        [Test]
        public void ASequenceGateThatPassedDoesNotRestartWhenALaterSiblingRuns()
        {
            var context = new BehaviourContext();
            context.SetValue("key", 1);
            var visited = new List<string>();
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SequenceBehaviour());
            var guard = tree.AddChild(root,
                new ContextValueConditionBehaviour("key", true, BehaviourAbortScope.LowerPriority));
            tree.AddChild(guard, new ActionBehaviour(_ =>
            {
                visited.Add("a");
                return BehaviourStatus.Success;
            }));
            var later = tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(context);
            tree.Tick(context);
            tree.Tick(context);

            Assert.That(visited, Is.EqualTo(new[] { "a" }),
                "통과한 문의 뒤엣 형제가 도는 것은 차례를 빼앗긴 것이 아니라 제 몫을 마친 것이다.");
            Assert.That(tree.IsRunning(later), Is.True);
        }

        [Test]
        public void AGateThatWasNotEvaluatedThisTickRequestsNothing()
        {
            var context = new BehaviourContext();
            context.SetValue("key", 1);
            var first = new ResetCountingBehaviour();
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            var firstNode = tree.AddChild(root, first);
            var guard = tree.AddChild(root,
                new ContextValueConditionBehaviour("key", true, BehaviourAbortScope.LowerPriority));
            tree.AddChild(guard, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(context);
            tree.Tick(context);

            Assert.That(first.ResetCount, Is.EqualTo(0), "문을 본 적이 없으면 견줄 바탕이 없으니 아무것도 걸지 않는다.");
            Assert.That(tree.IsRunning(firstNode), Is.True);
        }

        private sealed class ResetCountingBehaviour : IBehaviour
        {
            public int ResetCount { get; private set; }

            public BehaviourStatus Tick(in BehaviourTickContext context) => BehaviourStatus.Running;

            public void Reset() => ResetCount++;
        }
    }
}
