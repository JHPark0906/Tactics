namespace HS.Framework.Item
{
    /// <summary>
    /// 인벤토리에서 발생한 아이템 수량 변경의 종류를 나타낸다.
    /// </summary>
    public enum InventoryChangeType
    {
        Added,
        Removed
    }

    /// <summary>
    /// 인벤토리 변경을 UI와 저장 시스템에 전달한다.
    /// </summary>
    public readonly struct InventoryChangedEvent
    {
        /// <summary>
        /// 발생한 변경의 종류를 가져온다.
        /// </summary>
        public InventoryChangeType ChangeType { get; }

        /// <summary>
        /// 변경된 아이템 정의를 가져온다.
        /// </summary>
        public ItemDefinition Definition { get; }

        /// <summary>
        /// 실제로 변경된 아이템 수량을 가져온다.
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// 변경 이후의 인벤토리 revision을 가져온다.
        /// </summary>
        public long Revision { get; }

        /// <summary>
        /// 인벤토리 변경 이벤트를 생성한다.
        /// </summary>
        public InventoryChangedEvent(InventoryChangeType changeType, ItemDefinition definition, int amount, long revision)
        {
            ChangeType = changeType;
            Definition = definition;
            Amount = amount;
            Revision = revision;
        }
    }
}
