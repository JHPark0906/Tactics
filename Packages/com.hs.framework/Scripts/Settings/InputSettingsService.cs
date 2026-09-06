using System;
using HS.Framework.Foundation.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Framework.Settings
{
    /// <summary>
    /// 입력 액션 에셋, 입력 활성화 상태, 바인딩 오버라이드 저장 데이터를 관리한다.
    /// </summary>
    public sealed class InputSettingsService : IInputStateController
    {
        /// <summary>
        /// 저장 데이터 형식을 식별하는 버전 번호이다.
        /// </summary>
        private const int SaveVersion = 1;

        /// <summary>
        /// 입력 설정을 저장할 때 사용하는 저장소 키이다.
        /// 기본 백엔드에서는 같은 이름의 PlayerPrefs 키로 기록된다.
        /// </summary>
        private const string StorageKey = "InputSettings";

        /// <summary>
        /// UI 범위로 취급할 액션 맵 이름의 기본값이다.
        /// 프레임워크 기본 입력 에셋과 <c>UiWindowManager</c>가 같은 이름을 사용한다.
        /// </summary>
        public const string DefaultUiActionMapName = "UI";

        /// <summary>
        /// 현재 애플리케이션에서 사용 중인 입력 설정 인스턴스이다.
        /// </summary>
        private static InputSettingsService _current;

        /// <summary>
        /// 저장 대상이 되는 기본 입력 활성화 상태이다. 차단 토큰과 무관하게 유지된다.
        /// </summary>
        private bool _isBaseInputEnabled = true;

        /// <summary>
        /// 게임플레이 범위를 막고 있는 차단 토큰의 개수이다.
        /// </summary>
        private int _gameplayBlockCount;

        /// <summary>
        /// UI 범위를 막고 있는 차단 토큰의 개수이다.
        /// </summary>
        private int _uiBlockCount;

        /// <summary>
        /// UI 범위로 취급할 액션 맵의 이름이다.
        /// </summary>
        private string _uiActionMapName = DefaultUiActionMapName;

        /// <summary>
        /// 입력 설정이 저장되거나 변경된 뒤 발생한다.
        /// </summary>
        public event Action<InputSettingsService> OnInputSettingsChanged;

        /// <summary>
        /// 현재 입력 설정을 가져오며, 아직 초기화되지 않았으면 저장된 설정을 불러온다.
        /// </summary>
        /// <remarks>
        /// 이 정적 접근자는 주입 경로가 아직 없는 지점(FrameworkInitializer의 설정 초기화, 에디터 도구, EditMode 테스트)에서만 쓴다.
        /// 씬에 배치되는 런타임 컴포넌트는 VContainer로 주입받거나 직접 초기화할 수 있다.
        /// </remarks>
        public static InputSettingsService Current
        {
            get
            {
                if (_current != null)
                {
                    return _current;
                }

                return Load();
            }
        }

        /// <summary>
        /// 현재 등록된 Unity 입력 액션 에셋을 가져온다.
        /// </summary>
        public InputActionAsset InputActionAsset { get; private set; }

        /// <summary>
        /// 현재 게임플레이 입력이 실제로 활성화되어 있는지 여부를 가져온다.
        /// 기본 상태가 활성이더라도 게임플레이를 막는 차단 토큰이 있으면 false를 반환한다.
        /// </summary>
        public bool IsInputEnabled => IsScopeEnabled(InputBlockScope.Gameplay);

        /// <summary>
        /// UI 범위로 취급할 액션 맵의 이름을 가져온다. 이 맵을 뺀 나머지는 모두 게임플레이 범위이다.
        /// </summary>
        public string UiActionMapName => _uiActionMapName;

        /// <summary>
        /// 저장된 바인딩 오버라이드 JSON 문자열을 가져온다.
        /// </summary>
        public string BindingOverridesJson { get; private set; } = string.Empty;

        /// <summary>
        /// 입력 액션 에셋이 등록되어 있는지 여부를 가져온다.
        /// </summary>
        public bool HasInputActionAsset => InputActionAsset != null;

        /// <summary>
        /// 입력 설정을 초기화하고 입력 액션 에셋과 저장된 바인딩 오버라이드를 적용한다.
        /// </summary>
        /// <param name="inputActionAsset">등록할 Unity 입력 액션 에셋이다.</param>
        /// <param name="enableOnInitialize">초기화 직후 입력을 활성화할지 여부이다.</param>
        /// <param name="loadBindingOverrides">저장된 바인딩 오버라이드를 적용할지 여부이다.</param>
        /// <returns>초기화된 입력 설정 인스턴스를 반환한다.</returns>
        public static InputSettingsService Initialize(
            InputActionAsset inputActionAsset,
            bool enableOnInitialize = true,
            bool loadBindingOverrides = true)
        {
            var settings = Current;
            settings.SetInputActionAsset(inputActionAsset, enableOnInitialize, loadBindingOverrides);
            return settings;
        }

        /// <summary>
        /// 저장된 입력 설정을 불러오고, 없거나 유효하지 않으면 기본 설정을 사용한다.
        /// 캐시된 인스턴스를 새 인스턴스로 교체하지 않고 제자리에서 갱신하므로,
        /// 이전에 <see cref="Current"/>를 캡처한 소비자와 <see cref="OnInputSettingsChanged"/> 구독자,
        /// 그리고 살아 있는 입력 차단 토큰이 모두 유효한 상태로 남는다.
        /// 등록된 입력 액션 에셋과 차단 토큰 수는 저장 대상이 아니므로 이 호출로 초기화되지 않는다.
        /// </summary>
        /// <returns>불러온 입력 설정 인스턴스를 반환하며, <see cref="Current"/>와 항상 같은 인스턴스이다.</returns>
        public static InputSettingsService Load()
        {
            var settings = EnsureCurrent();
            settings.LoadInto();
            return settings;
        }

        /// <summary>
        /// 캐시된 입력 설정 인스턴스를 가져오며, 없으면 만들어 캐시한다.
        /// 모든 정적 진입점이 이 인스턴스를 재사용해야 초기화가 인스턴스를 교체하지 않는다.
        /// </summary>
        /// <returns>캐시된 입력 설정 인스턴스를 반환한다.</returns>
        private static InputSettingsService EnsureCurrent()
        {
            return _current ??= new InputSettingsService();
        }

        /// <summary>
        /// 도메인 리로드를 끄고 플레이 모드에 진입해도 이전 세션의 정적 상태가 남지 않도록 초기화한다.
        /// 정적 상태를 보유한 프레임워크 클래스는 모두 이 규약(SubsystemRegistration 시점 리셋)을 따르므로,
        /// 새로 정적 필드를 추가하는 작성자는 이 메서드에도 해당 필드를 반드시 추가해야 한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _current = null;
        }

        /// <summary>
        /// 테스트에서 정적 캐시 상태가 누출되지 않도록 캐시를 초기 상태로 되돌린다.
        /// 플레이 모드 진입 시 수행하는 리셋과 같은 동작이다.
        /// </summary>
        internal static void ResetCurrentForTests()
        {
            ResetStaticState();
        }

        /// <summary>
        /// 저장 대상 값을 기본값으로 되돌린 뒤, 저장된 설정이 있으면 덮어써 현재 인스턴스 상태를 갱신한다.
        /// </summary>
        private void LoadInto()
        {
            BindingOverridesJson = string.Empty;
            _isBaseInputEnabled = true;

            if (!SettingsStorage.Current.TryRead(StorageKey, out var json))
            {
                return;
            }

            try
            {
                Import(JsonUtility.FromJson<SettingsData>(json));
            }
            catch (ArgumentException)
            {
                Save();
            }
        }

        /// <summary>
        /// 현재 입력 설정을 설정 저장소에 저장한다.
        /// </summary>
        public void Save()
        {
            SettingsStorage.Current.Write(StorageKey, JsonUtility.ToJson(Export()));
        }

        /// <summary>
        /// 입력 액션 에셋을 등록하고 필요에 따라 바인딩 오버라이드와 활성화 상태를 적용한다.
        /// </summary>
        /// <param name="inputActionAsset">등록할 Unity 입력 액션 에셋이다.</param>
        /// <param name="enable">등록 직후 입력을 활성화할지 여부이다.</param>
        /// <param name="loadBindingOverrides">저장된 바인딩 오버라이드를 적용할지 여부이다.</param>
        public void SetInputActionAsset(
            InputActionAsset inputActionAsset,
            bool enable = true,
            bool loadBindingOverrides = true)
        {
            if (InputActionAsset != null && InputActionAsset != inputActionAsset)
            {
                InputActionAsset.Disable();
            }

            InputActionAsset = inputActionAsset;

            if (loadBindingOverrides)
            {
                ApplyBindingOverrides();
            }

            _isBaseInputEnabled = enable;
            ApplyInputEnabled();
            OnInputSettingsChanged?.Invoke(this);
        }

        /// <summary>
        /// 기본 입력 활성화 상태를 변경하고 저장하지 않는다.
        /// </summary>
        /// <param name="isEnabled">입력을 활성화할지 여부다.</param>
        public void SetInputEnabled(bool isEnabled)
        {
            SetInputEnabled(isEnabled, false);
        }

        /// <summary>
        /// 기본 입력 활성화 여부를 설정하고 필요에 따라 저장한다.
        /// 차단 토큰이 살아 있는 동안에는 실제 활성화가 지연되며, 모든 토큰이 해제될 때 반영된다.
        /// </summary>
        /// <param name="isEnabled">입력을 활성화할지 여부이다.</param>
        /// <param name="save">변경한 설정을 저장할지 여부이다.</param>
        public void SetInputEnabled(bool isEnabled, bool save = false)
        {
            _isBaseInputEnabled = isEnabled;
            ApplyInputEnabled();
            Commit(save);
        }

        /// <inheritdoc />
        public bool IsScopeEnabled(InputBlockScope scope)
        {
            if (!_isBaseInputEnabled)
            {
                return false;
            }

            if (scope.HasFlag(InputBlockScope.Gameplay) && _gameplayBlockCount > 0)
            {
                return false;
            }

            return !scope.HasFlag(InputBlockScope.Ui) || _uiBlockCount <= 0;
        }

        /// <summary>
        /// UI 범위로 취급할 액션 맵의 이름을 지정한다.
        /// 프로젝트가 UI 액션 맵을 다른 이름으로 쓴다면 입력 에셋을 등록하기 전에 바꿔 준다.
        /// </summary>
        /// <param name="actionMapName">UI 범위로 볼 액션 맵의 이름이며, 비어 있으면 기본값을 사용한다.</param>
        public void SetUiActionMapName(string actionMapName)
        {
            _uiActionMapName = string.IsNullOrWhiteSpace(actionMapName)
                ? DefaultUiActionMapName
                : actionMapName;
            ApplyInputEnabled();
        }

        /// <summary>
        /// 게임플레이 입력을 막는 차단 토큰을 획득한다. UI 입력은 살아 있다.
        /// </summary>
        /// <returns>해제 시 차단을 되돌리는 토큰을 반환한다.</returns>
        public IDisposable AcquireInputBlock()
        {
            return AcquireInputBlock(InputBlockScope.Gameplay);
        }

        /// <summary>
        /// 지정한 범위의 입력을 막는 차단 토큰을 획득한다.
        /// 범위별로 마지막 토큰이 해제될 때까지 그 범위의 입력은 비활성화 상태로 유지된다.
        /// 토큰 획득과 해제는 설정 변경이 아니므로 <see cref="OnInputSettingsChanged"/>를 발생시키지 않는다.
        /// </summary>
        /// <param name="scope">막을 입력 범위이다.</param>
        /// <returns>해제 시 차단을 되돌리는 토큰을 반환한다.</returns>
        public IDisposable AcquireInputBlock(InputBlockScope scope)
        {
            if (scope.HasFlag(InputBlockScope.Gameplay))
            {
                _gameplayBlockCount++;
            }

            if (scope.HasFlag(InputBlockScope.Ui))
            {
                _uiBlockCount++;
            }

            if (scope != InputBlockScope.None)
            {
                ApplyInputEnabled();
            }

            return new InputBlockToken(this, scope);
        }

        /// <summary>
        /// 차단 토큰 하나를 해제하고 남은 차단 상태를 다시 적용한다.
        /// </summary>
        /// <param name="scope">해제할 토큰이 막고 있던 범위이다.</param>
        private void ReleaseInputBlock(InputBlockScope scope)
        {
            if (scope.HasFlag(InputBlockScope.Gameplay) && _gameplayBlockCount > 0)
            {
                _gameplayBlockCount--;
            }

            if (scope.HasFlag(InputBlockScope.Ui) && _uiBlockCount > 0)
            {
                _uiBlockCount--;
            }

            if (scope != InputBlockScope.None)
            {
                ApplyInputEnabled();
            }
        }

        /// <summary>
        /// 등록된 입력 액션 에셋에서 액션 이름 또는 ID에 해당하는 입력 액션을 찾는다.
        /// </summary>
        /// <param name="actionNameOrId">찾을 입력 액션의 이름 또는 ID이다.</param>
        /// <param name="throwIfNotFound">액션을 찾지 못했을 때 예외를 발생시킬지 여부이다.</param>
        /// <returns>찾은 입력 액션을 반환하며, 없으면 null을 반환한다.</returns>
        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
        {
            if (InputActionAsset != null)
            {
                return InputActionAsset.FindAction(actionNameOrId, throwIfNotFound);
            }

            if (throwIfNotFound)
            {
                throw new InvalidOperationException("InputActionAsset is not registered.");
            }

            return null;
        }

        /// <summary>
        /// 등록된 입력 액션 에셋에서 액션 맵 이름 또는 ID에 해당하는 입력 액션 맵을 찾는다.
        /// </summary>
        /// <param name="actionMapNameOrId">찾을 입력 액션 맵의 이름 또는 ID이다.</param>
        /// <param name="throwIfNotFound">액션 맵을 찾지 못했을 때 예외를 발생시킬지 여부이다.</param>
        /// <returns>찾은 입력 액션 맵을 반환하며, 없으면 null을 반환한다.</returns>
        public InputActionMap FindActionMap(string actionMapNameOrId, bool throwIfNotFound = false)
        {
            if (InputActionAsset != null)
            {
                return InputActionAsset.FindActionMap(actionMapNameOrId, throwIfNotFound);
            }

            if (throwIfNotFound)
            {
                throw new InvalidOperationException("InputActionAsset is not registered.");
            }

            return null;
        }

        /// <summary>
        /// 현재 입력 액션 에셋의 바인딩 오버라이드를 JSON 문자열로 저장하고 필요에 따라 설정을 저장한다.
        /// </summary>
        /// <param name="save">변경한 설정을 설정 저장소에 저장할지 여부이다.</param>
        public void SaveBindingOverrides(bool save = true)
        {
            BindingOverridesJson = InputActionAsset != null
                ? InputActionAsset.SaveBindingOverridesAsJson()
                : string.Empty;

            Commit(save);
        }

        /// <summary>
        /// 저장된 바인딩 오버라이드를 현재 입력 액션 에셋에 적용하고 필요에 따라 설정을 저장한다.
        /// </summary>
        /// <param name="save">변경한 설정을 설정 저장소에 저장할지 여부이다.</param>
        public void LoadBindingOverrides(bool save = false)
        {
            ApplyBindingOverrides();
            Commit(save);
        }

        /// <summary>
        /// 현재 입력 액션 에셋의 모든 바인딩 오버라이드를 제거하고 저장된 JSON 값을 비운다.
        /// </summary>
        /// <param name="save">변경한 설정을 설정 저장소에 저장할지 여부이다.</param>
        public void ClearBindingOverrides(bool save = true)
        {
            InputActionAsset?.RemoveAllBindingOverrides();
            BindingOverridesJson = string.Empty;
            Commit(save);
        }

        /// <summary>
        /// 범위별 실효 상태에 따라 액션 맵을 하나씩 켜거나 끈다.
        /// </summary>
        /// <remarks>
        /// 에셋 전체를 한 번에 끄면 게임플레이를 막으려는 주체가 UI까지 끄게 되므로 맵 단위로 적용한다.
        /// <see cref="UiActionMapName"/>과 이름이 같은 맵만 UI 범위로 보고 나머지는 모두 게임플레이 범위로 본다.
        /// 이름을 기준으로 삼는 이유는 프로젝트마다 맵 구성이 다르고, 액션 맵 자체에는 용도를 밝히는 정보가 없기 때문이다.
        /// </remarks>
        private void ApplyInputEnabled()
        {
            if (InputActionAsset == null)
            {
                return;
            }

            var isGameplayEnabled = IsScopeEnabled(InputBlockScope.Gameplay);
            var isUiEnabled = IsScopeEnabled(InputBlockScope.Ui);
            foreach (var actionMap in InputActionAsset.actionMaps)
            {
                var isEnabled = IsUiActionMap(actionMap) ? isUiEnabled : isGameplayEnabled;
                if (isEnabled)
                {
                    actionMap.Enable();
                }
                else
                {
                    actionMap.Disable();
                }
            }
        }

        /// <summary>지정한 액션 맵이 UI 범위에 속하는지 확인한다.</summary>
        /// <param name="actionMap">확인할 액션 맵이다.</param>
        /// <returns>UI 범위이면 true이다.</returns>
        private bool IsUiActionMap(InputActionMap actionMap)
        {
            return string.Equals(actionMap.name, _uiActionMapName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 저장된 바인딩 오버라이드 JSON을 등록된 입력 액션 에셋에 적용한다.
        /// </summary>
        private void ApplyBindingOverrides()
        {
            if (InputActionAsset == null || string.IsNullOrEmpty(BindingOverridesJson))
            {
                return;
            }

            InputActionAsset.LoadBindingOverridesFromJson(BindingOverridesJson);
        }

        /// <summary>
        /// 변경된 설정을 필요에 따라 저장하고 변경 알림을 발생시킨다.
        /// </summary>
        /// <param name="save">설정을 저장할지 여부이다.</param>
        private void Commit(bool save)
        {
            if (save)
            {
                Save();
            }

            OnInputSettingsChanged?.Invoke(this);
        }

        /// <summary>
        /// 저장 데이터 값을 현재 인스턴스로 가져온다.
        /// </summary>
        /// <param name="data">가져올 입력 설정 저장 데이터이다.</param>
        private void Import(SettingsData data)
        {
            if (data == null || data.saveVersion != SaveVersion)
            {
                return;
            }

            BindingOverridesJson = data.bindingOverridesJson ?? string.Empty;
            _isBaseInputEnabled = data.isInputEnabled;
        }

        /// <summary>
        /// 현재 입력 설정을 저장 가능한 데이터 구조로 내보낸다.
        /// </summary>
        /// <returns>현재 입력 설정 값을 담은 저장 데이터를 반환한다.</returns>
        private SettingsData Export()
        {
            return new SettingsData
            {
                saveVersion = SaveVersion,
                bindingOverridesJson = BindingOverridesJson,
                isInputEnabled = _isBaseInputEnabled
            };
        }

        /// <summary>
        /// 해제 시 소유 서비스에서 자신이 막고 있던 범위의 차단 개수를 줄이는 입력 차단 토큰이다.
        /// 중복 해제는 무시된다.
        /// </summary>
        private sealed class InputBlockToken : IDisposable
        {
            private readonly InputBlockScope _scope;
            private InputSettingsService _owner;

            /// <summary>지정한 서비스가 소유하며 지정한 범위를 막는 차단 토큰을 생성한다.</summary>
            public InputBlockToken(InputSettingsService owner, InputBlockScope scope)
            {
                _owner = owner;
                _scope = scope;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                var owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                owner.ReleaseInputBlock(_scope);
            }
        }

        /// <summary>
        /// 설정 저장소에 직렬화해 저장하는 입력 설정 데이터이다.
        /// </summary>
        [Serializable]
        private sealed class SettingsData
        {
            /// <summary>
            /// 저장 데이터 형식의 버전 번호이다.
            /// </summary>
            public int saveVersion;

            /// <summary>
            /// 저장된 바인딩 오버라이드 JSON 문자열이다.
            /// </summary>
            public string bindingOverridesJson;

            /// <summary>
            /// 저장된 입력 활성화 여부이다.
            /// </summary>
            public bool isInputEnabled = true;
        }
    }
}
