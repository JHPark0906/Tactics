using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>에셋이 담는 것과 담지 않는 것을 고정한다.</summary>
    public sealed class BehaviourTreeAssetTests
    {
        [Test]
        public void AnEmptyAssetIsNotValidAndMakesAnEmptyTree()
        {
            var asset = ScriptableObject.CreateInstance<BehaviourTreeAsset>();
            try
            {
                Assert.That(asset.IsValid(out var error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(asset.CreateRuntimeTree(Build()).Count, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TwoTreesMadeFromOneAssetDoNotShareTheirRunningState()
        {
            var asset = MakeAsset();
            try
            {
                var first = asset.CreateRuntimeTree(Build());
                var second = asset.CreateRuntimeTree(Build());

                Assert.That(first.Root, Is.Not.SameAs(second.Root),
                    "도는 자리를 나눠 쓰면 유닛들이 서로의 진행을 덮어쓴다.");
                Assert.That(first.Root.Value, Is.Not.SameAs(second.Root.Value));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TheShapeComesOutTheWayItWasStored()
        {
            var asset = MakeAsset();
            try
            {
                var tree = asset.CreateRuntimeTree(Build());

                Assert.That(tree.Count, Is.EqualTo(2));
                Assert.That(tree.Root.Children.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private static BehaviourBuildContext Build() => new(null, new BehaviourContext());

        private static BehaviourTreeAsset MakeAsset()
        {
            var asset = ScriptableObject.CreateInstance<BehaviourTreeAsset>();
            var nodes = new System.Collections.Generic.List<BehaviourNodeDefinition>
            {
                new SelectorDefinition(),
                new SequenceDefinition()
            };
            var parents = new System.Collections.Generic.List<int> { BehaviourTreeAsset.NoParent, 0 };
            typeof(BehaviourTreeAsset)
                .GetField("nodes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(asset, nodes);
            typeof(BehaviourTreeAsset)
                .GetField("parents", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(asset, parents);
            return asset;
        }
    }
}
