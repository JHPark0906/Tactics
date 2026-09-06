using System.Collections.Generic;
using System.Reflection;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>자리와 방향을 보는 조건들이 문을 어떻게 여닫는지 고정한다.</summary>
    public sealed class SpatialConditionBehaviourTests
    {
        private readonly List<Object> _created = new();

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
        public void AConeCheckOpensWhenTheObservedIsInsideTheCone()
        {
            var context = new BehaviourContext();
            context.SetValue("origin", Vector3.zero);
            context.SetValue("direction", Vector3.forward);
            context.SetValue("observed", new Vector3(0f, 0f, 5f));

            Assert.That(Gate(new ConeCheckBehaviour("origin", "direction", "observed", 45f), context), Is.EqualTo(BehaviourStatus.Success));

            context.SetValue("observed", new Vector3(5f, 0f, 0f));
            Assert.That(Gate(new ConeCheckBehaviour("origin", "direction", "observed", 45f), context), Is.EqualTo(BehaviourStatus.Failure),
                "가운데에서 90도 벌어진 것은 45도 부채꼴 밖이다.");
            Assert.That(Gate(new ConeCheckBehaviour("origin", "direction", "observed", 90f), context), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void AConeCheckReadsTheDirectionFromATransform()
        {
            var eye = Track(new GameObject("Eye")).transform;
            eye.rotation = Quaternion.LookRotation(Vector3.right);
            var context = new BehaviourContext();
            context.SetValue("origin", eye);
            context.SetValue("direction", eye);
            context.SetValue("observed", new Vector3(5f, 0f, 0.5f));

            Assert.That(Gate(new ConeCheckBehaviour("origin", "direction", "observed", 30f), context), Is.EqualTo(BehaviourStatus.Success),
                "Transform이 담겨 있으면 그것이 바라보는 쪽이 방향이다.");
        }

        [Test]
        public void AConeCheckIsClosedWhenAKeyIsMissingAndOpenAtTheOrigin()
        {
            var context = new BehaviourContext();
            context.SetValue("origin", Vector3.zero);
            context.SetValue("direction", Vector3.forward);

            Assert.That(Gate(new ConeCheckBehaviour("origin", "direction", "observed", 45f), context), Is.EqualTo(BehaviourStatus.Failure));

            context.SetValue("observed", Vector3.zero);
            Assert.That(Gate(new ConeCheckBehaviour("origin", "direction", "observed", 45f), context), Is.EqualTo(BehaviourStatus.Success),
                "꼭짓점과 같은 자리는 안이다.");
        }

        [Test]
        public void KeepInConeCutsTheChildOffWhenTheObservedLeavesTheInitialCone()
        {
            var observed = Track(new GameObject("Observed")).transform;
            observed.position = new Vector3(0f, 0f, 5f);
            var mover = new CountingMover();
            var context = new BehaviourContext();
            context.SetValue("origin", Vector3.zero);
            context.SetValue("observed", observed);
            context.SetValue("destination", Vector3.one);
            var tree = new BehaviourTreeInstance();
            var keep = tree.SetRoot(new KeepInConeBehaviour("origin", "observed", 45f));
            tree.AddChild(keep, new MoveToPositionBehaviour(mover, "destination"));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running));

            observed.position = new Vector3(1f, 0f, 5f);
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running), "조금 움직인 것은 아직 안이다.");

            observed.position = new Vector3(5f, 0f, 0f);
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure), "처음 방향에서 벗어나면 막는다.");
            Assert.That(mover.StopCount, Is.EqualTo(1), "막히면서 돌던 아래는 되돌려진다.");

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running), "다시 들어오면 지금 방향이 새 기준이다.");
        }

        [Test]
        public void KeepInConeIsClosedWhenTheKeysAreMissing()
        {
            Assert.That(Gate(new KeepInConeBehaviour("origin", "observed", 45f), new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void IsAtLocationOpensWithinTheRadius()
        {
            var self = Track(new GameObject("Self")).transform;
            var context = new BehaviourContext();
            context.SetValue("location", new Vector3(0f, 0f, 0.3f));

            Assert.That(Gate(new IsAtLocationBehaviour(self, "location", 0.5f), context), Is.EqualTo(BehaviourStatus.Success));

            context.SetValue("location", new Vector3(0f, 0f, 2f));
            Assert.That(Gate(new IsAtLocationBehaviour(self, "location", 0.5f), context), Is.EqualTo(BehaviourStatus.Failure));

            var marker = Track(new GameObject("Marker")).transform;
            marker.position = new Vector3(0.1f, 0f, 0f);
            context.SetValue("location", marker);
            Assert.That(Gate(new IsAtLocationBehaviour(self, "location", 0.5f), context), Is.EqualTo(BehaviourStatus.Success),
                "자리는 Transform으로도 읽는다.");
        }

        [Test]
        public void DoesPathExistAsksFromTheUnitWhenNoStartKeyIsGiven()
        {
            var self = Track(new GameObject("Self")).transform;
            self.position = new Vector3(1f, 0f, 1f);
            var asked = new List<(Vector3 From, Vector3 To)>();
            var context = new BehaviourContext();
            context.SetValue("goal", new Vector3(5f, 0f, 5f));

            var status = Gate(new DoesPathExistBehaviour(self, null, "goal", (from, to) =>
            {
                asked.Add((from, to));
                return true;
            }), context);

            Assert.That(status, Is.EqualTo(BehaviourStatus.Success));
            Assert.That(asked, Is.EqualTo(new[] { (new Vector3(1f, 0f, 1f), new Vector3(5f, 0f, 5f)) }));
        }

        [Test]
        public void DoesPathExistReadsTheStartFromTheContextWhenGiven()
        {
            var self = Track(new GameObject("Self")).transform;
            var asked = new List<(Vector3 From, Vector3 To)>();
            var context = new BehaviourContext();
            context.SetValue("start", new Vector3(2f, 0f, 2f));
            context.SetValue("goal", new Vector3(5f, 0f, 5f));

            var status = Gate(new DoesPathExistBehaviour(self, "start", "goal", (from, to) =>
            {
                asked.Add((from, to));
                return false;
            }), context);

            Assert.That(status, Is.EqualTo(BehaviourStatus.Failure), "길이 없다고 답하면 막는다.");
            Assert.That(asked[0].From, Is.EqualTo(new Vector3(2f, 0f, 2f)));
        }

        [Test]
        public void DoesPathExistDoesNotAskWhenTheGoalIsMissing()
        {
            var self = Track(new GameObject("Self")).transform;
            var asked = 0;

            var status = Gate(new DoesPathExistBehaviour(self, null, "goal", (_, _) =>
            {
                asked++;
                return true;
            }), new BehaviourContext());

            Assert.That(status, Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(asked, Is.EqualTo(0));
        }

        [Test]
        public void TheDefinitionsNeedTheirKeysAndSomeNeedTheOwner()
        {
            var noOwner = new BehaviourBuildContext(null, new BehaviourContext());
            var owner = new BehaviourBuildContext(Track(new GameObject("Owner")), new BehaviourContext());

            Assert.That(new ConeCheckDefinition().CreateBehaviour(noOwner), Is.Null);
            Assert.That(Configure(new ConeCheckDefinition(), ("originKey", "o"), ("directionKey", "d"), ("observedKey", "x"))
                .CreateBehaviour(noOwner), Is.TypeOf<ConeCheckBehaviour>());

            Assert.That(new KeepInConeDefinition().CreateBehaviour(noOwner), Is.Null);
            Assert.That(Configure(new KeepInConeDefinition(), ("originKey", "o"), ("observedKey", "x"))
                .CreateBehaviour(noOwner), Is.TypeOf<KeepInConeBehaviour>());

            var atLocation = Configure(new IsAtLocationDefinition(), ("locationKey", "l"));
            Assert.That(atLocation.CreateBehaviour(noOwner), Is.Null, "유닛 자신의 자리가 필요하다.");
            Assert.That(atLocation.CreateBehaviour(owner), Is.TypeOf<IsAtLocationBehaviour>());

            var pathExists = Configure(new DoesPathExistDefinition(), ("toKey", "g"));
            Assert.That(pathExists.CreateBehaviour(noOwner), Is.Null);
            Assert.That(pathExists.CreateBehaviour(owner), Is.TypeOf<DoesPathExistBehaviour>());
            Assert.That(new DoesPathExistDefinition().CreateBehaviour(owner), Is.Null, "도착 키가 없으면 만들지 않는다.");

            foreach (var definition in new BehaviourNodeDefinition[]
                     {
                         new ConeCheckDefinition(), new KeepInConeDefinition(), new IsAtLocationDefinition(), new DoesPathExistDefinition()
                     })
            {
                Assert.That(definition.DisplayName, Is.Not.Empty, $"{definition.GetType().Name}의 이름이 비어 있다.");
            }
        }

        private static BehaviourStatus Gate(IBehaviour condition, IBehaviourContext context)
        {
            var tree = new BehaviourTreeInstance();
            var gate = tree.SetRoot(condition);
            tree.AddChild(gate, new ActionBehaviour(_ => BehaviourStatus.Success));
            return tree.Tick(context);
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static TDefinition Configure<TDefinition>(TDefinition definition, params (string Field, object Value)[] values)
            where TDefinition : BehaviourNodeDefinition
        {
            foreach (var (field, value) in values)
            {
                var info = typeof(TDefinition).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(info, Is.Not.Null, $"{typeof(TDefinition).Name}에 '{field}' 필드가 없다. 이름이 바뀌었다면 이 검사도 함께 고쳐야 한다.");
                info.SetValue(definition, value);
            }

            return definition;
        }

        private sealed class CountingMover : ICharacterMover
        {
            public int StopCount { get; private set; }

            public bool HasReachedDestination => false;

            public bool MoveTo(Vector3 destination) => true;

            public void Stop() => StopCount++;
        }
    }
}
