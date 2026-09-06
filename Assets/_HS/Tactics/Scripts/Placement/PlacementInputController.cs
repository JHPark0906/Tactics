using HS.Framework.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 맵을 클릭한 지점을 배치·이동·회수 요청으로 바꿔 배치 창과 컨트롤러에 전달한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>조작 방법.</b> 빈 자리를 주 버튼으로 클릭하면 배치 창에서 선택해 둔 유닛을 그 자리에 놓는다.
    /// 이미 놓은 유닛을 주 버튼으로 클릭하면 그 유닛을 집어 들고, 다시 클릭한 자리로 옮긴다.
    /// 보조 버튼으로 클릭하면 그 자리의 유닛을 회수하며, 집어 든 상태에서는 옮기기를 그만둔다.
    /// </para>
    /// <para>
    /// <b>배치 단계에서만 동작한다.</b> 전투가 시작되면 클릭 자체를 무시한다.
    /// 배치 규칙은 <see cref="UnitPlacementPlan"/>이 이미 판정하므로 여기서 상한이나 구역을 다시 검사하지 않는다.
    /// 이 컴포넌트가 하는 일은 화면 좌표를 월드 좌표로 바꾸고, 그 좌표가 무엇을 뜻하는지 정해
    /// 기존 공개 API로 넘기는 것까지이다.
    /// </para>
    /// <para>
    /// <b>선택한 유닛이 없을 때.</b> 빈 자리를 클릭해도 아무 일도 일어나지 않는다.
    /// 무엇을 놓을지 알 수 없기 때문이며, 이때는 배치 창에서 유닛을 먼저 선택해야 한다.
    /// 다만 이미 놓은 유닛을 집어 들거나 회수하는 조작은 선택과 무관하게 동작한다.
    /// </para>
    /// <para>
    /// <b>입력 경로.</b> 정적 설정 접근자를 쓰지 않고 프레임워크의 <see cref="InputActionResolver"/>를 통해
    /// 입력 액션을 해석한다. 설정 서비스에 등록된 에셋이 있으면 그 에셋을, 없으면 폴백 에셋을 쓰며
    /// 설정이 바뀌면 액션을 다시 찾는다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlacementInputController : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("배치할 유닛 선택을 들고 있는 배치 창이다. 새로 놓는 조작을 쓰려면 반드시 연결한다. " +
                 "비워 두면 이미 놓은 유닛을 옮기고 회수하는 것만 가능하다.")]
        [SerializeField]
        private PlacementWindow placementWindow;

        [Tooltip("조작할 배치 컨트롤러이다. 비워 두면 위의 배치 창에 연결된 컨트롤러를 쓰므로 보통 비워 둔다. " +
                 "배치 창 없이 이 컴포넌트만 시험해 볼 때만 직접 연결한다.")]
        [SerializeField]
        private UnitPlacementController placementController;

        [Header("Pointer")]
        [Tooltip("화면 좌표를 월드로 바꿀 때 쓸 카메라이다. 비워 두면 MainCamera 태그가 붙은 카메라를 찾는다.")]
        [SerializeField]
        private Camera pointerCamera;

        [Tooltip("바닥으로 볼 레이어이다. 기본값은 모든 레이어이므로 처음에는 그대로 두어도 되고, " +
                 "유닛이나 UI 콜라이더가 클릭을 가로채면 바닥 레이어만 남기도록 좁힌다.")]
        [SerializeField]
        private LayerMask groundLayers = ~0;

        [Tooltip("바닥을 찾을 때 광선을 쏘는 최대 거리(미터)이다. 맵이 크면 늘린다.")]
        [SerializeField]
        [Min(1f)]
        private float maxRayDistance = 500f;

        [Tooltip("바닥 콜라이더에 맞지 않았을 때 가상의 수평면으로 좌표를 구할지 여부이다. " +
                 "바닥에 콜라이더를 아직 붙이지 않았어도 배치를 시험할 수 있게 해 주므로 켜 두기를 권한다.")]
        [SerializeField]
        private bool useGroundPlaneFallback = true;

        [Tooltip("위 대체 경로가 쓰는 가상 수평면의 높이(Y)이다. 보통 바닥 높이와 같게 둔다.")]
        [SerializeField]
        private float groundPlaneHeight;

        [Tooltip("이미 놓은 유닛을 클릭했다고 인정할 반경(미터)이다. " +
                 "너무 크면 빈 자리를 클릭해도 옆 유닛이 집히고, 너무 작으면 유닛을 집기 어렵다.")]
        [SerializeField]
        [Min(0.01f)]
        private float unitPickRadius = 1f;

        [Header("Input")]
        [Tooltip("입력 설정 서비스에 에셋이 등록되지 않았을 때 사용할 폴백 입력 액션 에셋이다. " +
                 "패키지의 DefaultInputActions를 연결해 두면 설정이 없어도 조작할 수 있다.")]
        [SerializeField]
        private InputActionAsset fallbackInputActions;

        [Tooltip("조작 액션을 찾을 입력 액션 맵의 이름이다. 기본 에셋에서는 UI 맵에 포인터 액션이 들어 있다.")]
        [SerializeField]
        private string inputActionMapName = "UI";

        [Tooltip("포인터의 화면 좌표를 읽을 액션 이름이다. 기본 에셋의 UI 맵에서는 Point이다.")]
        [SerializeField]
        private string pointActionName = "Point";

        [Tooltip("배치하거나 유닛을 집어 드는 주 버튼 액션 이름이다. 기본 에셋의 UI 맵에서는 Click이다.")]
        [SerializeField]
        private string primaryActionName = "Click";

        [Tooltip("회수하거나 옮기기를 취소하는 보조 버튼 액션 이름이다. 기본 에셋의 UI 맵에서는 RightClick이다. " +
                 "비워 두면 보조 조작을 쓰지 않는다.")]
        [SerializeField]
        private string secondaryActionName = "RightClick";

        private InputActionResolver _inputActionResolver;
        private InputAction _pointAction;
        private InputAction _primaryAction;
        private InputAction _secondaryAction;

        /// <summary>지금 집어 들어 옮기는 중인 배치 항목의 식별자이며, 없으면 0이다.</summary>
        public int HeldEntryId { get; private set; }

        /// <summary>조작 대상 배치 컨트롤러이며, 직접 지정하지 않았으면 배치 창에 연결된 컨트롤러이다.</summary>
        public UnitPlacementController PlacementController
        {
            get
            {
                if (placementController != null)
                {
                    return placementController;
                }

                return placementWindow != null ? placementWindow.PlacementController : null;
            }
        }

        /// <summary>지금 맵 조작을 받아들이는 단계인지 여부이다.</summary>
        public bool IsPlacementInputAllowed
        {
            get
            {
                var controller = PlacementController;
                return controller != null && controller.IsPlacementInputAllowed;
            }
        }

        /// <summary>배치할 유닛 선택을 들고 있는 배치 창을 연결하거나 교체한다.</summary>
        /// <param name="window">연결할 배치 창이다.</param>
        public void SetPlacementWindow(PlacementWindow window)
        {
            placementWindow = window;
            HeldEntryId = 0;
        }

        /// <summary>조작할 배치 컨트롤러를 직접 지정한다. null을 넘기면 배치 창의 컨트롤러를 따른다.</summary>
        /// <param name="controller">연결할 배치 컨트롤러이다.</param>
        public void SetPlacementController(UnitPlacementController controller)
        {
            placementController = controller;
            HeldEntryId = 0;
        }

        /// <summary>
        /// 화면 좌표에 대응하는 바닥 위의 월드 좌표를 구한다.
        /// 바닥 콜라이더를 먼저 찾고, 맞지 않으면 설정에 따라 가상 수평면으로 대체한다.
        /// </summary>
        /// <param name="screenPosition">포인터의 화면 좌표이다.</param>
        /// <param name="worldPosition">구한 월드 좌표이며 실패하면 기본값이다.</param>
        /// <returns>좌표를 구했으면 true이다.</returns>
        public bool TryResolveWorldPoint(Vector2 screenPosition, out Vector3 worldPosition)
        {
            worldPosition = default;
            var camera = ResolveCamera();
            if (camera == null)
            {
                return false;
            }

            var ray = camera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out var hit, maxRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                worldPosition = hit.point;
                return true;
            }

            return useGroundPlaneFallback &&
                   PlacementInputRules.TryProjectRayOntoPlane(ray, groundPlaneHeight, out worldPosition);
        }

        /// <summary>
        /// 주 조작이 일어난 월드 좌표를 처리한다.
        /// 포인터 장치 없이도 실제 처리 경로를 그대로 태울 수 있도록 공개해 둔 진입점이다.
        /// </summary>
        /// <param name="worldPosition">조작이 일어난 월드 좌표이다.</param>
        /// <returns>배치 계획이 답한 처리 결과이다.</returns>
        public PlacementResult HandlePrimaryPress(Vector3 worldPosition)
        {
            var controller = PlacementController;
            if (controller == null || !controller.IsPlacementInputAllowed)
            {
                HeldEntryId = 0;
                return PlacementResult.NotInPlacingPhase;
            }

            PlacementInputRules.TryPickEntry(controller.Plan.Entries, worldPosition, unitPickRadius, out var pickedEntryId);
            var command = PlacementInputRules.ResolvePrimaryCommand(HeldEntryId, pickedEntryId, HasSelectedDefinition());
            return ExecuteCommand(command, controller, worldPosition);
        }

        /// <summary>
        /// 보조 조작이 일어난 월드 좌표를 처리한다.
        /// 포인터 장치 없이도 실제 처리 경로를 그대로 태울 수 있도록 공개해 둔 진입점이다.
        /// </summary>
        /// <param name="worldPosition">조작이 일어난 월드 좌표이다.</param>
        /// <returns>배치 계획이 답한 처리 결과이다.</returns>
        public PlacementResult HandleSecondaryPress(Vector3 worldPosition)
        {
            var controller = PlacementController;
            if (controller == null || !controller.IsPlacementInputAllowed)
            {
                HeldEntryId = 0;
                return PlacementResult.NotInPlacingPhase;
            }

            PlacementInputRules.TryPickEntry(controller.Plan.Entries, worldPosition, unitPickRadius, out var pickedEntryId);
            var command = PlacementInputRules.ResolveSecondaryCommand(HeldEntryId, pickedEntryId);
            return ExecuteCommand(command, controller, worldPosition);
        }

        private void OnEnable()
        {
            _inputActionResolver = new InputActionResolver(fallbackInputActions, ResolveActions);
            ResolveActions();
        }

        private void OnDisable()
        {
            _inputActionResolver?.Dispose();
            _inputActionResolver = null;
            _pointAction = null;
            _primaryAction = null;
            _secondaryAction = null;
            HeldEntryId = 0;
        }

        private void Update()
        {
            if (!IsPlacementInputAllowed)
            {
                HeldEntryId = 0;
                return;
            }

            var primaryPressed = _primaryAction?.WasPressedThisFrame() == true;
            var secondaryPressed = _secondaryAction?.WasPressedThisFrame() == true;
            if (!primaryPressed && !secondaryPressed)
            {
                return;
            }

            if (!TryReadPointerPosition(out var screenPosition) ||
                !TryResolveWorldPoint(screenPosition, out var worldPosition))
            {
                return;
            }

            if (primaryPressed)
            {
                HandlePrimaryPress(worldPosition);
            }

            if (secondaryPressed)
            {
                HandleSecondaryPress(worldPosition);
            }
        }

        /// <summary>해석한 조작을 기존 배치 API로 전달한다.</summary>
        /// <param name="command">수행할 동작이다.</param>
        /// <param name="controller">조작할 배치 컨트롤러이다.</param>
        /// <param name="worldPosition">조작이 일어난 월드 좌표이다.</param>
        /// <returns>배치 계획이 답한 처리 결과이다.</returns>
        private PlacementResult ExecuteCommand(
            PlacementInputCommand command,
            UnitPlacementController controller,
            Vector3 worldPosition)
        {
            switch (command.Kind)
            {
                case PlacementInputActionKind.Place:
                    return placementWindow != null
                        ? placementWindow.RequestPlaceSelectedUnit(worldPosition)
                        : PlacementResult.MissingDefinition;

                case PlacementInputActionKind.BeginMove:
                    HeldEntryId = command.EntryId;
                    return PlacementResult.Success;

                case PlacementInputActionKind.Move:
                    var moveResult = controller.TryMoveUnit(command.EntryId, worldPosition);
                    if (moveResult == PlacementResult.Success)
                    {
                        HeldEntryId = 0;
                    }

                    return moveResult;

                case PlacementInputActionKind.Recall:
                    return placementWindow != null
                        ? placementWindow.RequestRecallUnit(command.EntryId)
                        : controller.TryRecallUnit(command.EntryId);

                case PlacementInputActionKind.CancelMove:
                    HeldEntryId = 0;
                    return PlacementResult.Success;

                default:
                    return PlacementResult.MissingDefinition;
            }
        }

        /// <summary>배치 창에서 유닛 정의를 선택해 두었는지 확인한다.</summary>
        /// <returns>선택된 정의가 있으면 true이다.</returns>
        private bool HasSelectedDefinition()
        {
            return placementWindow != null && placementWindow.SelectedUnitDefinition != null;
        }

        /// <summary>화면 좌표를 구할 카메라를 찾는다.</summary>
        /// <returns>사용할 카메라이며 없으면 null이다.</returns>
        private Camera ResolveCamera()
        {
            if (pointerCamera == null)
            {
                pointerCamera = Camera.main;
            }

            return pointerCamera;
        }

        /// <summary>포인터 액션에서 화면 좌표를 읽는다.</summary>
        /// <param name="screenPosition">읽은 화면 좌표이며 실패하면 기본값이다.</param>
        /// <returns>좌표를 읽었으면 true이다.</returns>
        private bool TryReadPointerPosition(out Vector2 screenPosition)
        {
            if (_pointAction != null)
            {
                screenPosition = _pointAction.ReadValue<Vector2>();
                return true;
            }

            // 포인터 액션을 찾지 못했더라도 마우스가 있으면 그 좌표로 배치를 시험할 수 있게 한다.
            if (Mouse.current != null)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        /// <summary>현재 해석 대상 에셋에서 조작 액션을 다시 찾는다. 입력 설정이 바뀔 때도 호출된다.</summary>
        private void ResolveActions()
        {
            _pointAction = FindAction(pointActionName);
            _primaryAction = FindAction(primaryActionName);
            _secondaryAction = FindAction(secondaryActionName);
        }

        /// <summary>설정한 액션 맵에서 이름으로 입력 액션을 찾는다.</summary>
        /// <param name="actionName">찾을 입력 액션의 이름이며 비어 있으면 찾지 않는다.</param>
        /// <returns>찾은 입력 액션이며 없으면 null이다.</returns>
        private InputAction FindAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            return _inputActionResolver?.FindAction(inputActionMapName, actionName);
        }
    }
}
