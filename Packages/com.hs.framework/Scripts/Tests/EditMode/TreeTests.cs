using HS.Framework.Foundation.Collections;
using NUnit.Framework;
using System;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>트리가 약속한 것을 고정한다.</summary>
    /// <remarks>
    /// 여기 담긴 것은 코드만 봐서는 갈리지 않는 결정들이다. 루트를 다시 세우면 어떻게 되는지,
    /// 노드를 뺄 때 아래 가지를 어떻게 하는지, 루트가 간선으로도 세어지는지는 구현이 달라져도
    /// 컴파일이 되므로 검사로 못 박는다.
    /// </remarks>
    public sealed class TreeTests
    {
        [Test]
        public void ATreeStartsEmptyAndGetsItsRootOnce()
        {
            var tree = new Tree<string>();

            Assert.That(tree.Root, Is.Null);
            Assert.That(tree.NodeCount, Is.EqualTo(0));

            var root = tree.SetRoot("root");

            Assert.That(tree.Root, Is.SameAs(root));
            Assert.That(root.IsRoot, Is.True);
        }

        [Test]
        public void SettingASecondRootIsRejected()
        {
            var tree = new Tree<string>();
            tree.SetRoot("root");

            Assert.Throws<InvalidOperationException>(() => tree.SetRoot("another"),
                "루트를 갈아치우면 아래 가지가 통째로 트리 밖에 남는다.");
        }

        [Test]
        public void AChildCannotBeAddedUnderANodeFromAnotherTree()
        {
            var tree = new Tree<string>();
            tree.SetRoot("root");
            var stranger = new Tree<string>().SetRoot("stranger");

            Assert.Throws<ArgumentException>(() => tree.AddChild(stranger, "child"));
        }

        [Test]
        public void ChildrenKeepTheOrderTheyWereAddedIn()
        {
            var tree = new Tree<string>();
            var root = tree.SetRoot("root");
            var first = tree.AddChild(root, "a");
            var second = tree.AddChild(root, "b");
            var third = tree.AddChild(root, "c");

            Assert.That(root.Children, Is.EqualTo(new[] { first, second, third }),
                "자식 순서가 곧 우선순위이므로 넣은 순서가 유지되어야 한다.");
        }

        [Test]
        public void MovingAChildChangesOnlyItsPlaceAmongSiblings()
        {
            var tree = new Tree<string>();
            var root = tree.SetRoot("root");
            var first = tree.AddChild(root, "a");
            var second = tree.AddChild(root, "b");
            var third = tree.AddChild(root, "c");

            tree.MoveChild(root, 2, 0);

            Assert.That(root.Children, Is.EqualTo(new[] { third, first, second }));
        }

        [Test]
        public void RemovingANodeAlsoRemovesEverythingBelowIt()
        {
            var tree = new Tree<string>();
            var root = tree.SetRoot("root");
            var branch = tree.AddChild(root, "branch");
            var leaf = tree.AddChild(branch, "leaf");
            var sibling = tree.AddChild(root, "sibling");

            Assert.That(tree.RemoveNode(branch), Is.True);

            Assert.That(tree.Contains(branch), Is.False);
            Assert.That(tree.Contains(leaf), Is.False, "아래 가지를 부모에게 올리면 형제들의 우선순위가 밀린다.");
            Assert.That(tree.Contains(sibling), Is.True);
            Assert.That(root.Children, Is.EqualTo(new[] { sibling }));
        }

        [Test]
        public void RemovingTheRootEmptiesTheTree()
        {
            var tree = new Tree<string>();
            var root = tree.SetRoot("root");
            tree.AddChild(root, "a");

            Assert.That(tree.RemoveNode(root), Is.True);

            Assert.That(tree.Root, Is.Null);
            Assert.That(tree.NodeCount, Is.EqualTo(0));
        }

        [Test]
        public void TheRootIsANodeButNotAnEdge()
        {
            var tree = new Tree<string>();
            var root = tree.SetRoot("root");
            var child = tree.AddChild(root, "a");

            Assert.That(tree.Contains(root), Is.True);
            Assert.That(tree.Contains(new TreeEdge<string>(root)), Is.False,
                "루트는 부모가 없으므로 그리로 내려오는 간선이 없다.");
            Assert.That(tree.Contains(new TreeEdge<string>(child)), Is.True);
        }

        [Test]
        public void EveryNodeButTheRootBringsExactlyOneEdge()
        {
            var tree = new Tree<string>();
            var root = tree.SetRoot("root");
            var branch = tree.AddChild(root, "branch");
            tree.AddChild(branch, "leaf");

            Assert.That(tree.NodeCount, Is.EqualTo(3));
            Assert.That(tree.EdgeCount, Is.EqualTo(2));
            Assert.That(tree.Edges.Count, Is.EqualTo(2));
        }

        [Test]
        public void AnEdgeRunsFromTheParentDownToTheChild()
        {
            var tree = new Tree<string>();
            var root = tree.SetRoot("root");
            var child = tree.AddChild(root, "a");
            var edge = new TreeEdge<string>(child);

            Assert.That(tree.SourceOf(edge), Is.SameAs(root));
            Assert.That(tree.TargetOf(edge), Is.SameAs(child));
            Assert.That(tree.EdgesBetween(root, child).Count, Is.EqualTo(1));
            Assert.That(tree.EdgesBetween(child, root).Count, Is.EqualTo(0), "간선은 부모에서 자식으로만 간다.");
        }

        [Test]
        public void TheRootHasNothingComingIntoIt()
        {
            var tree = new Tree<string>();
            var root = tree.SetRoot("root");
            var child = tree.AddChild(root, "a");

            Assert.That(tree.IncomingEdges(root).Count, Is.EqualTo(0));
            Assert.That(tree.IncomingEdges(child).Count, Is.EqualTo(1), "부모가 하나뿐이므로 답도 하나이다.");
            Assert.That(tree.OutgoingEdges(root).Count, Is.EqualTo(1));
        }
    }
}
