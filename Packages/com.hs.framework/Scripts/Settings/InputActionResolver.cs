using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Framework.Settings
{
    /// <summary>
    /// InputSettingsService에 등록된 입력 액션 에셋을 우선 사용하고, 없으면 폴백 에셋으로 입력 액션을 해석한다.
    /// 폴백 에셋을 사용하는 동안에는 서비스의 입력 활성화 상태를 따라 폴백 에셋을 직접 활성화하거나
    /// 비활성화해 입력 게이팅이 일관되게 적용되도록 한다.
    /// </summary>
    public sealed class InputActionResolver : IDisposable
    {
        /// <summary>
        /// 폴백 에셋별로 현재 사용 중인 리졸버 수를 추적한다.
        /// 같은 폴백 에셋을 공유하는 컴포넌트가 있을 때 하나가 파괴되어도 에셋이 꺼지지 않게 한다.
        /// </summary>
        private static readonly Dictionary<InputActionAsset, int> FallbackUseCounts = new();

        private readonly InputActionAsset _fallbackInputActions;
        private readonly Action _onResolvedAssetChanged;
        private InputSettingsService _subscribedInputSettings;
        private bool _isUsingFallback;
        private bool _isDisposed;

        /// <summary>
        /// 폴백 에셋과 변경 콜백으로 리졸버를 생성하고 입력 설정 변경 이벤트를 구독한다.
        /// </summary>
        /// <param name="fallbackInputActions">서비스에 에셋이 등록되지 않았을 때 사용할 폴백 입력 액션 에셋이다.</param>
        /// <param name="onResolvedAssetChanged">해석 대상 에셋이 바뀌었을 때 호출할 콜백이다.</param>
        public InputActionResolver(InputActionAsset fallbackInputActions, Action onResolvedAssetChanged = null)
        {
            _fallbackInputActions = fallbackInputActions;
            _onResolvedAssetChanged = onResolvedAssetChanged;
            _subscribedInputSettings = InputSettingsService.Current;
            _subscribedInputSettings.OnInputSettingsChanged += HandleInputSettingsChanged;
            UpdateFallbackUsage();
            ApplyFallbackEnabledState();
        }

        /// <summary>
        /// 현재 해석 대상 입력 액션 에셋을 가져온다. 서비스에 등록된 에셋이 있으면 그 에셋을,
        /// 없으면 폴백 에셋을 반환한다.
        /// </summary>
        public InputActionAsset CurrentAsset
        {
            get
            {
                var settings = _subscribedInputSettings ?? InputSettingsService.Current;
                return settings.HasInputActionAsset ? settings.InputActionAsset : _fallbackInputActions;
            }
        }

        /// <summary>
        /// 현재 해석 대상 에셋에서 이름에 해당하는 입력 액션 맵을 찾는다.
        /// </summary>
        /// <param name="actionMapName">찾을 입력 액션 맵의 이름이다.</param>
        /// <returns>찾은 입력 액션 맵을 반환하며, 없으면 null을 반환한다.</returns>
        public InputActionMap FindActionMap(string actionMapName)
        {
            if (string.IsNullOrEmpty(actionMapName))
            {
                return null;
            }

            var asset = CurrentAsset;
            return asset == null ? null : asset.FindActionMap(actionMapName);
        }

        /// <summary>
        /// 현재 해석 대상 에셋의 지정한 액션 맵에서 이름에 해당하는 입력 액션을 찾는다.
        /// </summary>
        /// <param name="actionMapName">찾을 입력 액션 맵의 이름이다.</param>
        /// <param name="actionName">찾을 입력 액션의 이름이다.</param>
        /// <returns>찾은 입력 액션을 반환하며, 없으면 null을 반환한다.</returns>
        public InputAction FindAction(string actionMapName, string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
            {
                return null;
            }

            return FindActionMap(actionMapName)?.FindAction(actionName);
        }

        /// <summary>
        /// 입력 설정 변경 구독을 해제하고, 이 리졸버가 마지막 사용자였다면 폴백 에셋을 비활성화한다.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            UpdateFallbackUsage();
            ApplyFallbackEnabledState();

            if (_subscribedInputSettings != null)
            {
                _subscribedInputSettings.OnInputSettingsChanged -= HandleInputSettingsChanged;
                _subscribedInputSettings = null;
            }
        }

        /// <summary>
        /// 입력 설정이 변경되면 폴백 사용 상태와 활성화 상태를 갱신하고 변경 콜백을 호출한다.
        /// </summary>
        /// <param name="settings">변경된 입력 설정 인스턴스이다.</param>
        private void HandleInputSettingsChanged(InputSettingsService settings)
        {
            UpdateFallbackUsage();
            ApplyFallbackEnabledState();
            _onResolvedAssetChanged?.Invoke();
        }

        /// <summary>
        /// 이 리졸버가 폴백 에셋을 사용해야 하는지 판단하고 사용 카운트를 증감한다.
        /// </summary>
        private void UpdateFallbackUsage()
        {
            var settings = _subscribedInputSettings;
            var shouldUseFallback = !_isDisposed
                && settings != null
                && _fallbackInputActions != null
                && !settings.HasInputActionAsset;
            if (shouldUseFallback == _isUsingFallback)
            {
                return;
            }

            _isUsingFallback = shouldUseFallback;
            if (shouldUseFallback)
            {
                FallbackUseCounts.TryGetValue(_fallbackInputActions, out var count);
                FallbackUseCounts[_fallbackInputActions] = count + 1;
            }
            else if (FallbackUseCounts.TryGetValue(_fallbackInputActions, out var count) && count > 1)
            {
                FallbackUseCounts[_fallbackInputActions] = count - 1;
            }
            else
            {
                FallbackUseCounts.Remove(_fallbackInputActions);
            }
        }

        /// <summary>
        /// 폴백 에셋의 활성화 상태를 사용 카운트와 서비스의 입력 활성화 상태에 맞춘다.
        /// 폴백 에셋이 서비스에 그대로 등록된 경우에는 서비스가 활성화 상태를 관리하므로 건드리지 않는다.
        /// </summary>
        private void ApplyFallbackEnabledState()
        {
            if (_fallbackInputActions == null)
            {
                return;
            }

            var settings = _subscribedInputSettings ?? InputSettingsService.Current;
            if (settings.HasInputActionAsset && settings.InputActionAsset == _fallbackInputActions)
            {
                return;
            }

            if (FallbackUseCounts.ContainsKey(_fallbackInputActions) && settings.IsInputEnabled)
            {
                _fallbackInputActions.Enable();
            }
            else
            {
                _fallbackInputActions.Disable();
            }
        }

        /// <summary>
        /// 도메인 리로드 없이 플레이 모드에 진입해도 사용 카운트가 누적되지 않도록 정적 상태를 초기화한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            FallbackUseCounts.Clear();
        }
    }
}
