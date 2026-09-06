using System;
using System.Collections.Generic;
using HS.Framework.Foundation.MVVM;
using HS.Framework.Settings;
using UnityEngine;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 그래픽 설정 화면의 편집 상태와 선택 가능한 옵션 목록을 제공한다.
    /// </summary>
    public sealed class GraphicsSettingsViewModel : ChangeTrackingViewModelBase, IEditableViewModel
    {
        /// <summary>
        /// 연결된 그래픽 설정 서비스이다.
        /// </summary>
        private readonly DisplaySettingsService _settingsService;

        /// <summary>
        /// 적용된 설정 스냅샷이다.
        /// </summary>
        private GraphicsSettingsSnapshot _appliedSnapshot;

        /// <summary>
        /// 기본값으로 되돌릴 때 사용할 스냅샷이다.
        /// </summary>
        private readonly GraphicsSettingsSnapshot _defaultSnapshot;

        /// <summary>
        /// 선택된 해상도 옵션 인덱스이다.
        /// </summary>
        private int _selectedResolutionIndex;

        /// <summary>
        /// 선택된 주사율 옵션 인덱스이다.
        /// </summary>
        private int _selectedRefreshRateIndex;

        /// <summary>
        /// 선택된 전체화면 모드 옵션 인덱스이다.
        /// </summary>
        private int _selectedFullScreenModeIndex;

        /// <summary>
        /// 선택된 목표 프레임레이트 옵션 인덱스이다.
        /// </summary>
        private int _selectedTargetFrameRateIndex;

        /// <summary>
        /// 선택된 품질 레벨 옵션 인덱스이다.
        /// </summary>
        private int _selectedQualityLevelIndex;

        /// <summary>
        /// UI에서 편집 중인 VSync 활성화 여부이다.
        /// </summary>
        private bool _isVSyncEnabled;

        /// <summary>
        /// 그래픽 설정 ViewModel을 생성한다.
        /// </summary>
        /// <param name="settingsService">연결할 그래픽 설정 서비스이다.</param>
        /// <param name="availableResolutions">프로젝트나 플랫폼에서 제공할 해상도 목록이다.</param>
        /// <param name="availableRefreshRates">프로젝트나 플랫폼에서 제공할 주사율 목록이다.</param>
        /// <param name="targetFrameRates">프로젝트에서 제공할 목표 프레임레이트 목록이다.</param>
        /// <param name="qualityLevelNames">프로젝트에서 제공할 품질 레벨 이름 목록이며, 없으면 QualitySettings의 이름을 사용한다.</param>
        public GraphicsSettingsViewModel(
            DisplaySettingsService settingsService,
            IEnumerable<SettingsResolution> availableResolutions = null,
            IEnumerable<int> availableRefreshRates = null,
            IEnumerable<int> targetFrameRates = null,
            IEnumerable<string> qualityLevelNames = null)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            Resolutions = BuildResolutionOptions(availableResolutions, _settingsService);
            RefreshRates = BuildRefreshRateOptions(availableRefreshRates, _settingsService.PreferredRefreshRate);
            FullScreenModes = BuildFullScreenModeOptions();
            TargetFrameRates = BuildTargetFrameRateOptions(targetFrameRates, _settingsService.TargetFrameRate);
            QualityLevels = BuildQualityLevelOptions(qualityLevelNames);
            _appliedSnapshot = GraphicsSettingsSnapshot.FromSettings(_settingsService);
            _defaultSnapshot = _appliedSnapshot;
            LoadSnapshot(_appliedSnapshot);
        }

        /// <summary>
        /// 선택 가능한 해상도 옵션 목록을 가져온다.
        /// </summary>
        public IReadOnlyList<SettingsOptionViewModel<SettingsResolution>> Resolutions { get; }

        /// <summary>
        /// 선택 가능한 주사율 옵션 목록을 가져온다.
        /// </summary>
        public IReadOnlyList<SettingsOptionViewModel<int>> RefreshRates { get; }

        /// <summary>
        /// 선택 가능한 전체화면 모드 옵션 목록을 가져온다.
        /// </summary>
        public IReadOnlyList<SettingsOptionViewModel<FullScreenMode>> FullScreenModes { get; }

        /// <summary>
        /// 선택 가능한 목표 프레임레이트 옵션 목록을 가져온다.
        /// </summary>
        public IReadOnlyList<SettingsOptionViewModel<int>> TargetFrameRates { get; }

        /// <summary>
        /// 선택 가능한 품질 레벨 옵션 목록을 가져오며, 첫 옵션은 프로젝트 기본값 유지를 의미하는 -1이다.
        /// </summary>
        public IReadOnlyList<SettingsOptionViewModel<int>> QualityLevels { get; }

        /// <summary>
        /// 선택된 해상도 옵션 인덱스를 가져오거나 설정한다.
        /// </summary>
        public int SelectedResolutionIndex
        {
            get => _selectedResolutionIndex;
            set => SetSelectedIndex(ref _selectedResolutionIndex, value, Resolutions.Count);
        }

        /// <summary>
        /// 선택된 주사율 옵션 인덱스를 가져오거나 설정한다.
        /// </summary>
        public int SelectedRefreshRateIndex
        {
            get => _selectedRefreshRateIndex;
            set => SetSelectedIndex(ref _selectedRefreshRateIndex, value, RefreshRates.Count);
        }

        /// <summary>
        /// 선택된 전체화면 모드 옵션 인덱스를 가져오거나 설정한다.
        /// </summary>
        public int SelectedFullScreenModeIndex
        {
            get => _selectedFullScreenModeIndex;
            set => SetSelectedIndex(ref _selectedFullScreenModeIndex, value, FullScreenModes.Count);
        }

        /// <summary>
        /// 선택된 목표 프레임레이트 옵션 인덱스를 가져오거나 설정한다.
        /// </summary>
        public int SelectedTargetFrameRateIndex
        {
            get => _selectedTargetFrameRateIndex;
            set => SetSelectedIndex(ref _selectedTargetFrameRateIndex, value, TargetFrameRates.Count);
        }

        /// <summary>
        /// 선택된 품질 레벨 옵션 인덱스를 가져오거나 설정한다.
        /// </summary>
        public int SelectedQualityLevelIndex
        {
            get => _selectedQualityLevelIndex;
            set => SetSelectedIndex(ref _selectedQualityLevelIndex, value, QualityLevels.Count);
        }

        /// <summary>
        /// UI에서 편집 중인 VSync 활성화 여부를 가져오거나 설정한다.
        /// </summary>
        public bool IsVSyncEnabled
        {
            get => _isVSyncEnabled;
            set
            {
                SetTrackedValue(ref _isVSyncEnabled, value);
            }
        }

        /// <summary>
        /// 적용된 값과 편집 중인 값이 다른지 여부를 가져온다.
        /// </summary>
        public override bool HasChanges => !CreatePendingSnapshot().Equals(_appliedSnapshot);

        /// <summary>
        /// 현재 편집 중인 그래픽 설정 값을 런타임에 적용하고 저장한다.
        /// </summary>
        public void Apply()
        {
            Apply(true, true);
        }

        /// <summary>
        /// 현재 편집 중인 그래픽 설정 값을 적용한다.
        /// </summary>
        /// <param name="applyToRuntime">Unity 화면 설정에 즉시 적용할지 여부이다.</param>
        /// <param name="save">PlayerPrefs에 저장할지 여부이다.</param>
        public void Apply(bool applyToRuntime = true, bool save = true)
        {
            var snapshot = CreatePendingSnapshot();
            _settingsService.SetDisplay(snapshot.Resolution.Width, snapshot.Resolution.Height, snapshot.FullScreenMode, snapshot.RefreshRate, false, false);
            _settingsService.SetVSyncEnabled(snapshot.IsVSyncEnabled, false, false);
            _settingsService.SetQualityLevel(snapshot.QualityLevel, false, false);
            _settingsService.SetTargetFrameRate(snapshot.TargetFrameRate, applyToRuntime, save);
            _appliedSnapshot = GraphicsSettingsSnapshot.FromSettings(_settingsService);
            LoadSnapshot(_appliedSnapshot);
        }

        /// <summary>
        /// 편집 중인 값을 마지막 적용 값으로 되돌린다.
        /// </summary>
        public void Cancel()
        {
            LoadSnapshot(_appliedSnapshot);
        }

        /// <summary>
        /// 편집 중인 값을 ViewModel 생성 시점의 기본값으로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            LoadSnapshot(_defaultSnapshot);
        }

        /// <summary>
        /// 현재 편집 중인 설정 스냅샷을 만든다.
        /// </summary>
        /// <returns>편집 중인 그래픽 설정 스냅샷이다.</returns>
        private GraphicsSettingsSnapshot CreatePendingSnapshot()
        {
            return new GraphicsSettingsSnapshot(
                Resolutions[SelectedResolutionIndex].Value,
                RefreshRates[SelectedRefreshRateIndex].Value,
                FullScreenModes[SelectedFullScreenModeIndex].Value,
                IsVSyncEnabled,
                TargetFrameRates[SelectedTargetFrameRateIndex].Value,
                QualityLevels[SelectedQualityLevelIndex].Value);
        }

        /// <summary>
        /// 지정한 스냅샷 값을 UI 편집 상태에 반영한다.
        /// </summary>
        /// <param name="snapshot">반영할 설정 스냅샷이다.</param>
        private void LoadSnapshot(GraphicsSettingsSnapshot snapshot)
        {
            _selectedResolutionIndex = SettingsOptions.IndexOfValueOrDefault(Resolutions, snapshot.Resolution);
            _selectedRefreshRateIndex = SettingsOptions.IndexOfValueOrDefault(RefreshRates, snapshot.RefreshRate);
            _selectedFullScreenModeIndex = SettingsOptions.IndexOfValueOrDefault(FullScreenModes, snapshot.FullScreenMode);
            _selectedTargetFrameRateIndex = SettingsOptions.IndexOfValueOrDefault(TargetFrameRates, snapshot.TargetFrameRate);
            _selectedQualityLevelIndex = SettingsOptions.IndexOfValueOrDefault(QualityLevels, snapshot.QualityLevel);
            _isVSyncEnabled = snapshot.IsVSyncEnabled;
            NotifyAllChanged();
        }

        /// <summary>
        /// 선택 인덱스 값을 설정하고 변경 알림을 발생시킨다.
        /// </summary>
        /// <param name="field">변경할 인덱스 필드이다.</param>
        /// <param name="value">요청된 인덱스 값이다.</param>
        /// <param name="count">옵션 개수이다.</param>
        private void SetSelectedIndex(ref int field, int value, int count)
        {
            SetTrackedValue(ref field, SettingsOptions.ClampSelectedIndex(value, count));
        }

        /// <summary>
        /// 모든 표시 속성 변경을 알린다.
        /// </summary>
        private void NotifyAllChanged()
        {
            OnPropertiesChanged(
                nameof(SelectedResolutionIndex),
                nameof(SelectedRefreshRateIndex),
                nameof(SelectedFullScreenModeIndex),
                nameof(SelectedTargetFrameRateIndex),
                nameof(SelectedQualityLevelIndex),
                nameof(IsVSyncEnabled),
                nameof(HasChanges));
        }

        /// <summary>
        /// 해상도 옵션 목록을 만든다.
        /// </summary>
        /// <param name="availableResolutions">외부에서 제공한 해상도 목록이다.</param>
        /// <param name="settingsService">현재 그래픽 설정 서비스이다.</param>
        /// <returns>중복이 제거된 해상도 옵션 목록이다.</returns>
        private static IReadOnlyList<SettingsOptionViewModel<SettingsResolution>> BuildResolutionOptions(
            IEnumerable<SettingsResolution> availableResolutions,
            DisplaySettingsService settingsService)
        {
            var values = new List<SettingsResolution>();

            if (availableResolutions != null)
            {
                foreach (var resolution in availableResolutions)
                {
                    AddResolutionIfMissing(values, resolution);
                }
            }
            else
            {
                foreach (var resolution in DisplaySettingsService.AvailableResolutions)
                {
                    AddResolutionIfMissing(values, new SettingsResolution(resolution.width, resolution.height));
                }
            }

            AddResolutionIfMissing(values, new SettingsResolution(settingsService.ResolutionWidth, settingsService.ResolutionHeight));

            var options = new List<SettingsOptionViewModel<SettingsResolution>>(values.Count);
            foreach (var resolution in values)
            {
                options.Add(new SettingsOptionViewModel<SettingsResolution>(resolution.DisplayName, resolution.DisplayName, resolution));
            }

            return options;
        }

        /// <summary>
        /// 주사율 옵션 목록을 만든다.
        /// </summary>
        /// <param name="availableRefreshRates">외부에서 제공한 주사율 목록이다.</param>
        /// <param name="currentRefreshRate">현재 그래픽 설정의 주사율 값이다.</param>
        /// <returns>중복이 제거된 주사율 옵션 목록이다.</returns>
        private static IReadOnlyList<SettingsOptionViewModel<int>> BuildRefreshRateOptions(IEnumerable<int> availableRefreshRates, int currentRefreshRate)
        {
            var values = new List<int> { 0 };

            if (availableRefreshRates != null)
            {
                foreach (var refreshRate in availableRefreshRates)
                {
                    AddPositiveIntIfMissing(values, refreshRate);
                }
            }
            else
            {
                foreach (var resolution in DisplaySettingsService.AvailableResolutions)
                {
                    AddPositiveIntIfMissing(values, ToRoundedRefreshRate(resolution.refreshRateRatio));
                }
            }

            AddPositiveIntIfMissing(values, currentRefreshRate);

            values.Sort();
            var options = new List<SettingsOptionViewModel<int>>(values.Count);
            foreach (var refreshRate in values)
            {
                var displayName = refreshRate <= 0 ? "Platform Default" : $"{refreshRate} Hz";
                options.Add(new SettingsOptionViewModel<int>(refreshRate.ToString(), displayName, refreshRate));
            }

            return options;
        }

        /// <summary>
        /// 전체화면 모드 옵션 목록을 만든다.
        /// </summary>
        /// <returns>전체화면 모드 옵션 목록이다.</returns>
        private static IReadOnlyList<SettingsOptionViewModel<FullScreenMode>> BuildFullScreenModeOptions()
        {
            return new[]
            {
                new SettingsOptionViewModel<FullScreenMode>(nameof(FullScreenMode.FullScreenWindow), "Fullscreen Window", FullScreenMode.FullScreenWindow),
                new SettingsOptionViewModel<FullScreenMode>(nameof(FullScreenMode.ExclusiveFullScreen), "Exclusive Fullscreen", FullScreenMode.ExclusiveFullScreen),
                new SettingsOptionViewModel<FullScreenMode>(nameof(FullScreenMode.MaximizedWindow), "Maximized Window", FullScreenMode.MaximizedWindow),
                new SettingsOptionViewModel<FullScreenMode>(nameof(FullScreenMode.Windowed), "Windowed", FullScreenMode.Windowed)
            };
        }

        /// <summary>
        /// 목표 프레임레이트 옵션 목록을 만든다.
        /// </summary>
        /// <param name="targetFrameRates">외부에서 제공한 목표 프레임레이트 목록이다.</param>
        /// <param name="currentTargetFrameRate">현재 그래픽 설정의 목표 프레임레이트 값이다.</param>
        /// <returns>목표 프레임레이트 옵션 목록이다.</returns>
        private static IReadOnlyList<SettingsOptionViewModel<int>> BuildTargetFrameRateOptions(IEnumerable<int> targetFrameRates, int currentTargetFrameRate)
        {
            var values = new List<int>();
            var source = targetFrameRates ?? new[] { -1, 30, 60, 120, 144 };

            foreach (var frameRate in source)
            {
                var normalizedValue = frameRate < 1 ? -1 : frameRate;
                if (!values.Contains(normalizedValue))
                {
                    values.Add(normalizedValue);
                }
            }

            var normalizedCurrentTargetFrameRate = currentTargetFrameRate < 1 ? -1 : currentTargetFrameRate;
            if (!values.Contains(normalizedCurrentTargetFrameRate))
            {
                values.Add(normalizedCurrentTargetFrameRate);
            }

            if (values.Count == 0)
            {
                values.Add(-1);
            }

            values.Sort();
            var options = new List<SettingsOptionViewModel<int>>(values.Count);
            foreach (var frameRate in values)
            {
                var displayName = frameRate < 1 ? "Unlimited" : frameRate.ToString();
                options.Add(new SettingsOptionViewModel<int>(frameRate.ToString(), displayName, frameRate));
            }

            return options;
        }

        /// <summary>
        /// 품질 레벨 옵션 목록을 만든다. 첫 옵션은 프로젝트 기본값 유지를 의미하는 센티널 -1이다.
        /// </summary>
        /// <param name="qualityLevelNames">외부에서 제공한 품질 레벨 이름 목록이며, 없으면 QualitySettings의 이름을 사용한다.</param>
        /// <returns>품질 레벨 옵션 목록이다.</returns>
        private static IReadOnlyList<SettingsOptionViewModel<int>> BuildQualityLevelOptions(IEnumerable<string> qualityLevelNames)
        {
            var options = new List<SettingsOptionViewModel<int>>
            {
                new SettingsOptionViewModel<int>("-1", "Project Default", -1)
            };

            var names = new List<string>(qualityLevelNames ?? QualitySettings.names);
            for (var level = 0; level < names.Count; level++)
            {
                var displayName = string.IsNullOrWhiteSpace(names[level]) ? $"Level {level}" : names[level];
                options.Add(new SettingsOptionViewModel<int>(level.ToString(), displayName, level));
            }

            return options;
        }

        /// <summary>
        /// 해상도 목록에 값이 없으면 추가한다.
        /// </summary>
        /// <param name="values">해상도 목록이다.</param>
        /// <param name="resolution">추가할 해상도 값이다.</param>
        private static void AddResolutionIfMissing(ICollection<SettingsResolution> values, SettingsResolution resolution)
        {
            if (!values.Contains(resolution))
            {
                values.Add(resolution);
            }
        }

        /// <summary>
        /// 양수 정수 목록에 값이 없으면 추가한다.
        /// </summary>
        /// <param name="values">정수 목록이다.</param>
        /// <param name="value">추가할 값이다.</param>
        private static void AddPositiveIntIfMissing(ICollection<int> values, int value)
        {
            if (value > 0 && !values.Contains(value))
            {
                values.Add(value);
            }
        }

        /// <summary>
        /// Unity 주사율 값을 정수로 반올림한다.
        /// </summary>
        /// <param name="refreshRate">Unity 주사율 값이다.</param>
        /// <returns>정수 주사율 값이다.</returns>
        private static int ToRoundedRefreshRate(RefreshRate refreshRate)
        {
            return refreshRate.denominator == 0 ? 0 : Mathf.RoundToInt((float)refreshRate.value / refreshRate.denominator);
        }

        /// <summary>
        /// 그래픽 설정 값을 비교하기 위한 내부 스냅샷이다.
        /// </summary>
        private readonly struct GraphicsSettingsSnapshot : IEquatable<GraphicsSettingsSnapshot>
        {
            /// <summary>
            /// 그래픽 설정 스냅샷을 생성한다.
            /// </summary>
            /// <param name="resolution">화면 해상도 값이다.</param>
            /// <param name="refreshRate">우선 주사율 값이다.</param>
            /// <param name="fullScreenMode">전체화면 모드이다.</param>
            /// <param name="isVSyncEnabled">VSync 활성화 여부이다.</param>
            /// <param name="targetFrameRate">목표 프레임레이트이다.</param>
            /// <param name="qualityLevel">품질 레벨 인덱스이며, 음수면 프로젝트 기본값 유지를 의미한다.</param>
            public GraphicsSettingsSnapshot(
                SettingsResolution resolution,
                int refreshRate,
                FullScreenMode fullScreenMode,
                bool isVSyncEnabled,
                int targetFrameRate,
                int qualityLevel)
            {
                Resolution = resolution;
                RefreshRate = Math.Max(0, refreshRate);
                FullScreenMode = fullScreenMode;
                IsVSyncEnabled = isVSyncEnabled;
                TargetFrameRate = targetFrameRate < 1 ? -1 : targetFrameRate;
                QualityLevel = qualityLevel < 0 ? -1 : qualityLevel;
            }

            /// <summary>
            /// 화면 해상도 값을 가져온다.
            /// </summary>
            public SettingsResolution Resolution { get; }

            /// <summary>
            /// 우선 주사율 값을 가져온다.
            /// </summary>
            public int RefreshRate { get; }

            /// <summary>
            /// 전체화면 모드를 가져온다.
            /// </summary>
            public FullScreenMode FullScreenMode { get; }

            /// <summary>
            /// VSync 활성화 여부를 가져온다.
            /// </summary>
            public bool IsVSyncEnabled { get; }

            /// <summary>
            /// 목표 프레임레이트를 가져온다.
            /// </summary>
            public int TargetFrameRate { get; }

            /// <summary>
            /// 품질 레벨 인덱스를 가져오며, -1이면 프로젝트 기본값 유지를 의미한다.
            /// </summary>
            public int QualityLevel { get; }

            /// <summary>
            /// 그래픽 설정 서비스에서 스냅샷을 만든다.
            /// </summary>
            /// <param name="settingsService">그래픽 설정 서비스이다.</param>
            /// <returns>현재 그래픽 설정 스냅샷이다.</returns>
            public static GraphicsSettingsSnapshot FromSettings(DisplaySettingsService settingsService)
            {
                return new GraphicsSettingsSnapshot(
                    new SettingsResolution(settingsService.ResolutionWidth, settingsService.ResolutionHeight),
                    settingsService.PreferredRefreshRate,
                    settingsService.FullScreenMode,
                    settingsService.IsVSyncEnabled,
                    settingsService.TargetFrameRate,
                    settingsService.QualityLevel);
            }

            /// <summary>
            /// 다른 스냅샷과 같은지 확인한다.
            /// </summary>
            /// <param name="other">비교할 스냅샷이다.</param>
            /// <returns>모든 설정 값이 같으면 true를 반환한다.</returns>
            public bool Equals(GraphicsSettingsSnapshot other)
            {
                return Resolution.Equals(other.Resolution)
                       && RefreshRate == other.RefreshRate
                       && FullScreenMode == other.FullScreenMode
                       && IsVSyncEnabled == other.IsVSyncEnabled
                       && TargetFrameRate == other.TargetFrameRate
                       && QualityLevel == other.QualityLevel;
            }
        }
    }
}
