using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>다른 트리 에셋을 끼우는 자리가 그 에셋의 자리들을 제자리에 이어 붙이는 것을 고정한다.</summary>
    /// <remarks>
    /// 끼운 에셋의 뿌리가 그 자리에 서고 아래는 그 에셋이 그린 대로 붙는다. 자리 하나가 안에서
    /// 따로 트리를 돌리는 것이 아니므로, 만들어진 트리에는 끼운 자리 자체가 없다.
    /// </remarks>
    public sealed class RunBehaviourDefinitionTests
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
        public void ASubtreeIsGraftedInPlaceKeepingSiblingOrder()
        {
            var inner = MakeAsset(
                new BehaviourNodeDefinition[] { new SelectorDefinition(), new WaitDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0 });
            var outer = MakeAsset(
                new BehaviourNodeDefinition[] { new SequenceDefinition(), Run(inner), new WaitDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0, 0 });

            var tree = outer.CreateRuntimeTree(Build());

            Assert.That(tree.Count, Is.EqualTo(4), "끼운 자리는 사라지고 그 에셋의 자리들이 들어온다.");
            Assert.That(tree.Root.Value, Is.TypeOf<SequenceBehaviour>());
            Assert.That(tree.Root.Children.Count, Is.EqualTo(2));
            Assert.That(tree.Root.Children[0].Value, Is.TypeOf<SelectorBehaviour>(), "끼운 에셋의 뿌리가 그 자리에 선다.");
            Assert.That(tree.Root.Children[0].Children[0].Value, Is.TypeOf<WaitBehaviour>());
            Assert.That(tree.Root.Children[1].Value, Is.TypeOf<WaitBehaviour>(), "뒤엣 형제는 끼운 가지 뒤에 그대로 온다.");
        }

        [Test]
        public void ASubtreeAtTheRootPositionBecomesTheRoot()
        {
            var inner = MakeAsset(
                new BehaviourNodeDefinition[] { new SelectorDefinition(), new WaitDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0 });
            var outer = MakeAsset(new BehaviourNodeDefinition[] { Run(inner) }, new[] { BehaviourTreeAsset.NoParent });

            var tree = outer.CreateRuntimeTree(Build());

            Assert.That(tree.Root.Value, Is.TypeOf<SelectorBehaviour>());
            Assert.That(tree.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheSameSubtreeCanBeGraftedTwiceWithoutSharingState()
        {
            var inner = MakeAsset(
                new BehaviourNodeDefinition[] { new SelectorDefinition(), new WaitDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0 });
            var outer = MakeAsset(
                new BehaviourNodeDefinition[] { new SelectorDefinition(), Run(inner), Run(inner) },
                new[] { BehaviourTreeAsset.NoParent, 0, 0 });

            var tree = outer.CreateRuntimeTree(Build());

            Assert.That(tree.Count, Is.EqualTo(5));
            Assert.That(tree.Root.Children[0].Value, Is.Not.SameAs(tree.Root.Children[1].Value),
                "같은 에셋을 두 자리에 끼워도 도는 자리는 따로 만든다.");
        }

        [Test]
        public void AnAssetThatRunsItselfIsCutOffInsteadOfRecursingForever()
        {
            var asset = MakeAsset(
                new BehaviourNodeDefinition[] { new SequenceDefinition(), new RunBehaviourDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0 });
            SetSubtree((RunBehaviourDefinition)NodesOf(asset)[1], asset);
            LogAssert.Expect(LogType.Warning, new Regex("끝없이 되돈다"));

            var tree = asset.CreateRuntimeTree(Build());

            Assert.That(tree.Count, Is.EqualTo(1), "자기를 끼우는 자리는 이어 붙이지 않는다.");
        }

        [Test]
        public void AnAssetThatRunsItsAncestorIsCutOff()
        {
            var inner = MakeAsset(
                new BehaviourNodeDefinition[] { new SelectorDefinition(), new RunBehaviourDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0 });
            var outer = MakeAsset(
                new BehaviourNodeDefinition[] { new SequenceDefinition(), Run(inner) },
                new[] { BehaviourTreeAsset.NoParent, 0 });
            SetSubtree((RunBehaviourDefinition)NodesOf(inner)[1], outer);
            LogAssert.Expect(LogType.Warning, new Regex("끝없이 되돈다"));

            var tree = outer.CreateRuntimeTree(Build());

            Assert.That(tree.Count, Is.EqualTo(2), "조상 에셋을 다시 끼우는 자리도 이어 붙이지 않는다.");
        }

        [Test]
        public void ARunSlotWithoutAnAssetLeavesThatBranchOut()
        {
            var outer = MakeAsset(
                new BehaviourNodeDefinition[] { new SequenceDefinition(), new RunBehaviourDefinition(), new WaitDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0, 0 });
            LogAssert.Expect(LogType.Warning, new Regex("끼울 에셋이 비어 있다"));

            var tree = outer.CreateRuntimeTree(Build());

            Assert.That(tree.Count, Is.EqualTo(2));
            Assert.That(tree.Root.Children[0].Value, Is.TypeOf<WaitBehaviour>());
        }

        [Test]
        public void ChildrenDrawnUnderARunSlotHaveNowhereToGo()
        {
            var inner = MakeAsset(new BehaviourNodeDefinition[] { new SelectorDefinition() }, new[] { BehaviourTreeAsset.NoParent });
            var outer = MakeAsset(
                new BehaviourNodeDefinition[] { new SequenceDefinition(), Run(inner), new WaitDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0, 1 });
            LogAssert.Expect(LogType.Warning, new Regex("붙을 곳이 없다"));

            var tree = outer.CreateRuntimeTree(Build());

            Assert.That(tree.Count, Is.EqualTo(2), "끼우는 자리는 잎이다. 그 아래에 그린 자식은 붙을 곳이 없다.");
            Assert.That(tree.Root.Children[0].Children.Count, Is.EqualTo(0));
        }

        [Test]
        public void TheDefinitionDoesNotMakeABehaviourOfItsOwn()
        {
            var inner = MakeAsset(new BehaviourNodeDefinition[] { new SelectorDefinition() }, new[] { BehaviourTreeAsset.NoParent });
            var definition = Run(inner);

            Assert.That(definition.DisplayName, Is.Not.Empty);
            Assert.That(definition.CreateBehaviour(Build()), Is.Null, "자리를 만드는 대신 에셋이 서브트리를 이어 붙인다.");
        }

        private static BehaviourBuildContext Build() => new(null, new BehaviourContext());

        private BehaviourTreeAsset MakeAsset(BehaviourNodeDefinition[] nodes, int[] parents)
        {
            var asset = ScriptableObject.CreateInstance<BehaviourTreeAsset>();
            _created.Add(asset);
            SetPrivate(asset, "nodes", new List<BehaviourNodeDefinition>(nodes));
            SetPrivate(asset, "parents", new List<int>(parents));
            return asset;
        }

        private static RunBehaviourDefinition Run(BehaviourTreeAsset subtree)
        {
            var definition = new RunBehaviourDefinition();
            SetSubtree(definition, subtree);
            return definition;
        }

        private static void SetSubtree(RunBehaviourDefinition definition, BehaviourTreeAsset subtree)
            => SetPrivate(definition, "subtree", subtree);

        private static List<BehaviourNodeDefinition> NodesOf(BehaviourTreeAsset asset)
        {
            var info = typeof(BehaviourTreeAsset).GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, "BehaviourTreeAsset에 'nodes' 필드가 없다.");
            return (List<BehaviourNodeDefinition>)info.GetValue(asset);
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, $"{target.GetType().Name}에 '{field}' 필드가 없다. 이름이 바뀌었다면 이 검사도 함께 고쳐야 한다.");
            info.SetValue(target, value);
        }
    }
}
