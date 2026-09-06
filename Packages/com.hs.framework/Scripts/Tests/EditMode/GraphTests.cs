using HS.Framework.Foundation.Collections;
using NUnit.Framework;
using System;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>그래프 자료구조가 약속한 것을 고정한다.</summary>
    /// <remarks>
    /// 여기 담긴 것은 코드만 봐서는 갈리지 않는 결정들이다. 다중 간선을 어떻게 세는지,
    /// 자기 순환이 인접에 몇 번 나오는지, 노드를 뺄 때 간선을 어떻게 하는지는 구현이 달라져도
    /// 컴파일이 되므로, 문서가 아니라 검사로 못 박는다.
    /// </remarks>
    public sealed class GraphTests
    {
        [Test]
        public void SameTwoNodesCanBeJoinedByMoreThanOneEdge()
        {
            var graph = new DirectedGraph<string>();
            var from = graph.AddNode("a");
            var to = graph.AddNode("b");

            var first = graph.AddEdge(from, to);
            var second = graph.AddEdge(from, to);

            Assert.That(first, Is.Not.SameAs(second), "같은 두 노드를 이어도 간선은 서로 다른 것이어야 한다.");
            Assert.That(graph.EdgeCount, Is.EqualTo(2));
            Assert.That(graph.EdgesBetween(from, to).Count, Is.EqualTo(2));
        }

        [Test]
        public void RemovingOneOfTwoParallelEdgesLeavesTheOther()
        {
            var graph = new DirectedGraph<string>();
            var from = graph.AddNode("a");
            var to = graph.AddNode("b");
            var first = graph.AddEdge(from, to);
            var second = graph.AddEdge(from, to);

            Assert.That(graph.RemoveEdge(first), Is.True);

            Assert.That(graph.EdgeCount, Is.EqualTo(1));
            Assert.That(graph.Edges[0], Is.SameAs(second), "지운 것이 아니라 남은 것이 있어야 한다.");
        }

        [Test]
        public void ASelfLoopAppearsOnceInTheIncidentEdgesOfAnUndirectedGraph()
        {
            var graph = new UndirectedGraph<string>();
            var node = graph.AddNode("a");
            graph.AddEdge(node, node);

            Assert.That(
                graph.OutgoingEdges(node).Count,
                Is.EqualTo(1),
                "자기 순환은 두 끝이 같은 노드이지만 간선 하나이므로 한 번만 나온다.");
        }

        [Test]
        public void RemovingANodeAlsoRemovesTheEdgesAttachedToIt()
        {
            var graph = new DirectedGraph<string>();
            var kept = graph.AddNode("a");
            var removed = graph.AddNode("b");
            graph.AddEdge(kept, removed);
            graph.AddEdge(removed, kept);

            Assert.That(graph.RemoveNode(removed), Is.True);

            Assert.That(graph.NodeCount, Is.EqualTo(1));
            Assert.That(
                graph.EdgeCount,
                Is.EqualTo(0),
                "노드가 사라졌는데 그 노드를 끝으로 삼는 간선이 남으면 어디에도 닿지 않는 간선이 된다.");
        }

        [Test]
        public void AnEdgeCannotBeAddedBetweenNodesThatAreNotInThisGraph()
        {
            var graph = new DirectedGraph<string>();
            var mine = graph.AddNode("a");
            var stranger = new DirectedGraph<string>().AddNode("b");

            Assert.Throws<ArgumentException>(() => graph.AddEdge(mine, stranger));
        }

        [Test]
        public void DirectionIsRespectedWhenLookingUpEdgesBetweenTwoNodes()
        {
            var graph = new DirectedGraph<string>();
            var from = graph.AddNode("a");
            var to = graph.AddNode("b");
            graph.AddEdge(from, to);

            Assert.That(graph.EdgesBetween(from, to).Count, Is.EqualTo(1));
            Assert.That(graph.EdgesBetween(to, from).Count, Is.EqualTo(0), "방향 있는 그래프는 반대 방향을 세지 않는다.");
        }

        [Test]
        public void DirectionIsIgnoredWhenLookingUpEdgesInAnUndirectedGraph()
        {
            var graph = new UndirectedGraph<string>();
            var from = graph.AddNode("a");
            var to = graph.AddNode("b");
            graph.AddEdge(from, to);

            Assert.That(graph.EdgesBetween(to, from).Count, Is.EqualTo(1), "방향이 없으므로 어느 쪽에서 물어도 같다.");
        }

        [Test]
        public void NodesAndEdgesKeepTheOrderTheyWereAddedIn()
        {
            var graph = new DirectedGraph<string>();
            var first = graph.AddNode("a");
            var second = graph.AddNode("b");
            var third = graph.AddNode("c");
            var firstEdge = graph.AddEdge(first, second);
            var secondEdge = graph.AddEdge(second, third);

            Assert.That(graph.Nodes, Is.EqualTo(new[] { first, second, third }));
            Assert.That(graph.Edges, Is.EqualTo(new[] { firstEdge, secondEdge }));
        }

        [Test]
        public void TheOppositeEndOfAnEdgeIsTheOtherNode()
        {
            var graph = new UndirectedGraph<string>();
            var from = graph.AddNode("a");
            var to = graph.AddNode("b");
            var edge = graph.AddEdge(from, to);

            Assert.That(edge.Opposite(from), Is.SameAs(to));
            Assert.That(edge.Opposite(to), Is.SameAs(from));
        }

        [Test]
        public void AskingForTheOppositeEndOfAnUnrelatedNodeIsRejected()
        {
            var graph = new UndirectedGraph<string>();
            var from = graph.AddNode("a");
            var to = graph.AddNode("b");
            var unrelated = graph.AddNode("c");
            var edge = graph.AddEdge(from, to);

            Assert.Throws<ArgumentException>(() => edge.Opposite(unrelated));
        }
    }
}
