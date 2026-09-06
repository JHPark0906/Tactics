namespace HS.Framework.Foundation.Collections
{
    /// <summary>우선순위가 가장 낮은 항목부터 꺼내는 큐 계약이다.</summary>
    /// <typeparam name="TItem">담을 항목의 형식이다.</typeparam>
    public interface IPriorityQueue<TItem> : IReadOnlyPriorityQueue<TItem>
    {
        /// <summary>항목을 주어진 우선순위로 넣는다.</summary>
        /// <param name="item">넣을 항목이다.</param>
        /// <param name="priority">낮을수록 먼저 나오는 우선순위이다.</param>
        void Push(TItem item, float priority);

        /// <summary>가장 낮은 우선순위의 항목을 꺼내 없앤다. 비어 있으면 호출하지 않는다.</summary>
        /// <returns>가장 낮은 우선순위의 항목이다.</returns>
        TItem Pop();
    }
}
