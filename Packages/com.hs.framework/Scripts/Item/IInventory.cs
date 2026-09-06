using System.Collections.Generic;
using R3;

namespace HS.Framework.Item
{
    /// <summary>
    /// 아이템 추가, 제거 및 조회를 제공하는 단일 플레이어 인벤토리 계약이다.
    /// </summary>
    public interface IInventory
    {
        /// <summary>
        /// 보관할 수 있는 최대 ItemStack 수를 가져온다.
        /// 같은 아이템도 최대 스택 수를 초과하면 새 ItemStack을 사용한다.
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// 마지막 저장 또는 로드 이후 아이템 상태가 변경되었는지 가져온다.
        /// </summary>
        bool IsDirty { get; }

        /// <summary>
        /// 아이템 상태가 실제로 변경될 때마다 증가하는 revision을 가져온다.
        /// </summary>
        long Revision { get; }

        /// <summary>
        /// 아이템 수량 변경을 구독할 수 있는 R3 스트림을 가져온다.
        /// </summary>
        Observable<InventoryChangedEvent> Changed { get; }

        /// <summary>
        /// 현재 인벤토리의 아이템 스택 목록을 가져온다.
        /// </summary>
        IReadOnlyList<ItemStack> Items { get; }

        /// <summary>
        /// 기존 스택의 여유 공간과 남은 ItemStack 용량을 기준으로 지정한 아이템 수량을 모두 추가할 수 있는지 검사한다.
        /// </summary>
        bool CanAdd(ItemStack itemStack);

        /// <summary>
        /// 지정한 아이템 수량을 추가하고 실제로 추가한 수량을 반환한다.
        /// </summary>
        int Add(ItemStack itemStack);

        /// <summary>
        /// 지정한 아이템 수량을 제거하고 실제로 제거한 수량을 반환한다.
        /// </summary>
        int Remove(ItemDefinition definition, int amount);

        /// <summary>
        /// 지정한 아이템을 요구 수량 이상 보유하는지 확인한다.
        /// </summary>
        bool Contains(ItemDefinition definition, int amount = 1);

        /// <summary>
        /// 저장에 사용한 revision이 현재 revision과 같을 때만 dirty 상태를 해제한다.
        /// </summary>
        bool AcknowledgeSaved(long savedRevision);
    }
}
