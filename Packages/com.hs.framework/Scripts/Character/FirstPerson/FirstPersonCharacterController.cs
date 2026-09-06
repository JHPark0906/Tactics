using HS.Framework.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Framework.Character.FirstPerson
{
    /// <summary>
    /// CharacterController와 Input System을 사용해 기본 1인칭 이동을 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonCharacterController : MonoBehaviour, ICharacterComponent
    {
        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Transform movementReference;

        [Header("Input")]
        [SerializeField] private InputActionAsset fallbackInputActions;
        [SerializeField] private string inputActionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string sprintActionName = "Sprint";
        [SerializeField] private string crouchActionName = "Crouch";

        [Header("Movement")]
        [SerializeField] [Min(0f)] private float walkSpeed = 4f;
        [SerializeField] [Min(0f)] private float sprintSpeed = 7f;
        [SerializeField] [Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;

        [Header("Crouch")]
        [SerializeField] [Min(0.1f)] private float crouchingHeight = 1.2f;
        [SerializeField] [Min(0f)] private float crouchTransitionSpeed = 8f;
        [SerializeField] private LayerMask standingCollisionMask = Physics.DefaultRaycastLayers;

        private InputAction _crouchAction;
        private InputActionResolver _inputActionResolver;
        private InputAction _jumpAction;
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private bool _isInitialized;
        private float _standingHeight;
        private float _verticalVelocity;

        /// <summary>
        /// 이 컨트롤러를 소유한 캐릭터이며, 캐릭터 조립을 거치지 않았으면 null이다.
        /// </summary>
        public CharacterBase Owner { get; private set; }

        /// <summary>
        /// 캐릭터 조립 과정에서 소유 캐릭터를 연결한다.
        /// 조립 순서와 무관하게 동작하도록 내부 초기화는 한 번만 수행한다.
        /// </summary>
        public void Initialize(CharacterBase characterBase)
        {
            Owner = characterBase;
            EnsureInitialized();
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// 이동에 필요한 구성요소와 기준 높이를 한 번만 준비한다.
        /// Awake와 캐릭터 조립 중 먼저 도달한 경로에서 실행된다.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            // 직렬화된 참조는 비어 있어도 C# null 이 아닐 수 있으므로 Unity 의 null 비교로 확인한다.
            // ??= 로 건너뛰면 이미 있는 캐릭터 컨트롤러를 두고 하나를 더 붙이게 된다.
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
            }

            if (movementReference == null)
            {
                movementReference = transform;
            }

            _standingHeight = characterController.height;
            crouchingHeight = Mathf.Min(crouchingHeight, _standingHeight);
            _isInitialized = true;
        }

        private void OnEnable()
        {
            _inputActionResolver = new InputActionResolver(fallbackInputActions, ResolveInputActions);
            ResolveInputActions();
        }

        private void OnDisable()
        {
            _inputActionResolver?.Dispose();
            _inputActionResolver = null;
            _moveAction = null;
            _jumpAction = null;
            _sprintAction = null;
            _crouchAction = null;
        }

        private void Update()
        {
            UpdateCrouch();
            UpdateMovement();
        }

        private void UpdateMovement()
        {
            if (characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            if (_jumpAction?.WasPressedThisFrame() == true && characterController.isGrounded)
            {
                _verticalVelocity = CalculateJumpVelocity(jumpHeight, gravity);
            }

            _verticalVelocity += gravity * Time.deltaTime;

            var input = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            input = Vector2.ClampMagnitude(input, 1f);
            var speed = _sprintAction?.IsPressed() == true && !IsCrouching ? sprintSpeed : walkSpeed;
            var forward = Vector3.ProjectOnPlane(movementReference.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = transform.forward;
            }

            var right = Vector3.Cross(Vector3.up, forward);
            var movement = (right * input.x + forward * input.y) * speed;
            movement.y = _verticalVelocity;
            characterController.Move(movement * Time.deltaTime);
        }

        private void UpdateCrouch()
        {
            var shouldCrouch = _crouchAction?.IsPressed() == true;
            if (!shouldCrouch && !CanStand())
            {
                shouldCrouch = true;
            }

            var targetHeight = shouldCrouch ? crouchingHeight : _standingHeight;
            var height = Mathf.MoveTowards(
                characterController.height,
                targetHeight,
                crouchTransitionSpeed * Time.deltaTime);
            SetCharacterControllerHeight(height);
        }

        private bool IsCrouching => characterController.height < _standingHeight - 0.01f;

        private bool CanStand()
        {
            if (characterController.height >= _standingHeight - 0.01f)
            {
                return true;
            }

            var radius = characterController.radius;
            var bottom = transform.position + characterController.center - Vector3.up * (characterController.height * 0.5f);
            var top = bottom + Vector3.up * (_standingHeight - radius);
            var bottomSphere = bottom + Vector3.up * radius;
            return !Physics.CheckCapsule(
                bottomSphere,
                top,
                radius,
                standingCollisionMask,
                QueryTriggerInteraction.Ignore);
        }

        private void SetCharacterControllerHeight(float height)
        {
            var clampedHeight = Mathf.Max(height, characterController.radius * 2f);
            var bottom = characterController.center.y - characterController.height * 0.5f;
            characterController.height = clampedHeight;
            characterController.center = new Vector3(
                characterController.center.x,
                bottom + clampedHeight * 0.5f,
                characterController.center.z);
        }

        private void ResolveInputActions()
        {
            var actionMap = _inputActionResolver?.FindActionMap(inputActionMapName);
            _moveAction = actionMap?.FindAction(moveActionName);
            _jumpAction = actionMap?.FindAction(jumpActionName);
            _sprintAction = actionMap?.FindAction(sprintActionName);
            _crouchAction = actionMap?.FindAction(crouchActionName);
        }

        private static float CalculateJumpVelocity(float height, float gravityAcceleration)
        {
            if (height <= 0f || gravityAcceleration >= 0f)
            {
                return 0f;
            }

            return Mathf.Sqrt(height * -2f * gravityAcceleration);
        }
    }
}
