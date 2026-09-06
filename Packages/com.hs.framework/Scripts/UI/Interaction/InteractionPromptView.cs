using HS.Framework.Interaction;
using TMPro;
using UnityEngine;

namespace HS.Framework.UI.Interaction
{
    /// <summary>
    /// 상호작용 프롬프트 정보를 UGUI 요소에 표시한다.
    /// 직렬화 필드가 비어 있어도 오류 없이 동작한다.
    /// </summary>
    public sealed class InteractionPromptView : MonoBehaviour, IInteractionPromptPresenter
    {
        /// <summary>
        /// 프롬프트 전체를 켜고 끄는 루트 오브젝트이다.
        /// </summary>
        [SerializeField] private GameObject promptRoot;

        /// <summary>
        /// 루트 대신 알파로 표시를 제어할 CanvasGroup이다. 지정하면 루트 토글보다 우선한다.
        /// </summary>
        [SerializeField] private CanvasGroup canvasGroup;

        /// <summary>
        /// 상호작용 동작 이름을 표시하는 텍스트이다.
        /// </summary>
        [SerializeField] private TMP_Text actionText;

        /// <summary>
        /// 상호작용 대상 이름을 표시하는 텍스트이다.
        /// </summary>
        [SerializeField] private TMP_Text targetText;

        /// <summary>
        /// 상호작용 불가 이유를 표시하는 텍스트이다.
        /// </summary>
        [SerializeField] private TMP_Text unavailableReasonText;

        private void Awake()
        {
            Hide();
        }

        /// <inheritdoc />
        public void Show(InteractionPrompt prompt, InteractionAvailability availability)
        {
            if (actionText != null)
            {
                actionText.text = prompt.ActionText ?? string.Empty;
            }

            if (targetText != null)
            {
                targetText.text = prompt.TargetText ?? string.Empty;
            }

            if (unavailableReasonText != null)
            {
                var reason = InteractionPromptDisplay.ResolveUnavailableReason(availability);
                unavailableReasonText.text = reason ?? string.Empty;
                unavailableReasonText.gameObject.SetActive(reason != null);
            }

            SetVisible(true);
        }

        /// <inheritdoc />
        public void Hide()
        {
            SetVisible(false);
        }

        /// <summary>
        /// CanvasGroup이 있으면 알파로, 없으면 루트 오브젝트 토글로 표시 상태를 적용한다.
        /// </summary>
        private void SetVisible(bool isVisible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = isVisible ? 1f : 0f;
                canvasGroup.interactable = isVisible;
                canvasGroup.blocksRaycasts = isVisible;
                return;
            }

            if (promptRoot != null)
            {
                promptRoot.SetActive(isVisible);
            }
        }
    }
}
