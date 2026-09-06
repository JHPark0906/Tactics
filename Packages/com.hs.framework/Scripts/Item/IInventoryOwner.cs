namespace HS.Framework.Item
{
    /// <summary>
    /// 캐릭터와 상호작용 조건에서 사용할 인벤토리 보유자 계약이다.
    /// </summary>
    public interface IInventoryOwner
    {
        /// <summary>
        /// 보유자의 인벤토리를 가져온다.
        /// </summary>
        IInventory Inventory { get; }
    }
}
