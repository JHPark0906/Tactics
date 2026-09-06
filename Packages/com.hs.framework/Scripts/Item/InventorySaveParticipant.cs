using System;
using HS.Framework.Item;
using HS.Framework.Persistence;
using UnityEngine;

namespace HS.Framework.Item
{
    /// <summary>
    /// 인벤토리를 SaveOrchestrator의 저장 참여자로 연결하는 어댑터이다.
    /// 캡처 시점의 revision을 기록해 두었다가 저장 성공 시 AcknowledgeSaved(revision)로 dirty를 해제하므로,
    /// 캡처와 기록 사이에 인벤토리가 다시 변경되면 dirty가 유지되어 다음 자동 저장에서 재기록된다.
    /// </summary>
    public sealed class InventorySaveParticipant : ISaveable
    {
        /// <summary>
        /// 저장·복원 대상 인벤토리이다.
        /// </summary>
        private readonly IInventory _inventory;

        /// <summary>
        /// 저장된 Id를 정의로 되돌릴 아이템 카탈로그이다.
        /// </summary>
        private readonly ItemCatalog _catalog;

        /// <summary>
        /// 저장소에서 이 인벤토리를 식별하는 키이다.
        /// </summary>
        private readonly string _saveKey;

        /// <summary>
        /// 마지막 CaptureState 시점의 인벤토리 revision이다.
        /// </summary>
        private long _capturedRevision;

        /// <summary>
        /// 인벤토리 저장 참여자를 생성한다.
        /// </summary>
        /// <param name="inventory">저장·복원할 인벤토리이다.</param>
        /// <param name="catalog">Id를 정의로 되돌릴 아이템 카탈로그이다.</param>
        /// <param name="saveKey">저장소에서 이 인벤토리를 식별할 키이다.</param>
        public InventorySaveParticipant(IInventory inventory, ItemCatalog catalog, string saveKey)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (string.IsNullOrWhiteSpace(saveKey))
            {
                throw new ArgumentException("저장소 키는 비어 있을 수 없습니다.", nameof(saveKey));
            }

            _saveKey = saveKey;
        }

        /// <inheritdoc />
        public string SaveKey => _saveKey;

        /// <inheritdoc />
        public bool IsDirty => _inventory.IsDirty;

        /// <inheritdoc />
        public string CaptureState()
        {
            _capturedRevision = _inventory.Revision;
            return JsonUtility.ToJson(InventorySaveData.Capture(_inventory));
        }

        /// <inheritdoc />
        public void RestoreState(string serializedState)
        {
            if (string.IsNullOrWhiteSpace(serializedState))
            {
                return;
            }

            var data = JsonUtility.FromJson<InventorySaveData>(serializedState);
            if (data == null)
            {
                return;
            }

            InventorySaveData.Restore(_inventory, data, _catalog);

            // 복원 직후 상태는 저장소와 일치하므로 복원 과정에서 올라간 dirty를 해제한다.
            _inventory.AcknowledgeSaved(_inventory.Revision);
        }

        /// <inheritdoc />
        public void AcknowledgeSaved()
        {
            _inventory.AcknowledgeSaved(_capturedRevision);
        }
    }
}
