using System;
using System.Collections.Generic;
using HS.Framework.Foundation.Input;
using HS.Framework.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace HS.Framework.UI.Windows
{
    /// <summary>
    /// 씬에 배치되어 UI 창의 열기/닫기, 스택 순서, 취소(ESC) 입력, 캔버스 정렬을 관리한다.
    /// 모달 창이 열려 있는 동안에는 입력 차단 토큰을 보유해 게임플레이 입력을 막는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiWindowManager : MonoBehaviour
    {
        /// <summary>
        /// 시작 시 미리 등록할 창 목록이다.
        /// </summary>
        [Header("Windows")]
        [SerializeField] private UiWindowBase[] initialWindows;

        /// <summary>
        /// InputSettingsService에 에셋이 등록되지 않았을 때 사용할 폴백 입력 액션 에셋이다.
        /// </summary>
        [Header("Input")]
        [SerializeField] private InputActionAsset fallbackInputActions;

        /// <summary>
        /// 취소 액션을 찾을 입력 액션 맵의 이름이다.
        /// </summary>
        [SerializeField] private string inputActionMapName = "UI";

        /// <summary>
        /// 최상단 창을 닫는 취소 액션의 이름이다.
        /// </summary>
        [SerializeField] private string cancelActionName = "Cancel";

        /// <summary>
        /// 가장 아래 창에 적용할 캔버스 정렬 순서이다.
        /// </summary>
        [Header("Layering")]
        [SerializeField] private int baseSortingOrder = 100;

        /// <summary>
        /// 스택에서 한 단계 위 창마다 더할 정렬 순서 간격이다.
        /// </summary>
        [SerializeField] [Min(1)] private int sortingOrderStep = 10;

        private readonly UiWindowStack _stack = new();
        private readonly List<UiWindowBase> _registeredWindows = new();
        private InputActionResolver _inputActionResolver;
        private InputAction _cancelAction;
        private IInputStateController _inputStateController;
        private IDisposable _inputBlockToken;
        private bool _isDestroying;

        /// <summary>
        /// 현재 최상단에 열린 창을 가져온다. 열린 창이 없으면 null을 반환한다.
        /// </summary>
        public UiWindowBase TopWindow => _stack.Top as UiWindowBase;

        /// <summary>
        /// 현재 열린 창의 수를 가져온다.
        /// </summary>
        public int OpenWindowCount => _stack.Count;

        /// <summary>
        /// 모달 창이 하나라도 열려 있는지 여부를 가져온다.
        /// </summary>
        public bool HasOpenModal => _stack.HasModal;

        /// <summary>
        /// 창이 자기보다 위의 모달 창에 의해 조작이 차단되는지 여부를 반환한다.
        /// </summary>
        /// <param name="window">확인할 창이다.</param>
        public bool IsBlockedByModal(UiWindowBase window)
        {
            return _stack.IsBlockedByModal(window);
        }

        /// <summary>
        /// 게임플레이 입력을 차단할 때 사용할 입력 상태 제어 계약을 주입받는다.
        /// 주입 시점에 이미 모달 창이 열려 있으면 즉시 차단 토큰을 획득하고,
        /// 다른 컨트롤러로 바뀌면 이전 컨트롤러에서 받은 토큰을 먼저 반환한다.
        /// </summary>
        /// <param name="inputStateController">입력 차단 토큰을 발급하는 컨트롤러이다.</param>
        [Inject]
        public void InjectInputStateController(IInputStateController inputStateController)
        {
            if (ReferenceEquals(_inputStateController, inputStateController))
            {
                return;
            }

            ReleaseInputBlock();
            _inputStateController = inputStateController;
            ApplyModalInputBlock();
        }

        private void Awake()
        {
            if (initialWindows == null)
            {
                return;
            }

            foreach (var window in initialWindows)
            {
                Register(window);
            }
        }

        private void OnEnable()
        {
            _inputActionResolver = new InputActionResolver(fallbackInputActions, ResolveCancelAction);
            ResolveCancelAction();
            ApplyModalInputBlock();
        }

        /// <summary>
        /// 비활성화되면 취소 입력 해석을 정리하고 입력 차단을 해제한다.
        /// 관리자가 멈춘 동안 차단만 남으면 되돌릴 주체가 없으므로, 모달 창이 열려 있어도 토큰을 반환한다.
        /// 다시 활성화되면 열린 모달 상태에 맞춰 토큰을 재획득한다.
        /// </summary>
        private void OnDisable()
        {
            _inputActionResolver?.Dispose();
            _inputActionResolver = null;
            _cancelAction = null;
            ReleaseInputBlock();
        }

        private void OnDestroy()
        {
            _isDestroying = true;
            var windows = _registeredWindows.ToArray();
            for (var index = windows.Length - 1; index >= 0; index--)
            {
                Unregister(windows[index]);
            }

            ReleaseInputBlock();
            _inputStateController = null;
        }

        private void Update()
        {
            if (_cancelAction?.WasPressedThisFrame() == true)
            {
                HandleCancelPressed();
            }
        }

        /// <summary>
        /// 창을 관리 대상으로 등록한다. null이거나 이미 등록된 창이면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="window">등록할 창이다.</param>
        public void Register(UiWindowBase window)
        {
            if (_isDestroying || window == null || _registeredWindows.Contains(window))
            {
                return;
            }

            _registeredWindows.Add(window);
            window.Opened += HandleWindowOpened;
            window.Closed += HandleWindowClosed;
            window.CloseRequested += CloseWindow;

            if (window.IsOpen)
            {
                HandleWindowOpened(window);
            }
        }

        /// <summary>
        /// 등록·구독·스택을 먼저 해제한 뒤 창을 닫는다. 닫힘 콜백에서 다시 등록한 창은 새 등록으로 유지한다.
        /// </summary>
        /// <param name="window">해제할 창이다.</param>
        public void Unregister(UiWindowBase window)
        {
            if (window == null || !_registeredWindows.Remove(window))
            {
                return;
            }

            window.Opened -= HandleWindowOpened;
            window.Closed -= HandleWindowClosed;
            window.CloseRequested -= CloseWindow;
            HandleWindowClosed(window);
            window.Close();
        }

        /// <summary>
        /// 창을 등록하고 연다. 이미 열려 있으면 최상단으로 끌어올린다.
        /// </summary>
        /// <param name="window">열 창이다.</param>
        public void OpenWindow(UiWindowBase window)
        {
            if (_isDestroying || window == null)
            {
                return;
            }

            Register(window);
            if (window.IsOpen)
            {
                HandleWindowOpened(window);
                return;
            }

            window.Open();
        }

        /// <summary>
        /// 창을 닫는다. 스택 제거는 창의 닫힘 이벤트를 통해 일관되게 처리된다.
        /// </summary>
        /// <param name="window">닫을 창이다.</param>
        public void CloseWindow(UiWindowBase window)
        {
            if (window != null)
            {
                window.Close();
            }
        }

        /// <summary>
        /// 최상단 창을 닫는다. 열린 창이 없으면 아무것도 하지 않는다.
        /// </summary>
        public void CloseTopWindow()
        {
            CloseWindow(TopWindow);
        }

        /// <summary>
        /// 호출 시 열려 있던 요청만 위에서부터 닫는다. 콜백이 새로 열거나 같은 창에 교체한 요청은 유지한다.
        /// </summary>
        public void CloseAllWindows()
        {
            var targets = new List<(UiWindowBase Window, long Version)>(_stack.Count);
            foreach (var window in _stack.Windows)
            {
                if (window is UiWindowBase target)
                {
                    targets.Add((target, target.RequestVersion));
                }
            }

            for (var index = targets.Count - 1; index >= 0; index--)
            {
                var (window, version) = targets[index];
                if (window != null && window.IsOpen && window.RequestVersion == version)
                {
                    window.Close();
                }
            }

            ApplyModalInputBlock();
        }

        /// <summary>
        /// 취소 입력을 처리한다. 최상단 창이 취소로 닫기를 허용할 때만 그 창을 닫는다.
        /// </summary>
        private void HandleCancelPressed()
        {
            CloseWindow(_stack.PeekCancelTarget() as UiWindowBase);
        }

        /// <summary>
        /// 창이 열리면 스택에 푸시하고 정렬 순서와 모달 상태를 갱신한다.
        /// </summary>
        /// <param name="window">열린 창이다.</param>
        private void HandleWindowOpened(UiWindowBase window)
        {
            _stack.Push(window);
            ApplySortingOrders();
            ApplyModalInputBlock();
        }

        /// <summary>
        /// 창이 닫히면 스택에서 제거하고 정렬 순서와 모달 상태를 갱신한다.
        /// </summary>
        /// <param name="window">닫힌 창이다.</param>
        private void HandleWindowClosed(UiWindowBase window)
        {
            if (!_stack.Remove(window))
            {
                return;
            }

            ApplySortingOrders();
            ApplyModalInputBlock();
        }

        /// <summary>
        /// 스택 순서에 따라 각 창의 캔버스 정렬 순서를 적용한다.
        /// </summary>
        private void ApplySortingOrders()
        {
            var windows = _stack.Windows;
            for (var index = 0; index < windows.Count; index++)
            {
                (windows[index] as UiWindowBase)?.SetSortingOrder(baseSortingOrder + index * sortingOrderStep);
            }
        }

        /// <summary>
        /// 모달 창이 하나라도 열려 있으면 입력 차단 토큰을 보유하고, 모두 닫히면 반환한다.
        /// 토큰 보유 여부만 보고 판단하므로 몇 번 호출해도 결과가 같다.
        /// 다른 차단 주체(일시정지·씬 전환)와는 토큰 단위로 합성되므로 서로의 상태를 덮어쓰지 않는다.
        /// </summary>
        /// <remarks>
        /// <b>차단 범위는 <see cref="InputBlockScope.Gameplay"/>이다.</b> 모달 창은 자기 자신을 조작하게 하려고 여는 것이므로
        /// UI 입력까지 막으면 창이 자기 클릭과 취소 입력을 스스로 죽인다. 확인 대화상자가 눌리지 않는 상태가
        /// 바로 그 결과다. 모달이 막아야 하는 것은 창 아래의 게임플레이이지 창 자신이 아니다.
        /// </remarks>
        private void ApplyModalInputBlock()
        {
            if (_inputStateController == null || !isActiveAndEnabled)
            {
                return;
            }

            if (_stack.HasModal)
            {
                _inputBlockToken ??= _inputStateController.AcquireInputBlock(InputBlockScope.Gameplay);
            }
            else
            {
                ReleaseInputBlock();
            }
        }

        /// <summary>
        /// 보유 중인 입력 차단 토큰을 반환한다. 토큰이 없으면 아무것도 하지 않는다.
        /// </summary>
        private void ReleaseInputBlock()
        {
            _inputBlockToken?.Dispose();
            _inputBlockToken = null;
        }

        /// <summary>
        /// 현재 해석 대상 입력 에셋에서 취소 액션을 다시 찾는다.
        /// </summary>
        private void ResolveCancelAction()
        {
            _cancelAction = _inputActionResolver?.FindAction(inputActionMapName, cancelActionName);
        }
    }
}
