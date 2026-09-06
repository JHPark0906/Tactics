using System.Collections.Generic;
using HS.Framework.Item;
using R3;

namespace HS.Framework.Item
{
    /// <summary>
    /// 아이템 슬롯 수 제한과 스택 규칙을 처리하는 기본 인벤토리 구현의 확장 지점이다.
    /// </summary>
    public sealed class Inventory : IInventory
    {
        private readonly int _capacity;
        private readonly Subject<InventoryChangedEvent> _changed = new();
        private readonly Observable<InventoryChangedEvent> _changedObservable;
        private readonly List<ItemStack> _items;
        private bool _isDirty;
        private long _revision;

        /// <summary>
        /// 보관할 수 있는 최대 ItemStack 수를 지정해 인벤토리를 생성한다.
        /// </summary>
        public Inventory(int capacity)
        {
            if (capacity < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
            _changedObservable = _changed.AsObservable();
            _items = new List<ItemStack>();
        }

        /// <inheritdoc />
        public int Capacity => _capacity;

        /// <inheritdoc />
        public bool IsDirty => _isDirty;

        /// <inheritdoc />
        public long Revision => _revision;

        /// <inheritdoc />
        public Observable<InventoryChangedEvent> Changed => _changedObservable;

        /// <inheritdoc />
        public IReadOnlyList<ItemStack> Items => _items;

        /// <inheritdoc />
        public bool CanAdd(ItemStack itemStack)
        {
            if (!IsValid(itemStack))
            {
                return false;
            }

            var remainingAmount = itemStack.Amount;
            var maxStackSize = itemStack.Definition.MaxStackSize;
            foreach (var existingStack in _items)
            {
                if (existingStack.Definition != itemStack.Definition)
                {
                    continue;
                }

                remainingAmount -= System.Math.Max(0, maxStackSize - existingStack.Amount);
                if (remainingAmount <= 0)
                {
                    return true;
                }
            }

            var requiredStackCount = (remainingAmount + maxStackSize - 1) / maxStackSize;
            return requiredStackCount <= _capacity - _items.Count;
        }

        /// <inheritdoc />
        public int Add(ItemStack itemStack)
        {
            if (!IsValid(itemStack))
            {
                return 0;
            }

            var remainingAmount = itemStack.Amount;
            var maxStackSize = itemStack.Definition.MaxStackSize;
            for (var index = 0; index < _items.Count && remainingAmount > 0; index++)
            {
                var existingStack = _items[index];
                if (existingStack.Definition != itemStack.Definition || existingStack.Amount >= maxStackSize)
                {
                    continue;
                }

                var amountToAdd = System.Math.Min(remainingAmount, maxStackSize - existingStack.Amount);
                _items[index] = new ItemStack(existingStack.Definition, existingStack.Amount + amountToAdd);
                remainingAmount -= amountToAdd;
            }

            while (remainingAmount > 0 && _items.Count < _capacity)
            {
                var amountToAdd = System.Math.Min(remainingAmount, maxStackSize);
                _items.Add(new ItemStack(itemStack.Definition, amountToAdd));
                remainingAmount -= amountToAdd;
            }

            var addedAmount = itemStack.Amount - remainingAmount;
            if (addedAmount > 0)
            {
                NotifyChanged(InventoryChangeType.Added, itemStack.Definition, addedAmount);
            }

            return addedAmount;
        }

        /// <inheritdoc />
        public int Remove(ItemDefinition definition, int amount)
        {
            if (definition == null || amount <= 0)
            {
                return 0;
            }

            var remainingAmount = amount;
            for (var index = _items.Count - 1; index >= 0 && remainingAmount > 0; index--)
            {
                var existingStack = _items[index];
                if (existingStack.Definition != definition)
                {
                    continue;
                }

                var amountToRemove = System.Math.Min(remainingAmount, existingStack.Amount);
                remainingAmount -= amountToRemove;
                var updatedAmount = existingStack.Amount - amountToRemove;
                if (updatedAmount == 0)
                {
                    _items.RemoveAt(index);
                }
                else
                {
                    _items[index] = new ItemStack(definition, updatedAmount);
                }
            }

            var removedAmount = amount - remainingAmount;
            if (removedAmount > 0)
            {
                NotifyChanged(InventoryChangeType.Removed, definition, removedAmount);
            }

            return removedAmount;
        }

        /// <inheritdoc />
        public bool Contains(ItemDefinition definition, int amount = 1)
        {
            if (definition == null || amount <= 0)
            {
                return false;
            }

            var totalAmount = 0;
            foreach (var itemStack in _items)
            {
                if (itemStack.Definition != definition)
                {
                    continue;
                }

                totalAmount += itemStack.Amount;
                if (totalAmount >= amount)
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public bool AcknowledgeSaved(long savedRevision)
        {
            if (savedRevision != _revision)
            {
                return false;
            }

            _isDirty = false;
            return true;
        }

        private void NotifyChanged(InventoryChangeType changeType, ItemDefinition definition, int amount)
        {
            _revision++;
            _isDirty = true;
            _changed.OnNext(new InventoryChangedEvent(changeType, definition, amount, _revision));
        }

        private static bool IsValid(ItemStack itemStack)
        {
            return itemStack.Definition != null
                   && itemStack.Amount > 0
                   && itemStack.Definition.MaxStackSize > 0;
        }
    }
}
