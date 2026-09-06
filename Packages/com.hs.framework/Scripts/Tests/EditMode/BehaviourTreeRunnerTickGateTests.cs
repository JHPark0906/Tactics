using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>실행기에 건 문이 닫혀 있는 동안 트리가 돌지 않고, 열리면 곧바로 도는 것을 고정한다.</summary>
    /// <remarks>
    /// 트리를 언제 돌려도 되는지는 그것을 둔 쪽이 안다. 실행기는 값을 받아 두지 않고 물을 때마다 묻는다.
    /// 값을 받아 두면 갱신하는 자리가 하나 더 생기고, 그 자리가 빠지면 문이 실제와 어긋난다.
    /// </remarks>
    public sealed class BehaviourTreeRunnerTickGateTests
    {
        private readonly List<GameObject> _created = new();
        private BehaviourTreeRunner _runner;
        private int _tickCount;

        [SetUp]
        public void SetUp()
        {
            var host = new GameObject("Runner");
            _created.Add(host);
            _runner = host.AddComponent<BehaviourTreeRunner>();
            _tickCount = 0;

            var tree = new BehaviourTreeInstance();
            tree.SetRoot(new ActionBehaviour(_ =>
            {
                _tickCount++;
                return BehaviourStatus.Running;
            }));
            _runner.Initialize(tree, new BehaviourContext());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void AClosedGateKeepsTheTreeFromTicking()
        {
            _runner.SetTickGate(() => false);

            _runner.Tick(1f);

            Assert.That(_tickCount, Is.EqualTo(0), "문이 닫혀 있으면 주기 틱은 트리를 돌리지 않는다.");
            Assert.That(_runner.LastStatus, Is.EqualTo(BehaviourStatus.Failure), "돈 것이 없으니 마지막 결과도 그대로다.");
        }

        [Test]
        public void TheTreeTicksOnTheFirstStepAfterTheGateOpens()
        {
            var open = false;
            _runner.SetTickGate(() => open);
            _runner.Tick(1f);

            open = true;
            _runner.Tick(1f);

            Assert.That(_tickCount, Is.EqualTo(1), "문이 열리는 스텝에 곧바로 한 번 돈다.");
            Assert.That(_runner.LastStatus, Is.EqualTo(BehaviourStatus.Running));
        }

        [Test]
        public void AnExplicitTickAlsoRespectsTheGate()
        {
            _runner.SetTickGate(() => false);

            var status = _runner.Tick();

            Assert.That(_tickCount, Is.EqualTo(0), "명시적 틱도 문을 본다.");
            Assert.That(status, Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void WithoutAGateTheRunnerTicksAsBefore()
        {
            _runner.Tick(1f);

            Assert.That(_tickCount, Is.EqualTo(1));
        }

        [Test]
        public void RemovingTheGateOpensIt()
        {
            _runner.SetTickGate(() => false);
            _runner.SetTickGate(null);

            _runner.Tick(1f);

            Assert.That(_tickCount, Is.EqualTo(1), "문을 떼면 언제나 돈다.");
        }
    }
}
