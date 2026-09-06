using HS.Framework.Settings;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Framework.Character.FirstPerson
{
    /// <summary>
    /// 전역 Input System의 시점 입력을 Cinemachine Pan Tilt 축으로 전달한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachinePanTilt))]
    public sealed class FirstPersonCinemachineLookInput : MonoBehaviour
    {
        [SerializeField] private CinemachinePanTilt panTilt;
        [SerializeField] private InputActionAsset fallbackInputActions;
        [SerializeField] private string inputActionMapName = "Player";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] [Min(0f)] private float lookSensitivity = 0.1f;
        [SerializeField] private bool lockCursorOnEnable = true;

        private InputActionResolver _inputActionResolver;
        private InputAction _lookAction;

        /// <summary>인스펙터 참조가 비어 있으면 같은 오브젝트에서 팬틸트를 찾는다.</summary>
        /// <remarks>
        /// 직렬화된 오브젝트 참조는 비어 있어도 C# null 이 아닐 수 있으므로 Unity 의 null 비교로 확인한다.
        /// <c>??=</c> 는 C# null 만 보아 그 경우 대입을 건너뛴다.
        /// </remarks>
        private void Awake()
        {
            if (panTilt == null)
            {
                panTilt = GetComponent<CinemachinePanTilt>();
            }
        }

        private void OnEnable()
        {
            _inputActionResolver = new InputActionResolver(fallbackInputActions, ResolveInputAction);
            ResolveInputAction();
            SetCursorLock(lockCursorOnEnable);
        }

        private void OnDisable()
        {
            _inputActionResolver?.Dispose();
            _inputActionResolver = null;
            _lookAction = null;
            if (lockCursorOnEnable)
            {
                SetCursorLock(false);
            }
        }

        private void Update()
        {
            if (panTilt == null)
            {
                return;
            }

            var look = _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
            SetAxisValue(ref panTilt.PanAxis, look.x * lookSensitivity);
            SetAxisValue(ref panTilt.TiltAxis, -look.y * lookSensitivity);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && isActiveAndEnabled && lockCursorOnEnable)
            {
                SetCursorLock(true);
            }
        }

        private void ResolveInputAction()
        {
            _lookAction = _inputActionResolver?.FindAction(inputActionMapName, lookActionName);
        }

        private static void SetAxisValue(ref InputAxis axis, float input)
        {
            axis.Value = axis.ClampValue(axis.Value + input);
        }

        private static void SetCursorLock(bool isLocked)
        {
            Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isLocked;
        }
    }
}
