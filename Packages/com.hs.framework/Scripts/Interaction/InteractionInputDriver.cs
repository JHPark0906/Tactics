using HS.Framework.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Framework.Interaction
{
    /// <summary>
    /// Input System 입력으로 InteractionController의 포커스 갱신과 상호작용 실행을 구동한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionController))]
    public sealed class InteractionInputDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InteractionController interactionController;

        [Header("Input")]
        [SerializeField] private InputActionAsset fallbackInputActions;
        [SerializeField] private string inputActionMapName = "Player";
        [SerializeField] private string interactActionName = "Interact";

        [Header("Focus")]
        [Tooltip("포커스 재탐색 주기(초)이다. 0이면 매 프레임 갱신한다.")]
        [SerializeField] [Min(0f)] private float focusRefreshInterval = 0.1f;

        private InputActionResolver _inputActionResolver;
        private InputAction _interactAction;
        private float _nextRefreshTime;

        /// <summary>인스펙터 참조가 비어 있으면 같은 오브젝트에서 상호작용 컨트롤러를 찾는다.</summary>
        /// <remarks>
        /// 직렬화된 오브젝트 참조는 비어 있어도 C# null 이 아닐 수 있으므로 Unity 의 null 비교로 확인한다.
        /// <c>??=</c> 는 C# null 만 보아 그 경우 대입을 건너뛴다.
        /// </remarks>
        private void Awake()
        {
            if (interactionController == null)
            {
                interactionController = GetComponent<InteractionController>();
            }
        }

        private void OnEnable()
        {
            _inputActionResolver = new InputActionResolver(fallbackInputActions, ResolveInputActions);
            ResolveInputActions();
            _nextRefreshTime = 0f;
        }

        private void OnDisable()
        {
            _inputActionResolver?.Dispose();
            _inputActionResolver = null;
            _interactAction = null;
            if (interactionController != null && interactionController.isActiveAndEnabled)
            {
                interactionController.ClearFocus();
            }
        }

        private void Update()
        {
            if (interactionController == null)
            {
                return;
            }

            if (focusRefreshInterval <= 0f || Time.time >= _nextRefreshTime)
            {
                interactionController.RefreshFocus();
                _nextRefreshTime = Time.time + focusRefreshInterval;
            }

            if (_interactAction?.WasPressedThisFrame() == true)
            {
                interactionController.TryInteract();
            }
        }

        private void ResolveInputActions()
        {
            _interactAction = _inputActionResolver?.FindAction(inputActionMapName, interactActionName);
        }
    }
}
