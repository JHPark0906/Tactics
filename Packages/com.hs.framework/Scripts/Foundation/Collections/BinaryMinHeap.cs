using System.Collections.Generic;

namespace HS.Framework.Foundation.Collections
{
    /// <summary>
    /// 우선순위가 가장 낮은 항목을 꺼내는 이진 힙이다. A* 의 열린 목록이 이것 위에 선다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>우선순위가 같은 항목이 여럿이면 꺼내는 순서가 갈릴 수 있다.</b> 배열 위치가 같은 우선순위 항목들
    /// 사이의 순서를 정하지 않으므로, 그대로 두면 삽입 순서 같은 실행마다 달라질 수 있는 값에 기대게 된다.
    /// 그래서 우선순위가 같을 때는 항목 자체를 <see cref="IComparer{T}"/>로 비교해 가른다.
    /// </para>
    /// <para>
    /// 이미 넣은 항목의 우선순위를 낮추는 연산(decrease-key)은 없다. 더 싼 경로를 찾으면 새 우선순위로
    /// 다시 넣고, 오래된 항목은 꺼낼 때 호출자가 걸러낸다(느슨한 삭제) — 힙 안에서 항목의 위치를 추적하는
    /// 자료구조 없이도 올바르게 동작하는 값싼 방법이다.
    /// </para>
    /// </remarks>
    /// <typeparam name="TItem">담을 항목의 형식이다.</typeparam>
    public sealed class BinaryMinHeap<TItem> : IPriorityQueue<TItem>
    {
        private readonly List<(float Priority, TItem Item)> _entries = new();
        private readonly IComparer<TItem> _tieBreaker;

        /// <summary>주어진 비교자로 동점을 가르는 힙을 만든다.</summary>
        /// <param name="tieBreaker">우선순위가 같을 때 항목을 비교할 비교자이다.</param>
        public BinaryMinHeap(IComparer<TItem> tieBreaker)
        {
            _tieBreaker = tieBreaker;
        }

        /// <summary>담긴 항목 수이다.</summary>
        public int Count => _entries.Count;

        /// <summary>항목을 주어진 우선순위로 넣는다.</summary>
        /// <param name="item">넣을 항목이다.</param>
        /// <param name="priority">낮을수록 먼저 나오는 우선순위이다.</param>
        public void Push(TItem item, float priority)
        {
            _entries.Add((priority, item));
            SiftUp(_entries.Count - 1);
        }

        /// <summary>가장 낮은 우선순위의 항목을 꺼내 없앤다. 비어 있으면 호출하지 않는다.</summary>
        /// <returns>가장 낮은 우선순위의 항목이다.</returns>
        public TItem Pop()
        {
            var root = _entries[0].Item;
            var last = _entries.Count - 1;
            _entries[0] = _entries[last];
            _entries.RemoveAt(last);
            if (_entries.Count > 0)
            {
                SiftDown(0);
            }

            return root;
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (!IsLess(index, parent))
                {
                    break;
                }

                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            var count = _entries.Count;
            while (true)
            {
                var left = index * 2 + 1;
                var right = index * 2 + 2;
                var smallest = index;
                if (left < count && IsLess(left, smallest))
                {
                    smallest = left;
                }

                if (right < count && IsLess(right, smallest))
                {
                    smallest = right;
                }

                if (smallest == index)
                {
                    return;
                }

                Swap(index, smallest);
                index = smallest;
            }
        }

        /// <summary>a 자리의 항목이 b 자리의 항목보다 먼저 나와야 하는지 본다. 우선순위, 그다음 비교자 순이다.</summary>
        private bool IsLess(int a, int b)
        {
            var priorityOrder = _entries[a].Priority.CompareTo(_entries[b].Priority);
            return priorityOrder != 0
                ? priorityOrder < 0
                : _tieBreaker.Compare(_entries[a].Item, _entries[b].Item) < 0;
        }

        private void Swap(int a, int b)
        {
            (_entries[a], _entries[b]) = (_entries[b], _entries[a]);
        }
    }
}
