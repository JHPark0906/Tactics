using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>조건이 바뀌면 실행 중인 가지를 끊는 규칙을 고정한다.</summary>
    /// <remarks>
    /// 이것이 없으면 매 틱 뿌리부터 다시 훑어야 달라진 조건이 반영된다. 끊을 수 있으면 실행 중인
    /// 자리에 머물러 있다가 필요한 순간에만 되돌아간다.
    /// </remarks>
    public sealed class AbortScopeTests
    {
        [Test]
        public void ADecoratorWithNoScopeLeavesTheStoppingToTheGate()
        {
            var context = new BehaviourContext();
            context.SetValue("target", 1);
            var running = new ResetCountingBehaviour();
            var resetCountSeenByTheFirstSibling = -1;
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            tree.AddChild(root, new ActionBehaviour(_ =>
            {
                resetCountSeenByTheFirstSibling = running.ResetCount;
                return BehaviourStatus.Failure;
            }));
            var guard = tree.AddChild(root, new ContextValueConditionBehaviour("target", true, BehaviourAbortScope.None));
            var runningNode = tree.AddChild(guard, running);

            tree.Tick(context);
            Assert.That(tree.IsRunning(runningNode), Is.True);

            context.RemoveValue("target");
            Assert.That(running.ResetCount, Is.EqualTo(0), "끊지 않기로 했으면 값이 바뀐 순간에는 아무것도 되돌리지 않는다.");

            tree.Tick(context);

            Assert.That(resetCountSeenByTheFirstSibling, Is.EqualTo(0),
                "끊기가 걸리지 않았으므로 실행 앞에서 되돌려지는 것이 없다. 앞엣 형제가 돌 때까지 그대로다.");
            Assert.That(running.ResetCount, Is.EqualTo(1), "그 자리에 다시 이르렀을 때 문이 막으며 그 아래를 되돌린다.");
            Assert.That(tree.IsRunning(runningNode), Is.False);
        }

        [Test]
        public void SelfScopeStopsItsOwnBranchWhenTheConditionTurnsFalse()
        {
            var context = new BehaviourContext();
            context.SetValue("target", 1);
            var tree = Build(BehaviourAbortScope.Self, out var running, out _);

            tree.Tick(context);
            Assert.That(tree.IsRunning(running), Is.True);

            context.RemoveValue("target");
            tree.Tick(context);

            Assert.That(tree.IsRunning(running), Is.False,
                "조건이 거짓이 되었는데 그 아래가 계속 돌면 안 된다.");
        }

        [Test]
        public void LowerPriorityScopeTakesTheTurnBackWhenTheConditionTurnsTrue()
        {
            var context = new BehaviourContext();
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());

            var guard = tree.AddChild(root,
                new ContextValueConditionBehaviour("target", true, BehaviourAbortScope.LowerPriority));
            var guarded = tree.AddChild(guard, new ActionBehaviour(_ => BehaviourStatus.Running));
            var fallback = tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(context);
            Assert.That(tree.IsRunning(fallback), Is.True, "조건이 거짓이라 뒤엣것이 돈다.");

            context.SetValue("target", 1);
            tree.Tick(context);

            Assert.That(tree.IsRunning(guarded), Is.True, "앞에 있는 자리가 곧 높은 우선순위이다.");
            Assert.That(tree.IsRunning(fallback), Is.False);
        }

        [Test]
        public void WritingTheSameValueDoesNotStopAnything()
        {
            var context = new BehaviourContext();
            context.SetValue("target", 1);
            var tree = Build(BehaviourAbortScope.Self, out var running, out _);

            tree.Tick(context);
            context.SetValue("target", 1);
            tree.Tick(context);

            Assert.That(tree.IsRunning(running), Is.True,
                "매 틱 같은 값을 다시 쓰는 자리가 실행 중인 가지를 계속 끊으면 안 된다.");
        }

        [Test]
        public void ADecoratorStopsWatchingOnceItsBranchIsNoLongerReachable()
        {
            var context = new BehaviourContext();
            context.SetValue("target", 1);
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            var first = tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Running));
            var guard = tree.AddChild(root,
                new ContextValueConditionBehaviour("target", true, BehaviourAbortScope.Self));
            tree.AddChild(guard, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(context);

            Assert.That(tree.IsRunning(first), Is.True);
            Assert.That(tree.IsRunning(guard), Is.False,
                "앞엣것이 도는 동안 뒤엣 조건은 실행되지 않는다.");
        }

        private static BehaviourTreeInstance Build(
            BehaviourAbortScope scope,
            out HS.Framework.Foundation.Collections.TreeNode<IBehaviour> running,
            out HS.Framework.Foundation.Collections.TreeNode<IBehaviour> guard)
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            guard = tree.AddChild(root, new ContextValueConditionBehaviour("target", true, scope));
            running = tree.AddChild(guard, new ActionBehaviour(_ => BehaviourStatus.Running));
            return tree;
        }

        private sealed class ResetCountingBehaviour : IBehaviour
        {
            public int ResetCount { get; private set; }

            public BehaviourStatus Tick(in BehaviourTickContext context) => BehaviourStatus.Running;

            public void Reset() => ResetCount++;
        }
    }
}
