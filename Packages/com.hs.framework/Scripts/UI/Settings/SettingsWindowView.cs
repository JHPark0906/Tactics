using System.Collections.Generic;
using System.ComponentModel;
using HS.Framework.Foundation.MVVM;
using HS.Framework.ProjectManagement;
using HS.Framework.Settings;
using HS.Framework.UI.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 설정창 ViewModel을 UGUI 요소와 연결하는 런타임 View이다.
    /// 창 여닫기는 같은 GameObject에 조합한 <see cref="UiWindowBase"/>가 담당하며,
    /// 이 View는 ViewBase 상속을 유지한 채 창 수명주기에 편승한다.
    /// 따라서 UiWindowManager가 이 설정창을 다른 창과 동일하게 열고 닫을 수 있다.
    /// </summary>
    [RequireComponent(typeof(UiWindowBase))]
    public sealed class SettingsWindowView : ViewBase<SettingsWindowViewModel>
    {
        /// <summary>
        /// 이 설정창의 열기/닫기를 담당하는 창 컴포넌트이다. 비워 두면 같은 GameObject에서 찾는다.
        /// </summary>
        [Header("Window")]
        [SerializeField] private UiWindowBase window;

        private IProjectConfiguration _projectConfiguration;

        /// <summary>
        /// View가 스스로 생성해 수명을 소유한 ViewModel이다. 외부에서 주입된 ViewModel은 여기에 담지 않는다.
        /// </summary>
        private SettingsWindowViewModel _ownedViewModel;

        /// <summary>
        /// 해상도 선택 드롭다운이다.
        /// </summary>
        [Header("Graphics")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        /// <summary>
        /// 주사율 선택 드롭다운이다.
        /// </summary>
        [SerializeField] private TMP_Dropdown refreshRateDropdown;

        /// <summary>
        /// 전체화면 모드 선택 드롭다운이다.
        /// </summary>
        [SerializeField] private TMP_Dropdown fullScreenModeDropdown;

        /// <summary>
        /// 목표 프레임레이트 선택 드롭다운이다.
        /// </summary>
        [SerializeField] private TMP_Dropdown targetFrameRateDropdown;

        /// <summary>
        /// VSync 활성화 여부 토글이다.
        /// </summary>
        [SerializeField] private Toggle vSyncToggle;

        /// <summary>
        /// 품질 레벨 선택 드롭다운이다.
        /// </summary>
        [SerializeField] private TMP_Dropdown qualityLevelDropdown;

        /// <summary>
        /// 로케일 선택 드롭다운이다.
        /// </summary>
        [Header("Locale")]
        [SerializeField] private TMP_Dropdown localeDropdown;

        /// <summary>
        /// 입력 바인딩 항목을 배치할 부모 Transform이다.
        /// </summary>
        [Header("Input")]
        [SerializeField] private Transform inputBindingsRoot;

        /// <summary>
        /// 오디오 채널 항목을 배치할 부모 Transform이다.
        /// </summary>
        [Header("Audio Views")]
        [SerializeField] private Transform audioChannelsRoot;

        /// <summary>
        /// 변경 값을 적용하는 버튼이다.
        /// </summary>
        [Header("Actions")]
        [SerializeField] private Button applyButton;

        /// <summary>
        /// 변경 값을 취소하는 버튼이다.
        /// </summary>
        [SerializeField] private Button cancelButton;

        /// <summary>
        /// 편집 중인 값을 기본값으로 되돌리는 버튼이다.
        /// </summary>
        [SerializeField] private Button resetButton;

        /// <summary>
        /// 인스펙터에서 연결한 입력 바인딩 항목 View 목록이다.
        /// </summary>
        [SerializeField] private SettingsInputBindingItemView[] inputBindingItemViews;

        /// <summary>
        /// 인스펙터에서 연결한 오디오 채널 항목 View 목록이다.
        /// </summary>
        [SerializeField] private SettingsAudioChannelItemView[] audioChannelItemViews;

        public Transform InputBindingsRoot => inputBindingsRoot;
        public Transform AudioChannelsRoot => audioChannelsRoot;

        /// <summary>
        /// 이 설정창의 열기/닫기를 담당하는 창 컴포넌트를 가져온다.
        /// UiWindowManager에 등록하거나 창을 직접 여닫을 때 사용한다.
        /// </summary>
        public UiWindowBase Window => window;

        /// <summary>
        /// 어떤 설정 범주를 사용자에게 열지 정하는 프로젝트 구성을 주입받는다.
        /// ViewModel이 이미 연결되어 있으면 범주 표시를 곧바로 다시 맞춘다.
        /// </summary>
        /// <param name="projectConfiguration">프로젝트 구성이며 null이면 모든 범주를 연다.</param>
        [Inject]
        public void InjectProjectConfiguration(IProjectConfiguration projectConfiguration)
        {
            _projectConfiguration = projectConfiguration;
            if (ViewModel != null)
            {
                ApplyConfigurationVisibility();
            }
        }

        /// <summary>
        /// 조합된 창 컴포넌트를 찾아 닫힘 알림을 구독하고, 기본 ViewModel을 생성해 연결한다.
        /// </summary>
        private void Awake()
        {
            // 직렬화된 참조는 비어 있어도 C# null 이 아닐 수 있으므로 Unity 의 null 비교로 확인한다.
            if (window == null)
            {
                window = GetComponent<UiWindowBase>();
            }

            if (window != null)
            {
                window.Closed += OnWindowClosed;
            }

            if (ViewModel == null)
            {
                _ownedViewModel = SettingsWindowViewModel.CreateDefault(
                    DisplaySettingsService.Current,
                    InputSettingsService.Current,
                    AudioSettingsService.Current,
                    LocaleSettingsService.Current);
                Initialize(_ownedViewModel);
            }
        }

        protected override void OnViewModelBound()
        {
            BindStaticControls();
            BindGeneratedItems();
            ApplyConfigurationVisibility();
        }

        /// <summary>
        /// ViewModel 연결 해제 시 이벤트 구독을 정리하고, 소유한 ViewModel이 교체로 방치되지 않도록 함께 해제한다.
        /// </summary>
        protected override void OnViewModelUnbinding()
        {
            UnbindStaticControls();
            UnbindGeneratedItems();
            DisposeOwnedViewModel();
        }

        /// <summary>
        /// 컴포넌트가 제거될 때 창 구독을 해제하고 소유한 ViewModel을 함께 해제한다.
        /// </summary>
        protected override void OnDestroy()
        {
            if (window != null)
            {
                window.Closed -= OnWindowClosed;
            }

            base.OnDestroy();
            DisposeOwnedViewModel();
        }

        /// <summary>
        /// 창이 닫히면 적용하지 않은 편집 값을 되돌린다.
        /// 다음에 창을 열었을 때 이전에 버린 변경 값이 남아 있지 않게 하기 위함이다.
        /// </summary>
        /// <param name="closedWindow">닫힌 창 컴포넌트이다.</param>
        private void OnWindowClosed(UiWindowBase closedWindow)
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.Cancel();
            Refresh();
        }

        /// <summary>
        /// 스스로 생성해 소유한 ViewModel을 해제한다. 외부에서 주입된 ViewModel은 해제하지 않는다.
        /// </summary>
        private void DisposeOwnedViewModel()
        {
            if (_ownedViewModel == null)
            {
                return;
            }

            _ownedViewModel.Dispose();
            _ownedViewModel = null;
        }

        /// <summary>
        /// 정적 UI 컨트롤 이벤트와 옵션을 연결한다.
        /// </summary>
        private void BindStaticControls()
        {
            UnbindStaticControls();

            PopulateDropdown(resolutionDropdown, ViewModel?.Graphics?.Resolutions);
            PopulateDropdown(refreshRateDropdown, ViewModel?.Graphics?.RefreshRates);
            PopulateDropdown(fullScreenModeDropdown, ViewModel?.Graphics?.FullScreenModes);
            PopulateDropdown(targetFrameRateDropdown, ViewModel?.Graphics?.TargetFrameRates);
            PopulateDropdown(qualityLevelDropdown, ViewModel?.Graphics?.QualityLevels);
            PopulateDropdown(localeDropdown, ViewModel?.Locale?.Locales);

            if (resolutionDropdown != null)
            {
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }

            if (refreshRateDropdown != null)
            {
                refreshRateDropdown.onValueChanged.AddListener(OnRefreshRateChanged);
            }

            if (fullScreenModeDropdown != null)
            {
                fullScreenModeDropdown.onValueChanged.AddListener(OnFullScreenModeChanged);
            }

            if (targetFrameRateDropdown != null)
            {
                targetFrameRateDropdown.onValueChanged.AddListener(OnTargetFrameRateChanged);
            }

            if (vSyncToggle != null)
            {
                vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
            }

            if (qualityLevelDropdown != null)
            {
                qualityLevelDropdown.onValueChanged.AddListener(OnQualityLevelChanged);
            }

            if (localeDropdown != null)
            {
                localeDropdown.onValueChanged.AddListener(OnLocaleChanged);
            }

            if (applyButton != null)
            {
                applyButton.onClick.AddListener(OnApplyClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnResetClicked);
            }
        }

        /// <summary>
        /// 정적 UI 컨트롤 이벤트 구독을 해제한다.
        /// </summary>
        private void UnbindStaticControls()
        {
            if (resolutionDropdown != null)
            {
                resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
            }

            if (refreshRateDropdown != null)
            {
                refreshRateDropdown.onValueChanged.RemoveListener(OnRefreshRateChanged);
            }

            if (fullScreenModeDropdown != null)
            {
                fullScreenModeDropdown.onValueChanged.RemoveListener(OnFullScreenModeChanged);
            }

            if (targetFrameRateDropdown != null)
            {
                targetFrameRateDropdown.onValueChanged.RemoveListener(OnTargetFrameRateChanged);
            }

            if (vSyncToggle != null)
            {
                vSyncToggle.onValueChanged.RemoveListener(OnVSyncChanged);
            }

            if (qualityLevelDropdown != null)
            {
                qualityLevelDropdown.onValueChanged.RemoveListener(OnQualityLevelChanged);
            }

            if (localeDropdown != null)
            {
                localeDropdown.onValueChanged.RemoveListener(OnLocaleChanged);
            }

            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(OnApplyClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelClicked);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(OnResetClicked);
            }
        }

        /// <summary>
        /// 인스펙터에서 연결한 항목에 ViewModel을 연결한다.
        /// </summary>
        private void BindGeneratedItems()
        {
            if (ViewModel == null)
            {
                return;
            }

            var inputBindings = ViewModel.Input?.Bindings;
            if (inputBindings != null)
            {
                var count = Mathf.Min(inputBindings.Count, inputBindingItemViews?.Length ?? 0);
                for (var index = 0; index < count; index++)
                {
                    inputBindingItemViews[index]?.Initialize(inputBindings[index]);
                }
            }

            var audioChannels = ViewModel.AudioChannels;
            var audioCount = Mathf.Min(audioChannels.Count, audioChannelItemViews?.Length ?? 0);
            for (var index = 0; index < audioCount; index++)
            {
                audioChannelItemViews[index]?.Initialize(audioChannels[index]);
            }
        }

        /// <summary>
        /// 항목의 ViewModel 연결을 해제한다.
        /// </summary>
        private void UnbindGeneratedItems()
        {
            if (inputBindingItemViews != null)
            {
                foreach (var view in inputBindingItemViews)
                {
                    view?.Initialize(null);
                }
            }

            if (audioChannelItemViews != null)
            {
                foreach (var view in audioChannelItemViews)
                {
                    view?.Initialize(null);
                }
            }
        }

        /// <summary>
        /// 구성 에셋에서 허용하지 않은 설정 범주는 UI에서 숨긴다.
        /// </summary>
        private void ApplyConfigurationVisibility()
        {
            var isGraphicsVisible = _projectConfiguration == null ||
                                    _projectConfiguration.IsUserConfigurable(ProjectUserSetting.Graphics);
            SetControlVisible(resolutionDropdown, isGraphicsVisible);
            SetControlVisible(refreshRateDropdown, isGraphicsVisible);
            SetControlVisible(fullScreenModeDropdown, isGraphicsVisible);
            SetControlVisible(targetFrameRateDropdown, isGraphicsVisible);
            SetControlVisible(vSyncToggle, isGraphicsVisible);
            SetControlVisible(qualityLevelDropdown, isGraphicsVisible);

            // 로케일 드롭다운은 프로젝트 설정 허용 여부와 실제 적용 가능한 로케일 옵션 존재 여부를 모두 만족할 때만 보인다.
            // 옵션 목록은 LocaleSettingsService.GetAvailableLocales가 서비스의 IsUserConfigurable 판정에 따라 채우므로,
            // "보이면 적용도 동작한다" 불변식이 성립한다.
            var isLocaleVisible = (_projectConfiguration == null ||
                                   _projectConfiguration.IsUserConfigurable(ProjectUserSetting.Locale)) &&
                                  ViewModel?.Locale != null && ViewModel.Locale.Locales.Count > 0;
            SetControlVisible(localeDropdown, isLocaleVisible);

            var isInputVisible = _projectConfiguration == null ||
                                 _projectConfiguration.IsUserConfigurable(ProjectUserSetting.Input);
            if (inputBindingsRoot != null)
            {
                inputBindingsRoot.gameObject.SetActive(isInputVisible);
            }

            var isAudioVisible = _projectConfiguration == null ||
                                 _projectConfiguration.IsUserConfigurable(ProjectUserSetting.Audio);
            if (audioChannelsRoot != null)
            {
                audioChannelsRoot.gameObject.SetActive(isAudioVisible);
            }
        }

        private static void SetControlVisible(Selectable control, bool isVisible)
        {
            if (control != null)
            {
                control.transform.parent.gameObject.SetActive(isVisible);
            }
        }

        /// <summary>
        /// UI 표시 값을 ViewModel 상태와 맞춘다.
        /// </summary>
        public override void Refresh()
        {
            if (ViewModel == null)
            {
                return;
            }

            var graphics = ViewModel.Graphics;
            if (graphics != null)
            {
                SetDropdownValueWithoutNotify(resolutionDropdown, graphics.SelectedResolutionIndex);
                SetDropdownValueWithoutNotify(refreshRateDropdown, graphics.SelectedRefreshRateIndex);
                SetDropdownValueWithoutNotify(fullScreenModeDropdown, graphics.SelectedFullScreenModeIndex);
                SetDropdownValueWithoutNotify(targetFrameRateDropdown, graphics.SelectedTargetFrameRateIndex);
                SetDropdownValueWithoutNotify(qualityLevelDropdown, graphics.SelectedQualityLevelIndex);

                if (vSyncToggle != null)
                {
                    vSyncToggle.SetIsOnWithoutNotify(graphics.IsVSyncEnabled);
                }
            }

            var locale = ViewModel.Locale;
            if (locale != null)
            {
                SetDropdownValueWithoutNotify(localeDropdown, locale.SelectedLocaleIndex);
            }

            if (applyButton != null)
            {
                applyButton.interactable = ViewModel.HasChanges;
            }
        }

        /// <summary>
        /// 드롭다운 옵션을 구성한다.
        /// </summary>
        /// <param name="dropdown">옵션을 넣을 드롭다운이다.</param>
        /// <param name="options">표시할 설정 옵션 목록이다.</param>
        /// <typeparam name="T">옵션 값의 형식이다.</typeparam>
        private static void PopulateDropdown<T>(TMP_Dropdown dropdown, IReadOnlyList<SettingsOptionViewModel<T>> options)
        {
            if (dropdown == null)
            {
                return;
            }

            dropdown.ClearOptions();
            if (options == null)
            {
                return;
            }

            var dropdownOptions = new List<TMP_Dropdown.OptionData>(options.Count);
            foreach (var option in options)
            {
                dropdownOptions.Add(new TMP_Dropdown.OptionData(option.DisplayName));
            }

            dropdown.AddOptions(dropdownOptions);
        }

        /// <summary>
        /// 드롭다운 값을 이벤트 없이 변경한다.
        /// </summary>
        /// <param name="dropdown">값을 바꿀 드롭다운이다.</param>
        /// <param name="value">새 인덱스 값이다.</param>
        private static void SetDropdownValueWithoutNotify(TMP_Dropdown dropdown, int value)
        {
            if (dropdown != null)
            {
                dropdown.SetValueWithoutNotify(value);
            }
        }

        /// <summary>
        /// ViewModel 속성 변경 시 UI 표시를 갱신한다.
        /// </summary>
        /// <param name="sender">변경된 ViewModel이다.</param>
        /// <param name="e">속성 변경 이벤트 인자이다.</param>
        protected override void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Refresh();
        }

        /// <summary>
        /// 해상도 드롭다운 변경 값을 ViewModel에 전달한다.
        /// </summary>
        /// <param name="index">선택된 옵션 인덱스이다.</param>
        private void OnResolutionChanged(int index)
        {
            if (ViewModel?.Graphics != null)
            {
                ViewModel.Graphics.SelectedResolutionIndex = index;
            }
        }

        /// <summary>
        /// 주사율 드롭다운 변경 값을 ViewModel에 전달한다.
        /// </summary>
        /// <param name="index">선택된 옵션 인덱스이다.</param>
        private void OnRefreshRateChanged(int index)
        {
            if (ViewModel?.Graphics != null)
            {
                ViewModel.Graphics.SelectedRefreshRateIndex = index;
            }
        }

        /// <summary>
        /// 전체화면 모드 드롭다운 변경 값을 ViewModel에 전달한다.
        /// </summary>
        /// <param name="index">선택된 옵션 인덱스이다.</param>
        private void OnFullScreenModeChanged(int index)
        {
            if (ViewModel?.Graphics != null)
            {
                ViewModel.Graphics.SelectedFullScreenModeIndex = index;
            }
        }

        /// <summary>
        /// 목표 프레임레이트 드롭다운 변경 값을 ViewModel에 전달한다.
        /// </summary>
        /// <param name="index">선택된 옵션 인덱스이다.</param>
        private void OnTargetFrameRateChanged(int index)
        {
            if (ViewModel?.Graphics != null)
            {
                ViewModel.Graphics.SelectedTargetFrameRateIndex = index;
            }
        }

        /// <summary>
        /// VSync 토글 변경 값을 ViewModel에 전달한다.
        /// </summary>
        /// <param name="isEnabled">새 VSync 활성화 여부이다.</param>
        private void OnVSyncChanged(bool isEnabled)
        {
            if (ViewModel?.Graphics != null)
            {
                ViewModel.Graphics.IsVSyncEnabled = isEnabled;
            }
        }

        /// <summary>
        /// 품질 레벨 드롭다운 변경 값을 ViewModel에 전달한다.
        /// </summary>
        /// <param name="index">선택된 옵션 인덱스이다.</param>
        private void OnQualityLevelChanged(int index)
        {
            if (ViewModel?.Graphics != null)
            {
                ViewModel.Graphics.SelectedQualityLevelIndex = index;
            }
        }

        /// <summary>
        /// 로케일 드롭다운 변경 값을 ViewModel에 전달한다.
        /// </summary>
        /// <param name="index">선택된 옵션 인덱스이다.</param>
        private void OnLocaleChanged(int index)
        {
            if (ViewModel?.Locale != null)
            {
                ViewModel.Locale.SelectedLocaleIndex = index;
            }
        }

        /// <summary>
        /// 적용 버튼 클릭을 처리한다.
        /// </summary>
        private void OnApplyClicked()
        {
            ViewModel?.Apply();
            Refresh();
        }

        /// <summary>
        /// 취소 버튼 클릭을 처리한다. 편집 값을 되돌린 뒤 창이 열려 있으면 닫기를 요청한다.
        /// 창이 없거나 열려 있지 않아도 되돌리기는 항상 수행한다.
        /// </summary>
        private void OnCancelClicked()
        {
            ViewModel?.Cancel();
            Refresh();
            if (window != null)
            {
                window.RequestClose();
            }
        }

        /// <summary>
        /// 기본값 버튼 클릭을 처리한다.
        /// </summary>
        private void OnResetClicked()
        {
            ViewModel?.ResetToDefaults();
            Refresh();
        }
    }
}
