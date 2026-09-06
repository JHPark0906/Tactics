using UnityEngine;

namespace HS.Framework.Item
{
    /// <summary>
    /// 인벤토리에 저장 가능한 아이템의 불변 정의 데이터이다.
    /// Unity ScriptableObject 에셋으로 생성해 여러 프리팹에서 공유한다.
    /// </summary>
    public abstract class ItemDefinition : ScriptableObject
    {
        /// <summary>
        /// 저장과 조회에 사용할 고유 식별자를 가져온다.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// UI에 표시할 아이템 이름을 가져온다.
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// 한 슬롯에 쌓을 수 있는 최대 수량을 가져온다.
        /// </summary>
        public abstract int MaxStackSize { get; }
    }
}