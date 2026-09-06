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
    /// <summary>에셋이 트리를 지으면서 자리를 뺄 때 그 사실을 알리는 것을 고정한다.</summary>
    /// <remarks>
    /// 설명이 자리를 비우면 그 가지가 통째로 빠진다. 그것이 조용히 일어나면 화면에는 유닛이
    /// 가만히 서 있는 것만 남고, 어느 자리가 왜 빠졌는지는 아무 데도 남지 않는다. 자리 번호와
    /// 이름이 경고에 실리는지, 빠진 자리 아래를 다시 알리지는 않는지를 본다.
    /// </remarks>
    public sealed class BehaviourTreeAssetBuildLogTests
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
        public void ASkippedSlotIsReportedWithItsIndexAndName()
        {
            var asset = MakeAsset(
                new BehaviourNodeDefinition[] { new SequenceDefinition(), new MoveToPositionDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0 });
            LogAssert.Expect(LogType.Warning, new Regex("1번째 자리 'MoveTo'.*그 가지를 뺀다"));

            var tree = asset.CreateRuntimeTree(Build());

            Assert.That(tree.Count, Is.EqualTo(1));
        }

        [Test]
        public void ARootThatCannotBeMadeSaysTheTreeIsEmpty()
        {
            var asset = MakeAsset(
                new BehaviourNodeDefinition[] { new MoveToPositionDefinition() },
                new[] { BehaviourTreeAsset.NoParent });
            LogAssert.Expect(LogType.Warning, new Regex("0번째 자리 'MoveTo'.*뿌리라 트리가 빈다"));

            var tree = asset.CreateRuntimeTree(Build());

            Assert.That(tree.Root, Is.Null);
        }

        [Test]
        public void DescendantsOfASkippedSlotAreNotReportedAgain()
        {
            var asset = MakeAsset(
                new BehaviourNodeDefinition[] { new SequenceDefinition(), new MoveToPositionDefinition(), new WaitDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0, 1 });
            LogAssert.Expect(LogType.Warning, new Regex("1번째 자리 'MoveTo'"));

            asset.CreateRuntimeTree(Build());

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AnAssetThatCannotFormATreeIsReported()
        {
            var asset = MakeAsset(new BehaviourNodeDefinition[0], new int[0]);
            LogAssert.Expect(LogType.Warning, new Regex("담긴 자리가 없다"));

            Assert.That(asset.CreateRuntimeTree(Build()).Root, Is.Null);
        }

        [Test]
        public void ARunSlotWithoutAnAssetIsReported()
        {
            var asset = MakeAsset(
                new BehaviourNodeDefinition[] { new SequenceDefinition(), new RunBehaviourDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0 });
            LogAssert.Expect(LogType.Warning, new Regex("1번째 자리 'Run'.*끼울 에셋이 비어 있다"));

            asset.CreateRuntimeTree(Build());
        }

        [Test]
        public void ARunSlotThatRecursesIsReported()
        {
            var asset = MakeAsset(
                new BehaviourNodeDefinition[] { new SequenceDefinition(), new RunBehaviourDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0 });
            SetPrivate(NodesOf(asset)[1], "subtree", asset);
            LogAssert.Expect(LogType.Warning, new Regex("1번째 자리 'Run: .*끝없이 되돈다"));

            asset.CreateRuntimeTree(Build());
        }

        [Test]
        public void AChildDrawnUnderARunSlotIsReported()
        {
            var inner = MakeAsset(new BehaviourNodeDefinition[] { new SelectorDefinition() }, new[] { BehaviourTreeAsset.NoParent });
            var asset = MakeAsset(
                new BehaviourNodeDefinition[] { new SequenceDefinition(), new RunBehaviourDefinition(), new WaitDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0, 1 });
            SetPrivate(NodesOf(asset)[1], "subtree", inner);
            LogAssert.Expect(LogType.Warning, new Regex("2번째 자리 'Wait.*붙을 곳이 없다"));

            asset.CreateRuntimeTree(Build());
        }

        [Test]
        public void ASlotThatIsMadeIsNotReported()
        {
            var asset = MakeAsset(
                new BehaviourNodeDefinition[] { new SequenceDefinition(), new WaitDefinition() },
                new[] { BehaviourTreeAsset.NoParent, 0 });

            asset.CreateRuntimeTree(Build());

            LogAssert.NoUnexpectedReceived();
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
