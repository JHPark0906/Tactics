using System;
using System.Collections.Generic;

namespace HS.Framework.Foundation.Collections
{
    /// <summary>
    /// 참조 타입 인스턴스를 재사용하는 범용 오브젝트 풀이다. 사전 워밍업과 자동 확장을 지원한다.
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _createItem;
        private readonly Action<T> _onAcquire;
        private readonly Action<T> _onRelease;
        private readonly Stack<T> _freeItems = new();
        private readonly HashSet<T> _freeItemSet = new();
        private readonly HashSet<T> _ownedItems = new();

        /// <summary>
        /// 항목 생성 함수를 받아 풀을 생성한다.
        /// </summary>
        /// <param name="createItem">새 항목을 만드는 함수이다.</param>
        /// <param name="onAcquire">대여하는 항목을 내주기 전에 부를 함수이며, 생략하면 아무 일도 하지 않는다.</param>
        /// <param name="onRelease">반환받은 항목을 다시 대여 가능한 상태로 두기 전에 부를 함수이며, 생략하면 아무 일도 하지 않는다.</param>
        public ObjectPool(Func<T> createItem, Action<T> onAcquire = null, Action<T> onRelease = null)
        {
            _createItem = createItem ?? throw new ArgumentNullException(nameof(createItem));
            _onAcquire = onAcquire;
            _onRelease = onRelease;
        }

        /// <summary>지금까지 생성한 전체 항목 수이다.</summary>
        public int CreatedCount => _ownedItems.Count;

        /// <summary>대여 가능한 항목 수이다.</summary>
        public int FreeCount => _freeItems.Count;

        /// <summary>대여 중인 항목 수이다.</summary>
        public int ActiveCount => _ownedItems.Count - _freeItems.Count;

        /// <summary>지정한 수만큼 항목을 미리 생성해 둔다.</summary>
        public void Prewarm(int count)
        {
            for (var index = 0; index < count; index++)
            {
                var item = CreateOwnedItem();
                _freeItems.Push(item);
                _freeItemSet.Add(item);
            }
        }

        /// <summary>항목을 대여한다. 여분이 없으면 새로 생성해 자동 확장한다.</summary>
        public T Acquire()
        {
            T item;
            if (_freeItems.Count > 0)
            {
                item = _freeItems.Pop();
                _freeItemSet.Remove(item);
            }
            else
            {
                item = CreateOwnedItem();
            }

            _onAcquire?.Invoke(item);
            return item;
        }

        /// <summary>
        /// 대여한 항목을 반환한다. 이 풀이 만들지 않았거나 이미 반환된 항목이면 거부하고 false를 반환한다.
        /// </summary>
        public bool Release(T item)
        {
            if (item == null || !_ownedItems.Contains(item) || _freeItemSet.Contains(item))
            {
                return false;
            }

            _onRelease?.Invoke(item);
            _freeItems.Push(item);
            _freeItemSet.Add(item);
            return true;
        }

        private T CreateOwnedItem()
        {
            var item = _createItem();
            if (item == null)
            {
                throw new InvalidOperationException("풀 항목 생성 함수가 null을 반환했다.");
            }

            _ownedItems.Add(item);
            return item;
        }
    }
}
