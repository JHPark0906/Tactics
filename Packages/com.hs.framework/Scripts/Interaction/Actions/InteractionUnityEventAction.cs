using UnityEngine;
using UnityEngine.Events;

namespace HS.Framework.Interaction.Actions
{
    /// <summary>
    /// 인스펙터에 연결한 UnityEvent를 상호작용 동작으로 실행한다.
    /// </summary>
    public sealed class InteractionUnityEventAction : MonoBehaviour, IInteractionAction
    {
        [SerializeField] private UnityEvent<GameObject> onExecuted;

        /// <inheritdoc />
        public void Execute(IInteractor interactor)
        {
            onExecuted?.Invoke(interactor?.GameObject);
        }
    }
}
