using System.Collections.Generic;
using System.Reflection;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using HS.Framework.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>문맥의 키에 담긴 에셋을 그때그때 지어 돌리는 잎을 고정한다.</summary>
    public sealed class RunDynamicBehaviourTests
    {
        private const string Key = "subtree";

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
        public void TheAssetInTheContextIsBuiltAndTicked()
        {
            var context = new BehaviourContext();
            context.SetValue(Key, FinishingAsset(BehaviourStatus.Success));
            var run = new RunDynamicBehaviour(Key, new BehaviourBuildContext(null, context));

            Assert.That(run.Tick(context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(run.CurrentAsset, Is.Not.Null);
        }

        [Test]
        public void FailsWhenTheContextHasNoAsset()
        {
            var context = new BehaviourContext();
            var run = new RunDynamicBehaviour(Key, new BehaviourBuildContext(null, context));

            Assert.That(run.Tick(context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(run.CurrentAsset, Is.Null);
        }

        [Test]
        public void SwappingTheAssetResetsTheOldTreeAndBuildsTheNew()
        {
            var owner = Track(new GameObject("Owner"));
            var mover = owner.AddComponent<CountingMover>();
            var context = new BehaviourContext();
            context.SetValue("destination", Vector3.one);
            var walking = MovingAsset();
            context.SetValue(Key, walking);
            var run = new RunDynamicBehaviour(Key, new BehaviourBuildContext(owner, context));

            Assert.That(run.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            Assert.That(run.CurrentAsset, Is.SameAs(walking));

            var finishing = FinishingAsset(BehaviourStatus.Success);
            context.SetValue(Key, finishing);

            Assert.That(run.Tick(context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(run.CurrentAsset, Is.SameAs(finishing));
            Assert.That(mover.StopCount, Is.EqualTo(1), "다른 에셋으로 바뀌면 돌던 트리는 되돌려진다.");
        }

        [Test]
        public void RemovingTheAssetFailsAndResetsTheRunningTree()
        {
            var owner = Track(new GameObject("Owner"));
            var mover = owner.AddComponent<CountingMover>();
            var context = new BehaviourContext();
            context.SetValue("destination", Vector3.one);
            context.SetValue(Key, MovingAsset());
            var run = new RunDynamicBehaviour(Key, new BehaviourBuildContext(owner, context));

            run.Tick(context);
            context.RemoveValue(Key);

            Assert.That(run.Tick(context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(mover.StopCount, Is.EqualTo(1));
            Assert.That(run.CurrentAsset, Is.Null);
        }

        [Test]
        public void ResettingTheSlotResetsTheInnerTree()
        {
            var owner = Track(new GameObject("Owner"));
            var mover = owner.AddComponent<CountingMover>();
            var context = new BehaviourContext();
            context.SetValue("destination", Vector3.one);
            context.SetValue(Key, MovingAsset());
            var tree = new BehaviourTreeInstance();
            tree.SetRoot(new RunDynamicBehaviour(Key, new BehaviourBuildContext(owner, context)));

            tree.Tick(context);
            tree.Reset();

            Assert.That(mover.StopCount, Is.EqualTo(1), "바깥이 이 자리를 되돌리면 안의 트리도 통째로 되돌려진다.");
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running), "지은 것은 버리지 않으므로 다시 돈다.");
        }

        [Test]
        public void TheDefinitionNeedsAKeyButNotAnOwner()
        {
            var build = new BehaviourBuildContext(null, new BehaviourContext());

            Assert.That(new RunDynamicBehaviourDefinition().CreateBehaviour(build), Is.Null);

            var definition = new RunDynamicBehaviourDefinition();
            var info = typeof(RunDynamicBehaviourDefinition).GetField("assetKey", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, "RunDynamicBehaviourDefinition에 'assetKey' 필드가 없다.");
            info.SetValue(definition, Key);

            Assert.That(definition.CreateBehaviour(build), Is.TypeOf<RunDynamicBehaviour>());
            Assert.That(definition.DisplayName, Is.Not.Empty);
        }

        private BehaviourTreeAsset FinishingAsset(BehaviourStatus result)
        {
            var definition = new FinishWithResultDefinition();
            SetPrivate(definition, "result", result);
            return MakeAsset(new BehaviourNodeDefinition[] { definition }, new[] { BehaviourTreeAsset.NoParent });
        }

        private BehaviourTreeAsset MovingAsset()
        {
            var definition = new MoveToPositionDefinition();
            SetPrivate(definition, "destinationKey", "destination");
            return MakeAsset(new BehaviourNodeDefinition[] { definition }, new[] { BehaviourTreeAsset.NoParent });
        }

        private BehaviourTreeAsset MakeAsset(BehaviourNodeDefinition[] nodes, int[] parents)
        {
            var asset = ScriptableObject.CreateInstance<BehaviourTreeAsset>();
            _created.Add(asset);
            SetPrivate(asset, "nodes", new List<BehaviourNodeDefinition>(nodes));
            SetPrivate(asset, "parents", new List<int>(parents));
            return asset;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, $"{target.GetType().Name}에 '{field}' 필드가 없다. 이름이 바뀌었다면 이 검사도 함께 고쳐야 한다.");
            info.SetValue(target, value);
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private sealed class CountingMover : MonoBehaviour, ICharacterMover
        {
            public int StopCount { get; private set; }

            public bool HasReachedDestination => false;

            public bool MoveTo(Vector3 destination) => true;

            public void Stop() => StopCount++;
        }
    }
}
