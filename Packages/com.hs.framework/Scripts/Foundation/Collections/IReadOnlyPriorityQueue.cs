namespace HS.Framework.Foundation.Collections
{
    /// <summary>고치지 않고 담긴 수만 읽는 우선순위 큐 계약이다.</summary>
    /// <typeparam name="TItem">담을 항목의 형식이다.</typeparam>
    public interface IReadOnlyPriorityQueue<TItem>
    {
        /// <summary>담긴 항목 수이다.</summary>
        int Count { get; }
    }
}
