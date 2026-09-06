using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HS.Framework.Foundation.Input;
using HS.Framework.Foundation.MVVM;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 설정창에서 편집하는 단일 입력 바인딩 항목을 표현한다.
    /// </summary>
    public sealed class InputBindingItemViewModel : ChangeTrackingViewModelBase, IEditableViewModel
    {

        /// <summary>
        /// 같은 액션에서 동시에 수행 중인 재지정을 막는 잠금 목록이다.
        /// </summary>
        private static readonly HashSet<InputAction> RebindingActions = new();

        private readonly IInputStateController _inputStateController;

        /// <summary>
        /// UI에서 편집 중인 바인딩 경로이다.
        /// </summary>
        private string _pendingPath;

        /// <summary>
        /// 적용된 바인딩 경로이다.
        /// </summary>
        private string _appliedPath;

        /// <summary>
        /// 대화형 입력 재지정을 수행 중인지 여부이다.
        /// </summary>
        private bool _isRebinding;

        /// <summary>
        /// 현재 실행 중인 대화형 재지정 작업이다.
        /// </summary>
        private InputActionRebindingExtensions.RebindingOperation _rebindingOperation;

        /// <summary>
        /// 현재 재지정 작업의 완료를 알리는 소스이다.
        /// </summary>
        private UniTaskCompletionSource _rebindingCompletionSource;

        /// <summary>
        /// 재지정 시작 전 액션의 활성화 상태이다.
        /// </summary>
        private bool _wasActionEnabled;

        /// <summary>
        /// 입력 바인딩 항목 ViewModel을 생성한다.
        /// </summary>
        /// <param name="action">바인딩을 소유한 입력 액션이다.</param>
        /// <param name="bindingIndex">액션 내부 바인딩 인덱스이다.</param>
        /// <param name="actionMapName">입력 액션 맵 이름이다.</param>
        public InputBindingItemViewModel(
            InputAction action,
            int bindingIndex,
            string actionMapName,
            IInputStateController inputStateController = null)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            BindingIndex = bindingIndex;
            ActionMapName = actionMapName ?? string.Empty;
            ActionName = action.name;
            _inputStateController = inputStateController;

            var binding = action.bindings[bindingIndex];
            Id = binding.id.ToString();
            BindingName = string.IsNullOrWhiteSpace(binding.name) ? binding.ToDisplayString() : binding.name;
            DefaultPath = binding.path ?? string.Empty;
            _appliedPath = string.IsNullOrWhiteSpace(binding.overridePath) ? DefaultPath : binding.overridePath;
            _pendingPath = _appliedPath;
        }

        /// <summary>
        /// 바인딩을 소유한 입력 액션을 가져온다.
        /// </summary>
        public InputAction Action { get; }

        /// <summary>
        /// 입력 바인딩 ID를 가져온다.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 입력 액션 맵 이름을 가져온다.
        /// </summary>
        public string ActionMapName { get; }

        /// <summary>
        /// 입력 액션 이름을 가져온다.
        /// </summary>
        public string ActionName { get; }

        /// <summary>
        /// 바인딩 표시 이름을 가져온다.
        /// </summary>
        public string BindingName { get; }

        /// <summary>
        /// 액션 내부 바인딩 인덱스를 가져온다.
        /// </summary>
        public int BindingIndex { get; }

        /// <summary>
        /// 기본 바인딩 경로를 가져온다.
        /// </summary>
        public string DefaultPath { get; }

        /// <summary>
        /// 적용된 바인딩 경로를 가져온다.
        /// </summary>
        public string AppliedPath => _appliedPath;

        /// <summary>
        /// UI에 표시할 현재 대기 바인딩의 사람이 읽기 쉬운 이름이다.
        /// </summary>
        public string DisplayName => ToDisplayName(PendingPath);

        /// <summary>
        /// 대화형 입력 재지정을 수행 중인지 여부를 가져온다.
        /// </summary>
        public bool IsRebinding => _isRebinding;

        /// <summary>
        /// UI에서 편집 중인 바인딩 경로를 가져오거나 설정한다.
        /// </summary>
        public string PendingPath
        {
            get => _pendingPath;
            set
            {
                if (IsDisposed)
                {
                    return;
                }

                if (SetTrackedValue(ref _pendingPath, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        /// <summary>
        /// 적용된 값과 편집 중인 값이 다른지 여부를 가져온다.
        /// </summary>
        public override bool HasChanges => !string.Equals(_appliedPath, PendingPath, StringComparison.Ordinal);

        /// <summary>
        /// 편집 중인 바인딩 경로를 입력 액션에 적용한다.
        /// </summary>
        public void Apply()
        {
            if (IsDisposed)
            {
                return;
            }

            CancelRebinding();

            var wasEnabled = Action.enabled;
            if (wasEnabled)
            {
                Action.Disable();
            }

            if (string.IsNullOrWhiteSpace(PendingPath) || string.Equals(PendingPath, DefaultPath, StringComparison.Ordinal))
            {
                Action.RemoveBindingOverride(BindingIndex);
                _appliedPath = DefaultPath;
                _pendingPath = DefaultPath;
            }
            else
            {
                Action.ApplyBindingOverride(BindingIndex, PendingPath);
                _appliedPath = PendingPath;
            }

            if (wasEnabled && (_inputStateController?.IsInputEnabled ?? true))
            {
                Action.Enable();
            }

            OnPropertiesChanged(nameof(AppliedPath), nameof(PendingPath), nameof(DisplayName), nameof(HasChanges));
        }

        /// <summary>
        /// 다음 입력을 기다렸다가 편집 중인 바인딩 경로로 설정한다.
        /// </summary>
        /// <param name="cancelPath">재지정을 취소할 입력 경로이다.</param>
        /// <returns>재지정이 시작되면 true, 요청이 거부되면 false를 반환한다.</returns>
        /// <exception cref="OperationCanceledException">재지정이 취소되면 발생한다.</exception>
        public UniTask<bool> StartInteractiveRebindingAsync(string cancelPath = "<Keyboard>/escape")
        {
            if (IsDisposed || _isRebinding || !TryAcquireAction())
            {
                return UniTask.FromResult(false);
            }

            var completionSource = new UniTaskCompletionSource();
            _rebindingCompletionSource = completionSource;
            _wasActionEnabled = Action.enabled;
            if (_wasActionEnabled)
            {
                Action.Disable();
            }

            if (IsDisposed || _rebindingCompletionSource != completionSource)
            {
                return AwaitRebindingAsync(completionSource.Task);
            }

            try
            {
                _rebindingOperation = Action.PerformInteractiveRebinding(BindingIndex)
                    .WithCancelingThrough(cancelPath)
                    .OnApplyBinding((_, path) => PendingPath = path)
                    .OnComplete(_ => CompleteRebinding())
                    .OnCancel(_ => CompleteRebinding(canceled: true));
                _rebindingOperation.Start();

                if (IsDisposed || _rebindingCompletionSource != completionSource)
                {
                    return AwaitRebindingAsync(completionSource.Task);
                }

                _isRebinding = true;
                OnPropertyChanged(nameof(IsRebinding));
            }
            catch (Exception exception)
            {
                CompleteRebinding(exception);
            }

            return AwaitRebindingAsync(completionSource.Task);
        }

        /// <summary>
        /// 진행 중인 대화형 재지정을 취소한다.
        /// </summary>
        public void CancelRebinding()
        {
            _rebindingOperation?.Cancel();
        }

        /// <summary>
        /// 편집 중인 값을 마지막 적용 값으로 되돌린다.
        /// </summary>
        public void Cancel()
        {
            if (IsDisposed)
            {
                return;
            }

            CancelRebinding();
            PendingPath = _appliedPath;
            OnHasChangesChanged();
        }

        /// <summary>
        /// 편집 중인 값을 기본 바인딩 경로로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            if (IsDisposed)
            {
                return;
            }

            CancelRebinding();
            PendingPath = DefaultPath;
            OnHasChangesChanged();
        }

        /// <summary>
        /// 입력 수집 결과를 대기 값에 반영하고 사용한 작업을 정리한다.
        /// </summary>
        private void CompleteRebinding(Exception exception = null, bool canceled = false)
        {
            var operation = _rebindingOperation;
            var completionSource = _rebindingCompletionSource;
            _rebindingOperation = null;
            _rebindingCompletionSource = null;

            try
            {
                operation?.Dispose();
            }
            finally
            {
                if (_wasActionEnabled && (_inputStateController?.IsInputEnabled ?? true))
                {
                    Action.Enable();
                }

                _wasActionEnabled = false;
                _isRebinding = false;
                if (!IsDisposed)
                {
                    OnPropertyChanged(nameof(IsRebinding));
                }

                ReleaseAction();

                if (canceled)
                {
                    completionSource?.TrySetCanceled();
                }
                else if (exception == null)
                {
                    completionSource?.TrySetResult();
                }
                else
                {
                    completionSource?.TrySetException(exception);
                }
            }
        }

        /// <summary>
        /// 재지정 완료 작업을 시작 성공 결과와 함께 반환한다.
        /// </summary>
        private static async UniTask<bool> AwaitRebindingAsync(UniTask task)
        {
            await task;
            return true;
        }

        /// <summary>
        /// 액션 단위 재지정 권한을 얻는다.
        /// </summary>
        /// <returns>권한을 얻었으면 true를 반환한다.</returns>
        private bool TryAcquireAction()
        {
            lock (RebindingActions)
            {
                return RebindingActions.Add(Action);
            }
        }

        /// <summary>
        /// 액션 단위 재지정 권한을 반환한다.
        /// </summary>
        private void ReleaseAction()
        {
            lock (RebindingActions)
            {
                RebindingActions.Remove(Action);
            }
        }

        /// <summary>
        /// 입력 경로를 UI에 표시하기 쉬운 이름으로 변환한다.
        /// </summary>
        /// <param name="path">변환할 입력 경로이다.</param>
        /// <returns>변환된 표시 이름을 반환한다.</returns>
        private static string ToDisplayName(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        /// <summary>
        /// 도메인 리로드를 끄고 플레이 모드에 진입해도 이전 세션의 정적 상태가 남지 않도록 초기화한다.
        /// 정적 상태를 보유한 프레임워크 클래스는 모두 이 규약(SubsystemRegistration 시점 리셋)을 따르므로,
        /// 새로 정적 필드를 추가하는 작성자는 이 메서드에도 해당 필드를 반드시 추가해야 한다.
        /// 이전 세션에서 재지정 중이던 액션이 잠금 목록에 남아 새 세션의 재지정을 막지 않게 한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            RebindingActions.Clear();
        }

        /// <summary>
        /// 진행 중인 입력 재지정을 취소한다.
        /// </summary>
        protected override void OnDispose()
        {
            CancelRebinding();

            // 시작 전 작업은 Cancel 콜백이 호출되지 않을 수 있어 강제로 취소 완료한다.
            if (_rebindingOperation != null || _rebindingCompletionSource != null)
            {
                CompleteRebinding(canceled: true);
            }

            base.OnDispose();
        }
    }
}
