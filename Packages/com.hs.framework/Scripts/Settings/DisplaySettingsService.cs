using System;
using UnityEngine;

namespace HS.Framework.Settings
{
    /// <summary>
    /// 해상도, 주사율, 전체화면, VSync, 프레임레이트 설정을 저장하고 적용한다.
    /// </summary>
    public sealed class DisplaySettingsService
    {
        /// <summary>
        /// 저장 데이터 형식을 식별하는 버전 번호이다. 버전 2에서 품질 레벨 필드가 추가되었다.
        /// 현재 버전 이하의 저장 데이터는 누락 필드를 필드 초기화식 기본값으로 보완해 수용하고,
        /// 현재 버전을 초과하는 저장 데이터만 거부한다. 따라서 새 필드를 추가할 때는 필드 초기화식으로
        /// 기본값을 지정하고, 버전은 호환 불가 변경에만 올린다.
        /// </summary>
        private const int SaveVersion = 2;

        /// <summary>
        /// 그래픽 설정을 저장할 때 사용하는 저장소 키이다.
        /// 기본 백엔드에서는 같은 이름의 PlayerPrefs 키로 기록된다.
        /// </summary>
        private const string StorageKey = "GraphicsSettings";

        /// <summary>
        /// 현재 애플리케이션에서 사용 중인 그래픽 설정 인스턴스이다.
        /// </summary>
        private static DisplaySettingsService _current;

        /// <summary>
        /// 품질 레벨 조회와 적용에 사용하는 컨트롤러이며, 없으면 Unity QualitySettings 기반 기본 구현을 만든다.
        /// </summary>
        private static IQualityLevelController _qualityController;

        /// <summary>
        /// 명시 품질 레벨을 처음 적용하기 전에 캡처한 프로젝트 기본 품질 레벨 인덱스이며,
        /// 센티널 -1 적용 시 이 값으로 복원한다. 아직 캡처하지 않았으면 null이다.
        /// </summary>
        private static int? _projectDefaultQualityLevel;

        /// <summary>
        /// 그래픽 설정이 저장되거나 적용된 뒤 발생한다.
        /// </summary>
        public event Action<DisplaySettingsService> OnDisplaySettingsChanged;

        /// <summary>
        /// 현재 그래픽 설정을 가져오며, 아직 초기화되지 않았으면 기본 설정으로 초기화한다.
        /// </summary>
        /// <remarks>
        /// 이 정적 접근자는 주입 경로가 아직 없는 지점(FrameworkInitializer의 설정 초기화, 에디터 도구, EditMode 테스트)에서만 쓴다.
        /// 씬에 배치되는 런타임 컴포넌트는 VContainer로 주입받거나 직접 초기화할 수 있다.
        /// </remarks>
        public static DisplaySettingsService Current
        {
            get
            {
                if (_current != null)
                {
                    return _current;
                }

                return Initialize();
            }
        }

        /// <summary>
        /// 적용할 화면 해상도의 너비를 가져온다.
        /// </summary>
        public int ResolutionWidth { get; private set; }

        /// <summary>
        /// 적용할 화면 해상도의 높이를 가져온다.
        /// </summary>
        public int ResolutionHeight { get; private set; }

        /// <summary>
        /// 적용할 우선 주사율을 가져오며, 0이면 플랫폼 기본값을 사용한다.
        /// </summary>
        public int PreferredRefreshRate { get; private set; }

        /// <summary>
        /// VSync가 비활성화되었을 때 사용할 목표 프레임레이트를 가져온다.
        /// </summary>
        public int TargetFrameRate { get; private set; }

        /// <summary>
        /// VSync 활성화 여부를 가져온다.
        /// </summary>
        public bool IsVSyncEnabled { get; private set; }

        /// <summary>
        /// 적용할 전체화면 모드를 가져온다.
        /// </summary>
        public FullScreenMode FullScreenMode { get; private set; }

        /// <summary>
        /// 적용할 품질 레벨 인덱스를 가져오며, -1이면 현재 프로젝트 기본값을 유지한다.
        /// </summary>
        public int QualityLevel { get; private set; } = -1;

        /// <summary>
        /// 현재 디스플레이에서 사용할 수 있는 해상도 목록을 가져온다.
        /// </summary>
        public static Resolution[] AvailableResolutions => Screen.resolutions;

        /// <summary>
        /// 품질 레벨 조회와 적용에 사용할 컨트롤러를 가져오거나 설정한다.
        /// null을 설정하면 Unity QualitySettings 기반 기본 구현으로 되돌린다.
        /// 설정 시 캡처된 프로젝트 기본 품질 레벨도 초기화하므로 테스트에서 대체 구현을 주입할 때 사용한다.
        /// </summary>
        public static IQualityLevelController QualityController
        {
            get => _qualityController ??= new UnityQualityLevelController();
            set
            {
                _qualityController = value;
                _projectDefaultQualityLevel = null;
            }
        }

        /// <summary>
        /// 그래픽 설정을 초기화하고 필요에 따라 저장된 설정을 불러오거나 화면에 적용한다.
        /// 캐시된 인스턴스를 새 인스턴스로 교체하지 않고 제자리에서 갱신하므로,
        /// 초기화 전에 <see cref="Current"/>를 캡처한 소비자와 <see cref="OnDisplaySettingsChanged"/> 구독자가
        /// 초기화 결과를 그대로 반영받는다.
        /// </summary>
        /// <param name="defaultSettings">저장된 설정이 없거나 사용하지 않을 때 적용할 기본 설정이다.</param>
        /// <param name="loadSavedSettings">저장된 설정을 불러올지 여부이다.</param>
        /// <param name="applyOnInitialize">초기화 직후 설정을 화면에 적용할지 여부이다.</param>
        /// <param name="saveOnInitialize">초기화 직후 설정을 저장할지 여부이다.</param>
        /// <returns>초기화된 그래픽 설정 인스턴스를 반환하며, <see cref="Current"/>와 항상 같은 인스턴스이다.</returns>
        public static DisplaySettingsService Initialize(
            DisplaySettingsPreset defaultSettings = null,
            bool loadSavedSettings = true,
            bool applyOnInitialize = true,
            bool saveOnInitialize = false)
        {
            var settings = EnsureCurrent();
            if (loadSavedSettings)
            {
                settings.LoadInto(defaultSettings);
            }
            else
            {
                settings.Import(defaultSettings ?? DisplaySettingsPreset.CreateCurrent());
            }

            if (saveOnInitialize)
            {
                settings.Save();
            }

            if (applyOnInitialize)
            {
                settings.Apply();
            }

            settings.NotifyChanged();
            return settings;
        }

        /// <summary>
        /// 저장된 그래픽 설정을 불러오고, 없거나 유효하지 않으면 기본 설정을 사용한다.
        /// <see cref="Initialize"/>와 마찬가지로 캐시된 인스턴스를 제자리에서 갱신한다.
        /// </summary>
        /// <param name="defaultSettings">저장된 설정이 없거나 유효하지 않을 때 적용할 기본 설정이다.</param>
        /// <returns>불러온 그래픽 설정 인스턴스를 반환하며, <see cref="Current"/>와 항상 같은 인스턴스이다.</returns>
        public static DisplaySettingsService Load(DisplaySettingsPreset defaultSettings = null)
        {
            var settings = EnsureCurrent();
            settings.LoadInto(defaultSettings);
            return settings;
        }

        /// <summary>
        /// 캐시된 그래픽 설정 인스턴스를 가져오며, 없으면 만들어 캐시한다.
        /// 모든 정적 진입점이 이 인스턴스를 재사용해야 초기화가 인스턴스를 교체하지 않는다.
        /// </summary>
        /// <returns>캐시된 그래픽 설정 인스턴스를 반환한다.</returns>
        private static DisplaySettingsService EnsureCurrent()
        {
            return _current ??= new DisplaySettingsService();
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
            _qualityController = null;
            _projectDefaultQualityLevel = null;
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
        /// 기본 설정으로 현재 인스턴스 상태를 초기화한 뒤, 저장된 설정이 있으면 덮어쓴다.
        /// 저장 데이터가 손상되었거나 형식 버전이 맞지 않으면 기본 설정으로 다시 저장한다.
        /// </summary>
        /// <param name="defaultSettings">저장된 설정이 없거나 유효하지 않을 때 적용할 기본 설정이다.</param>
        private void LoadInto(DisplaySettingsPreset defaultSettings)
        {
            Import(defaultSettings ?? DisplaySettingsPreset.CreateCurrent());

            if (!SettingsStorage.Current.TryRead(StorageKey, out var json))
            {
                return;
            }

            try
            {
                if (!Import(JsonUtility.FromJson<SettingsData>(json)))
                {
                    Save();
                }
            }
            catch (ArgumentException)
            {
                Save();
            }
        }

        /// <summary>
        /// 현재 그래픽 설정을 설정 저장소에 저장한다.
        /// </summary>
        public void Save()
        {
            SettingsStorage.Current.Write(StorageKey, JsonUtility.ToJson(Export()));
        }

        /// <summary>
        /// 현재 그래픽 설정을 화면과 품질 설정에 적용한다.
        /// </summary>
        public void Apply()
        {
            ApplyDisplay();
        }

        /// <summary>
        /// 그래픽 설정을 기본값으로 되돌리고 필요에 따라 적용하거나 저장한다.
        /// </summary>
        /// <param name="defaultSettings">되돌릴 때 사용할 기본 설정이다.</param>
        /// <param name="apply">초기화한 설정을 즉시 적용할지 여부이다.</param>
        /// <param name="save">초기화한 설정을 저장할지 여부이다.</param>
        public void ResetToDefaults(DisplaySettingsPreset defaultSettings = null, bool apply = true, bool save = true)
        {
            Import(defaultSettings ?? DisplaySettingsPreset.CreateCurrent());
            Commit(apply, save, ApplyDisplay);
        }

        /// <summary>
        /// 해상도, 전체화면 모드, 우선 주사율을 설정하고 필요에 따라 적용하거나 저장한다.
        /// </summary>
        /// <param name="width">설정할 해상도 너비이다.</param>
        /// <param name="height">설정할 해상도 높이이다.</param>
        /// <param name="fullScreenMode">설정할 전체화면 모드이다.</param>
        /// <param name="preferredRefreshRate">설정할 우선 주사율이며, 0이면 플랫폼 기본값을 사용한다.</param>
        /// <param name="apply">변경한 설정을 즉시 적용할지 여부이다.</param>
        /// <param name="save">변경한 설정을 저장할지 여부이다.</param>
        public void SetDisplay(
            int width,
            int height,
            FullScreenMode fullScreenMode,
            int preferredRefreshRate,
            bool apply = true,
            bool save = true)
        {
            ResolutionWidth = Mathf.Max(1, width);
            ResolutionHeight = Mathf.Max(1, height);
            FullScreenMode = fullScreenMode;
            PreferredRefreshRate = Mathf.Max(0, preferredRefreshRate);

            Commit(apply, save, ApplyDisplay);
        }

        /// <summary>
        /// Unity 해상도 정보와 전체화면 모드를 사용해 디스플레이 설정을 변경한다.
        /// </summary>
        /// <param name="resolution">설정할 Unity 해상도 정보이다.</param>
        /// <param name="fullScreenMode">설정할 전체화면 모드이다.</param>
        /// <param name="apply">변경한 설정을 즉시 적용할지 여부이다.</param>
        /// <param name="save">변경한 설정을 저장할지 여부이다.</param>
        public void SetDisplay(Resolution resolution, FullScreenMode fullScreenMode, bool apply = true, bool save = true)
        {
            SetDisplay(
                resolution.width,
                resolution.height,
                fullScreenMode,
                ToRoundedRefreshRate(resolution.refreshRateRatio),
                apply,
                save);
        }

        /// <summary>
        /// 해상도 너비와 높이를 설정하고 필요에 따라 적용하거나 저장한다.
        /// </summary>
        /// <param name="width">설정할 해상도 너비이다.</param>
        /// <param name="height">설정할 해상도 높이이다.</param>
        /// <param name="apply">변경한 설정을 즉시 적용할지 여부이다.</param>
        /// <param name="save">변경한 설정을 저장할지 여부이다.</param>
        public void SetResolution(int width, int height, bool apply = true, bool save = true)
        {
            ResolutionWidth = Mathf.Max(1, width);
            ResolutionHeight = Mathf.Max(1, height);
            Commit(apply, save, ApplyDisplay);
        }

        /// <summary>
        /// 우선 주사율을 설정하고 필요에 따라 적용하거나 저장한다.
        /// </summary>
        /// <param name="preferredRefreshRate">설정할 우선 주사율이며, 0이면 플랫폼 기본값을 사용한다.</param>
        /// <param name="apply">변경한 설정을 즉시 적용할지 여부이다.</param>
        /// <param name="save">변경한 설정을 저장할지 여부이다.</param>
        public void SetPreferredRefreshRate(int preferredRefreshRate, bool apply = true, bool save = true)
        {
            PreferredRefreshRate = Mathf.Max(0, preferredRefreshRate);
            Commit(apply, save, ApplyDisplay);
        }

        /// <summary>
        /// 전체화면 모드를 설정하고 필요에 따라 적용하거나 저장한다.
        /// </summary>
        /// <param name="fullScreenMode">설정할 전체화면 모드이다.</param>
        /// <param name="apply">변경한 설정을 즉시 적용할지 여부이다.</param>
        /// <param name="save">변경한 설정을 저장할지 여부이다.</param>
        public void SetFullScreenMode(FullScreenMode fullScreenMode, bool apply = true, bool save = true)
        {
            FullScreenMode = fullScreenMode;
            Commit(apply, save, ApplyDisplay);
        }

        /// <summary>
        /// VSync 활성화 여부를 설정하고 필요에 따라 적용하거나 저장한다.
        /// </summary>
        /// <param name="isEnabled">VSync를 활성화할지 여부이다.</param>
        /// <param name="apply">변경한 설정을 즉시 적용할지 여부이다.</param>
        /// <param name="save">변경한 설정을 저장할지 여부이다.</param>
        public void SetVSyncEnabled(bool isEnabled, bool apply = true, bool save = true)
        {
            IsVSyncEnabled = isEnabled;
            Commit(apply, save, ApplyDisplay);
        }

        /// <summary>
        /// 목표 프레임레이트를 설정하고 필요에 따라 적용하거나 저장한다.
        /// </summary>
        /// <param name="frameRate">설정할 목표 프레임레이트이며, 1보다 작으면 제한하지 않는다.</param>
        /// <param name="apply">변경한 설정을 즉시 적용할지 여부이다.</param>
        /// <param name="save">변경한 설정을 저장할지 여부이다.</param>
        public void SetTargetFrameRate(int frameRate, bool apply = true, bool save = true)
        {
            TargetFrameRate = SanitizeFrameRate(frameRate);
            Commit(apply, save, ApplyDisplay);
        }

        /// <summary>
        /// 품질 레벨 인덱스를 설정하고 필요에 따라 적용하거나 저장한다.
        /// </summary>
        /// <param name="qualityLevel">설정할 품질 레벨 인덱스이며, 음수면 현재 프로젝트 기본값을 유지한다.</param>
        /// <param name="apply">변경한 설정을 즉시 적용할지 여부이다.</param>
        /// <param name="save">변경한 설정을 저장할지 여부이다.</param>
        public void SetQualityLevel(int qualityLevel, bool apply = true, bool save = true)
        {
            QualityLevel = SanitizeQualityLevel(qualityLevel);
            Commit(apply, save, ApplyDisplay);
        }

        /// <summary>
        /// 목표 프레임레이트 값을 Unity에서 사용하는 제한 없음 값으로 정규화한다.
        /// </summary>
        /// <param name="frameRate">정규화할 목표 프레임레이트이다.</param>
        /// <returns>1보다 작으면 -1, 그렇지 않으면 입력한 프레임레이트를 반환한다.</returns>
        private static int SanitizeFrameRate(int frameRate)
        {
            return frameRate < 1 ? -1 : frameRate;
        }

        /// <summary>
        /// 품질 레벨 값을 프로젝트 기본값 유지 센티널로 정규화한다.
        /// 프로젝트 품질 단계가 줄어든 뒤에도 범위 밖 인덱스가 저장 데이터에 잔존하지 않도록
        /// 유효 범위를 벗어난 값은 모두 센티널로 되돌린다.
        /// </summary>
        /// <param name="qualityLevel">정규화할 품질 레벨 인덱스이다.</param>
        /// <returns>음수이거나 사용 가능한 품질 레벨 범위를 벗어나면 -1, 그렇지 않으면 입력한 품질 레벨 인덱스를 반환한다.</returns>
        private static int SanitizeQualityLevel(int qualityLevel)
        {
            return qualityLevel < 0 || qualityLevel >= QualityController.Count ? -1 : qualityLevel;
        }

        /// <summary>
        /// Unity RefreshRate 값을 정수 주사율로 반올림해 변환한다.
        /// </summary>
        /// <param name="refreshRate">변환할 Unity RefreshRate 값이다.</param>
        /// <returns>분모가 0이면 0, 그렇지 않으면 반올림한 주사율을 반환한다.</returns>
        private static int ToRoundedRefreshRate(RefreshRate refreshRate)
        {
            return refreshRate.denominator == 0 ? 0 : Mathf.RoundToInt((float)refreshRate.value / refreshRate.denominator);
        }

        /// <summary>
        /// 정수 주사율 값을 Unity RefreshRate 구조체로 변환한다.
        /// </summary>
        /// <param name="refreshRate">변환할 정수 주사율이다.</param>
        /// <returns>Unity Screen API에 전달할 RefreshRate 값을 반환한다.</returns>
        private static RefreshRate ToRefreshRate(int refreshRate)
        {
            return new RefreshRate
            {
                numerator = (uint)Mathf.Max(1, refreshRate),
                denominator = 1
            };
        }

        /// <summary>
        /// 현재 그래픽 설정을 Unity 화면, VSync, 목표 프레임레이트 설정에 적용한다.
        /// </summary>
        private void ApplyDisplay()
        {
            if (PreferredRefreshRate > 0)
            {
                Screen.SetResolution(ResolutionWidth, ResolutionHeight, FullScreenMode, ToRefreshRate(PreferredRefreshRate));
            }
            else
            {
                Screen.SetResolution(ResolutionWidth, ResolutionHeight, FullScreenMode);
            }

            EnsureProjectDefaultQualityCaptured();

            var qualityController = QualityController;
            var targetQualityLevel = QualityLevel >= 0 && QualityLevel < qualityController.Count
                ? QualityLevel
                : _projectDefaultQualityLevel.Value;

            if (targetQualityLevel != qualityController.GetQualityLevel())
            {
                qualityController.SetQualityLevel(targetQualityLevel);
            }

            QualitySettings.vSyncCount = IsVSyncEnabled ? 1 : 0;
            Application.targetFrameRate = IsVSyncEnabled ? -1 : TargetFrameRate;
        }

        /// <summary>
        /// 명시 품질 레벨을 적용하기 전 시점의 프로젝트 기본 품질 레벨을 아직 캡처하지 않았으면 캡처한다.
        /// 저장된 명시 레벨이 적용되기 전에 호출해야 시작 시점 기본값을 보존할 수 있다.
        /// </summary>
        private static void EnsureProjectDefaultQualityCaptured()
        {
            _projectDefaultQualityLevel ??= QualityController.GetQualityLevel();
        }

        /// <summary>
        /// 변경된 설정을 적용, 저장하고 변경 알림을 발생시킨다.
        /// </summary>
        /// <param name="apply">설정을 즉시 적용할지 여부이다.</param>
        /// <param name="save">설정을 저장할지 여부이다.</param>
        /// <param name="applyAction">설정을 적용할 때 실행할 동작이다.</param>
        private void Commit(bool apply, bool save, Action applyAction)
        {
            if (apply)
            {
                applyAction();
            }

            if (save)
            {
                Save();
            }

            NotifyChanged();
        }

        /// <summary>
        /// 그래픽 설정 변경 이벤트를 구독자에게 알린다.
        /// </summary>
        private void NotifyChanged()
        {
            OnDisplaySettingsChanged?.Invoke(this);
        }

        /// <summary>
        /// 그래픽 설정 프리셋 값을 현재 인스턴스로 가져온다.
        /// </summary>
        /// <param name="preset">가져올 그래픽 설정 프리셋이다.</param>
        private void Import(DisplaySettingsPreset preset)
        {
            ResolutionWidth = preset.ResolutionWidth;
            ResolutionHeight = preset.ResolutionHeight;
            PreferredRefreshRate = preset.PreferredRefreshRate;
            TargetFrameRate = preset.TargetFrameRate;
            IsVSyncEnabled = preset.IsVSyncEnabled;
            FullScreenMode = preset.FullScreenMode;
            QualityLevel = SanitizeQualityLevel(preset.QualityLevel);
        }

        /// <summary>
        /// 저장 데이터 값을 현재 인스턴스로 가져온다.
        /// </summary>
        /// <param name="data">가져올 저장 데이터이다.</param>
        /// <returns>저장 데이터가 유효하여 가져오기에 성공했으면 true, 그렇지 않으면 false를 반환한다.</returns>
        private bool Import(SettingsData data)
        {
            if (data == null || data.version > SaveVersion)
            {
                return false;
            }

            if (data.resolutionWidth > 0)
            {
                ResolutionWidth = data.resolutionWidth;
            }

            if (data.resolutionHeight > 0)
            {
                ResolutionHeight = data.resolutionHeight;
            }

            PreferredRefreshRate = Mathf.Max(0, data.preferredRefreshRate);
            TargetFrameRate = SanitizeFrameRate(data.targetFrameRate);
            IsVSyncEnabled = data.isVSyncEnabled;
            QualityLevel = SanitizeQualityLevel(data.qualityLevel);

            if (Enum.IsDefined(typeof(FullScreenMode), data.fullScreenMode))
            {
                FullScreenMode = data.fullScreenMode;
            }

            return true;
        }

        /// <summary>
        /// 현재 그래픽 설정을 저장 가능한 데이터 구조로 내보낸다.
        /// </summary>
        /// <returns>현재 그래픽 설정 값을 담은 저장 데이터를 반환한다.</returns>
        private SettingsData Export()
        {
            return new SettingsData
            {
                version = SaveVersion,
                resolutionWidth = ResolutionWidth,
                resolutionHeight = ResolutionHeight,
                preferredRefreshRate = PreferredRefreshRate,
                targetFrameRate = TargetFrameRate,
                isVSyncEnabled = IsVSyncEnabled,
                fullScreenMode = FullScreenMode,
                qualityLevel = QualityLevel
            };
        }

        /// <summary>
        /// 설정 저장소에 직렬화해 저장하는 그래픽 설정 데이터이다.
        /// </summary>
        [Serializable]
        private sealed class SettingsData
        {
            /// <summary>
            /// 저장 데이터 형식의 버전 번호이다.
            /// </summary>
            public int version;

            /// <summary>
            /// 저장된 화면 해상도 너비이다.
            /// </summary>
            public int resolutionWidth;

            /// <summary>
            /// 저장된 화면 해상도 높이이다.
            /// </summary>
            public int resolutionHeight;

            /// <summary>
            /// 저장된 우선 주사율이며, 0이면 플랫폼 기본값을 사용한다.
            /// </summary>
            public int preferredRefreshRate;

            /// <summary>
            /// 저장된 목표 프레임레이트이며, -1이면 제한하지 않는다.
            /// </summary>
            public int targetFrameRate;

            /// <summary>
            /// 저장된 VSync 활성화 여부이다.
            /// </summary>
            public bool isVSyncEnabled;

            /// <summary>
            /// 저장된 전체화면 모드이다.
            /// </summary>
            public FullScreenMode fullScreenMode;

            /// <summary>
            /// 저장된 품질 레벨 인덱스이며, -1이면 현재 프로젝트 기본값을 유지한다.
            /// </summary>
            public int qualityLevel = -1;
        }

        /// <summary>
        /// Unity QualitySettings를 사용하는 기본 품질 레벨 컨트롤러이다.
        /// </summary>
        private sealed class UnityQualityLevelController : IQualityLevelController
        {
            /// <inheritdoc />
            public int Count => QualitySettings.count;

            /// <inheritdoc />
            public int GetQualityLevel()
            {
                return QualitySettings.GetQualityLevel();
            }

            /// <inheritdoc />
            public void SetQualityLevel(int qualityLevel)
            {
                QualitySettings.SetQualityLevel(qualityLevel, true);
            }
        }
    }

    /// <summary>
    /// 품질 레벨의 개수 조회, 현재 레벨 조회, 레벨 적용을 추상화한다.
    /// 기본 구현은 Unity QualitySettings를 사용하며, 테스트에서는 대체 구현을 주입해
    /// 전역 품질 설정 상태에 대한 의존을 제거한다.
    /// </summary>
    public interface IQualityLevelController
    {
        /// <summary>
        /// 사용 가능한 품질 레벨 개수를 가져온다.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 현재 적용된 품질 레벨 인덱스를 가져온다.
        /// </summary>
        int GetQualityLevel();

        /// <summary>
        /// 지정한 품질 레벨을 적용한다.
        /// </summary>
        /// <param name="qualityLevel">적용할 품질 레벨 인덱스이다.</param>
        void SetQualityLevel(int qualityLevel);
    }
}
