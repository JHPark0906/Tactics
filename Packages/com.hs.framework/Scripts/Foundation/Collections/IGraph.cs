namespace HS.Framework.Foundation.Collections
{
    /// <summary>가리키는 것만으로 고칠 수 있는 그래프 계약이다.</summary>
    /// <remarks>
    /// <para>
    /// <b>노드와 간선을 새로 만드는 자리는 여기 없다.</b> 무엇을 실을지는 구체 형식이 아는 것이고,
    /// 가리키는 형식만 열어 둔 이 계약은 그 값을 말할 수 없다. 만드는 일은 구체 형식에 둔다.
    /// </para>
    /// <para>
    /// 그래서 여기 남는 것은 이미 있는 것을 가리켜 빼는 일뿐이다. 훑고 지우는 코드는 이 계약으로
    /// 충분하고, 짓는 코드는 어차피 무엇을 짓는지 알고 있다.
    /// </para>
    /// </remarks>
    /// <typeparam name="TNodeRef">노드를 가리키는 형식이다.</typeparam>
    /// <typeparam name="TEdgeRef">간선을 가리키는 형식이다.</typeparam>
    public interface IGraph<TNodeRef, TEdgeRef> : IReadOnlyGraph<TNodeRef, TEdgeRef>
    {
        /// <summary>그 노드를 뺀다.</summary>
        /// <remarks>붙어 있던 것을 어떻게 할지는 구현이 정하고 자기 문서에 적는다.</remarks>
        /// <param name="node">뺄 노드이다.</param>
        /// <returns>실제로 뺐으면 참이다.</returns>
        bool RemoveNode(TNodeRef node);

        /// <summary>그 간선을 뺀다.</summary>
        /// <param name="edge">뺄 간선이다.</param>
        /// <returns>실제로 뺐으면 참이다.</returns>
        bool RemoveEdge(TEdgeRef edge);
    }
}
