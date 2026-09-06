using System;
using System.Collections.Generic;
using HS.Framework.Item;

namespace HS.Framework.Item
{
    /// <summary>
    /// 인벤토리 상태를 (아이템 Id, 수량) 목록으로 직렬화 가능한 형태로 보관한다.
    /// 캡처는 같은 정의의 스택을 Id별로 합산하고, 복원은 카탈로그 룩업으로 정의를 되찾아 다시 추가한다.
    /// </summary>
    [Serializable]
    public sealed class InventorySaveData
    {
        /// <summary>
        /// 저장된 (아이템 Id, 수량) 항목 목록이다.
        /// </summary>
        public List<InventorySaveEntry> entries = new();

        /// <summary>
        /// 인벤토리의 현재 상태를 Id별 합산 수량으로 캡처한다.
        /// </summary>
        /// <param name="inventory">캡처할 인벤토리이다.</param>
        /// <returns>캡처한 저장 데이터를 반환한다.</returns>
        public static InventorySaveData Capture(IInventory inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            var data = new InventorySaveData();
            var entryIndexById = new Dictionary<string, int>();
            foreach (var itemStack in inventory.Items)
            {
                if (itemStack.Definition == null || itemStack.Amount <= 0 || string.IsNullOrWhiteSpace(itemStack.Definition.Id))
                {
                    continue;
                }

                if (entryIndexById.TryGetValue(itemStack.Definition.Id, out var entryIndex))
                {
                    data.entries[entryIndex].amount += itemStack.Amount;
                }
                else
                {
                    entryIndexById.Add(itemStack.Definition.Id, data.entries.Count);
                    data.entries.Add(new InventorySaveEntry
                    {
                        id = itemStack.Definition.Id,
                        amount = itemStack.Amount
                    });
                }
            }

            return data;
        }

        /// <summary>
        /// 인벤토리를 비운 뒤 저장 데이터의 항목을 카탈로그 룩업으로 되살려 추가한다.
        /// 카탈로그에 없는 Id와 0 이하 수량 항목은 건너뛴다.
        /// </summary>
        /// <param name="inventory">복원할 인벤토리이다.</param>
        /// <param name="data">복원에 사용할 저장 데이터이다.</param>
        /// <param name="catalog">Id를 정의로 되돌릴 아이템 카탈로그이다.</param>
        /// <returns>실제로 복원한 항목 수를 반환한다.</returns>
        public static int Restore(IInventory inventory, InventorySaveData data, ItemCatalog catalog)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            RemoveAllItems(inventory);

            var restoredCount = 0;
            foreach (var entry in data.entries)
            {
                if (entry == null || entry.amount <= 0 || !catalog.TryGetById(entry.id, out var definition))
                {
                    continue;
                }

                if (inventory.Add(new ItemStack(definition, entry.amount)) > 0)
                {
                    restoredCount++;
                }
            }

            return restoredCount;
        }

        /// <summary>
        /// 인벤토리의 모든 아이템 스택을 제거한다.
        /// </summary>
        /// <param name="inventory">비울 인벤토리이다.</param>
        private static void RemoveAllItems(IInventory inventory)
        {
            while (inventory.Items.Count > 0)
            {
                var itemStack = inventory.Items[inventory.Items.Count - 1];
                if (inventory.Remove(itemStack.Definition, itemStack.Amount) <= 0)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 저장된 아이템 하나의 Id와 수량을 나타낸다.
    /// </summary>
    [Serializable]
    public sealed class InventorySaveEntry
    {
        /// <summary>
        /// 저장된 아이템의 고유 Id이다.
        /// </summary>
        public string id;

        /// <summary>
        /// 저장된 아이템의 합산 수량이다.
        /// </summary>
        public int amount;
    }
}
