using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>끊기가 실행의 어느 자리에서 적용되고, 무엇까지 되돌리는지를 고정한다.</summary>
    /// <remarks>
    /// <para>
    /// 걸어 둔 끊기는 뿌리가 돌기 <b>앞에서</b> 적용되고, 지켜볼 자리는 뿌리가 돌고 난 <b>뒤에</b>
    /// 셈해진다. 어느 한쪽이 빠지면 조건 자리는 값이 바뀌어도 아무것도 끊지 못하는데, 그 고장은
    /// 컴파일도 되고 예외도 내지 않는다.
    /// </para>
    /// <para>
    /// 자기를 끊는 범위는 자기 아래만 되돌린다. 부모까지 되돌리면 이미 끝난 형제가 다시 돈다.
    /// </para>
    /// </remarks>
    public sealed class AbortWiringTests
    {
        [Test]
        public void PendingAbortsAreAppliedBeforeTheRootTicks()
        {
            var context = new BehaviourContext();
            var visited = new List<string>();
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            var guard = tree.AddChild(root,
                new ContextValueConditionBehaviour("target", true, BehaviourAbortScope.LowerPriority));
            var guarded = tree.AddChild(guard, new ActionBehaviour(_ => BehaviourStatus.Running));
            tree.AddChild(root, new ActionBehaviour(_ =>
            {
                visited.Add("fallback");
                return BehaviourStatus.Running;
            }));

            tree.Tick(context);
            Assert.That(visited, Is.EqualTo(new[] { "fallback" }));

            context.SetValue("target", 1);
            visited.Clear();
            tree.Tick(context);

            Assert.That(tree.IsRunning(guarded), Is.True);
            Assert.That(visited, Is.Empty, "끊기가 실행 앞에서 적용되면 뒤엣것은 그 실행에서 돌지 않는다.");
        }

        [Test]
        public void SelfScopeLeavesFinishedSiblingsAlone()
        {
            var context = new BehaviourContext();
            context.SetValue("target", 1);
            var first = new CountingBehaviour(BehaviourStatus.Success);
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SequenceBehaviour());
            tree.AddChild(root, first);
            var guard = tree.AddChild(root,
                new ContextValueConditionBehaviour("target", true, BehaviourAbortScope.Self));
            var running = tree.AddChild(guard, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(context);
            Assert.That(first.TickCount, Is.EqualTo(1));
            Assert.That(tree.IsRunning(running), Is.True);

            context.RemoveValue("target");
            var status = tree.Tick(context);

            Assert.That(status, Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(tree.IsRunning(running), Is.False);
            Assert.That(first.TickCount, Is.EqualTo(1),
                "자기를 끊을 때 부모까지 되돌리면 이미 끝난 형제가 다시 돈다.");

            tree.Tick(context);

            Assert.That(first.TickCount, Is.EqualTo(2), "실패가 위로 오른 뒤에는 순차 자리가 처음부터 다시 한다.");
        }

        [Test]
        public void ChildrenOfTheRunningPathBecomeRelevantAfterTheTick()
        {
            var context = new BehaviourContext();
            var first = new CountingBehaviour(BehaviourStatus.Running);
            var probe = new ObservingProbe();
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            tree.AddChild(root, first);
            tree.AddChild(root, probe);

            tree.Tick(context);

            Assert.That(probe.BecameRelevantCount, Is.EqualTo(1),
                "부모가 실행 중이면 그 자식은 지켜볼 자리이다.");
            Assert.That(probe.CeasedRelevantCount, Is.EqualTo(0));

            first.Status = BehaviourStatus.Success;
            tree.Tick(context);

            Assert.That(probe.CeasedRelevantCount, Is.EqualTo(1),
                "부모가 끝나 실행 경로에서 빠지면 그 자식은 지켜보기를 그만둔다.");
        }

        [Test]
        public void AGuardUnsubscribesOnceItsParentStopsRunning()
        {
            var context = new SubscriptionCountingContext();
            context.SetValue("target", 1);
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            var guard = tree.AddChild(root,
                new ContextValueConditionBehaviour("target", true, BehaviourAbortScope.Self));
            tree.AddChild(guard, new ActionBehaviour(_ => BehaviourStatus.Running));
            tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Success));

            tree.Tick(context);
            Assert.That(context.LiveSubscriptions, Is.EqualTo(1), "부모가 도는 동안 조건 자리는 값을 지켜본다.");

            context.RemoveValue("target");
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success), "조건이 닫히면 뒤엣것으로 넘어간다.");

            Assert.That(context.LiveSubscriptions, Is.EqualTo(0),
                "뿌리가 끝나 실행 경로가 비면 조건 자리는 지켜보기를 그만둔다. 걸어 둔 채로 두면 실행 중이지도 않은 자리가 트리를 끊는다.");
            Assert.That(tree.IsRunning(guard), Is.False);
        }

        /// <summary>살아 있는 구독 수를 세는 문맥이다. 지켜보기를 그만두었는지는 이것으로만 볼 수 있다.</summary>
        private sealed class SubscriptionCountingContext : IBehaviourContext
        {
            private readonly BehaviourContext _inner = new();

            public int LiveSubscriptions { get; private set; }

            public bool TryGetValue<T>(string key, out T value) => _inner.TryGetValue(key, out value);

            public void SetValue<T>(string key, T value) => _inner.SetValue(key, value);

            public bool RemoveValue(string key) => _inner.RemoveValue(key);

            public System.IDisposable Observe(string key, System.Action<string> onChanged)
            {
                LiveSubscriptions++;
                return new Subscription(this, _inner.Observe(key, onChanged));
            }

            private sealed class Subscription : System.IDisposable
            {
                private SubscriptionCountingContext _owner;
                private readonly System.IDisposable _inner;

                public Subscription(SubscriptionCountingContext owner, System.IDisposable inner)
                {
                    _owner = owner;
                    _inner = inner;
                }

                public void Dispose()
                {
                    if (_owner == null)
                    {
                        return;
                    }

                    _owner.LiveSubscriptions--;
                    _owner = null;
                    _inner.Dispose();
                }
            }
        }

        private sealed class CountingBehaviour : IBehaviour
        {
            public BehaviourStatus Status { get; set; }

            public int TickCount { get; private set; }

            public CountingBehaviour(BehaviourStatus status) => Status = status;

            public BehaviourStatus Tick(in BehaviourTickContext context)
            {
                TickCount++;
                return Status;
            }

            public void Reset()
            {
            }
        }

        private sealed class ObservingProbe : IBehaviour, IObservingBehaviour
        {
            public int BecameRelevantCount { get; private set; }

            public int CeasedRelevantCount { get; private set; }

            public BehaviourStatus Tick(in BehaviourTickContext context) => BehaviourStatus.Failure;

            public void Reset()
            {
            }

            public void OnBecomeRelevant(in BehaviourTickContext context) => BecameRelevantCount++;

            public void OnCeaseRelevant() => CeasedRelevantCount++;
        }
    }
}
