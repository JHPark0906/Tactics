using HS.Framework.Item;
using UnityEngine;

namespace HS.Framework.Interaction.Actions
{
    /// <summary>
    /// 상호작용 주체의 인벤토리에 지정한 아이템을 지급한다.
    /// </summary>
    /// <remarks>
    /// 상호작용과 아이템을 잇는 유일한 동작이라 별도의 브리지 어셈블리에 둔다.
    /// 이 동작이 상호작용 모듈 안에 있으면 아이템을 쓰지 않는 프로젝트도 상호작용만으로
    /// 아이템과 저장 모듈을 함께 끌어오게 되고, 반대로 아이템 모듈로 옮기면 저수준 모듈인
    /// 아이템이 상호작용의 씬·설정·입력 의존까지 떠안는다. 양쪽을 참조하는 얇은 어셈블리가
    /// 두 모듈을 서로 독립으로 유지하는 방법이다.
    /// </remarks>
    public sealed class InteractionItemGrantAction : MonoBehaviour, IInteractionAction
    {
        [SerializeField] private ItemDefinition itemDefinition;
        [SerializeField] [Min(1)] private int amount = 1;

        private int _remainingAmount;

        /// <summary>아직 획득되지 않은 아이템 수량이다.</summary>
        public int RemainingAmount => _remainingAmount;

        private void Awake()
        {
            _remainingAmount = Mathf.Max(0, amount);
        }

        /// <inheritdoc />
        public void Execute(IInteractor interactor)
        {
            if (itemDefinition == null || _remainingAmount <= 0 || interactor?.GameObject == null)
            {
                return;
            }

            if (!TryGetInventory(interactor.GameObject, out var inventory))
            {
                Debug.LogWarning("[InteractionItemGrantAction] 상호작용 주체에서 IInventoryOwner를 찾을 수 없습니다.", interactor.GameObject);
                return;
            }

            var addedAmount = inventory.Add(new ItemStack(itemDefinition, _remainingAmount));
            _remainingAmount -= addedAmount;
            if (_remainingAmount == 0)
            {
                Destroy(gameObject);
            }
        }

        private static bool TryGetInventory(GameObject gameObject, out IInventory inventory)
        {
            foreach (var behaviour in gameObject.GetComponentsInParent<MonoBehaviour>())
            {
                if (behaviour is IInventoryOwner inventoryOwner && inventoryOwner.Inventory != null)
                {
                    inventory = inventoryOwner.Inventory;
                    return true;
                }
            }

            inventory = null;
            return false;
        }
    }
}
