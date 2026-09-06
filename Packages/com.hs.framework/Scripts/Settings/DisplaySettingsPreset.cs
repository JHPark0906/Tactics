using UnityEngine;

namespace HS.Framework.Settings
{
    /// <summary>
    /// 그래픽 설정 초기화에 사용할 프로젝트 기본값을 정의한다.
    /// </summary>
    [CreateAssetMenu(menuName = "HS/Settings/Display Settings Preset", fileName = "DisplaySettingsPreset")]
    public sealed class DisplaySettingsPreset : ScriptableObject
    {
        [SerializeField] [Min(1)] private int resolutionWidth = 1920;
        [SerializeField] [Min(1)] private int resolutionHeight = 1080;
        [SerializeField] private FullScreenMode fullScreenMode = FullScreenMode.FullScreenWindow;
        [SerializeField] [Min(0)] private int preferredRefreshRate;
        [SerializeField] private bool isVSyncEnabled = true;
        [SerializeField] private int targetFrameRate = -1;

        /// <summary>
        /// 기본 품질 레벨 인덱스이며, 음수면 현재 프로젝트 기본값을 유지한다.
        /// </summary>
        [SerializeField] private int qualityLevel = -1;

        public int ResolutionWidth => Mathf.Max(1, resolutionWidth);
        public int ResolutionHeight => Mathf.Max(1, resolutionHeight);
        public FullScreenMode FullScreenMode => fullScreenMode;
        public int PreferredRefreshRate => Mathf.Max(0, preferredRefreshRate);
        public bool IsVSyncEnabled => isVSyncEnabled;
        public int TargetFrameRate => targetFrameRate < 1 ? -1 : targetFrameRate;

        /// <summary>
        /// 기본 품질 레벨 인덱스를 가져오며, -1이면 현재 프로젝트 기본값을 유지한다.
        /// </summary>
        public int QualityLevel => qualityLevel < 0 ? -1 : qualityLevel;

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 프리셋을 만든다.
        /// </summary>
        public static DisplaySettingsPreset CreateRuntime(
            int resolutionWidth,
            int resolutionHeight,
            FullScreenMode fullScreenMode,
            int preferredRefreshRate,
            bool isVSyncEnabled,
            int targetFrameRate,
            int qualityLevel = -1)
        {
            var preset = CreateInstance<DisplaySettingsPreset>();
            preset.resolutionWidth = Mathf.Max(1, resolutionWidth);
            preset.resolutionHeight = Mathf.Max(1, resolutionHeight);
            preset.fullScreenMode = fullScreenMode;
            preset.preferredRefreshRate = Mathf.Max(0, preferredRefreshRate);
            preset.isVSyncEnabled = isVSyncEnabled;
            preset.targetFrameRate = targetFrameRate < 1 ? -1 : targetFrameRate;
            preset.qualityLevel = qualityLevel < 0 ? -1 : qualityLevel;
            return preset;
        }

        /// <summary>
        /// 현재 클라이언트 화면 상태를 기준으로 프리셋을 만든다.
        /// </summary>
        public static DisplaySettingsPreset CreateCurrent()
        {
            var currentResolution = Screen.currentResolution;
            var width = Screen.width > 0 ? Screen.width : currentResolution.width;
            var height = Screen.height > 0 ? Screen.height : currentResolution.height;
            var refreshRate = currentResolution.refreshRateRatio.denominator == 0
                ? 0
                : Mathf.RoundToInt((float)currentResolution.refreshRateRatio.value / currentResolution.refreshRateRatio.denominator);

            return CreateRuntime(
                width,
                height,
                Screen.fullScreenMode,
                refreshRate,
                QualitySettings.vSyncCount > 0,
                Application.targetFrameRate);
        }
    }
}
