using UnityEngine;

namespace HS.Framework.Interaction
{
    /// <summary>
    /// 월드 오브젝트에 상호작용 기능을 제공하는 기본 컴포넌트다.
    /// </summary>
    public abstract class InteractableBehaviour : MonoBehaviour, IInteractable
    {
        [SerializeField] private string actionText = "Interact";
        [SerializeField] private string targetText;

        /// <inheritdoc />
        public InteractionPrompt Prompt => new(actionText, targetText);

        /// <inheritdoc />
        public virtual InteractionAvailability Evaluate(IInteractor interactor)
        {
            return new InteractionAvailability(interactor != null);
        }

        /// <inheritdoc />
        public abstract void Interact(IInteractor interactor);
    }
}
