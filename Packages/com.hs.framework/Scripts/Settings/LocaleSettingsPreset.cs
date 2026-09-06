using UnityEngine;
using UnityEngine.Localization;

namespace HS.Framework.Settings
{
    /// <summary>
    /// 프로젝트의 기본 로케일과 사용자 변경 허용 여부를 정의한다.
    /// </summary>
    [CreateAssetMenu(menuName = "HS/Settings/Locale Settings Preset", fileName = "LocaleSettingsPreset")]
    public sealed class LocaleSettingsPreset : ScriptableObject
    {
        [SerializeField] private Locale defaultLocale;
        [SerializeField] private bool isUserConfigurable = true;

        public Locale DefaultLocale => defaultLocale;
        public bool IsUserConfigurable => isUserConfigurable;
    }
}
