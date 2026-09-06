using System;

namespace HS.Framework.Item
{
    /// <summary>
    /// 아이템 정의와 수량을 함께 보관하는 읽기 전용 값이다.
    /// </summary>
    [Serializable]
    public readonly struct ItemStack
    {
        /// <summary>
        /// 아이템 정의를 가져온다.
        /// </summary>
        public ItemDefinition Definition { get; }

        /// <summary>
        /// 보관 중인 수량을 가져온다.
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// 아이템 스택을 생성한다.
        /// </summary>
        public ItemStack(ItemDefinition definition, int amount)
        {
            Definition = definition;
            Amount = amount;
        }
    }
}