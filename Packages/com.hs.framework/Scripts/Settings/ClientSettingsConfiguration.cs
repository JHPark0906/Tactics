using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Framework.Settings
{
    /// <summary>
    /// FrameworkInitializer와 설정 UI가 함께 참조하는 클라이언트 설정 원본이다.
    /// </summary>
    [CreateAssetMenu(menuName = "HS/Settings/Client Settings Configuration", fileName = "ClientSettingsConfiguration")]
    public sealed class ClientSettingsConfiguration : ScriptableObject
    {
        [SerializeField] private InputActionAsset inputActionAsset;
        [SerializeField] private DisplaySettingsPreset displaySettingsPreset;
        [SerializeField] private AudioSettingsPreset audioSettingsPreset;
        [SerializeField] private LocaleSettingsPreset localeSettingsPreset;

        public InputActionAsset InputActionAsset => inputActionAsset;
        public DisplaySettingsPreset DisplaySettingsPreset => displaySettingsPreset;
        public AudioSettingsPreset AudioSettingsPreset => audioSettingsPreset;
        public LocaleSettingsPreset LocaleSettingsPreset => localeSettingsPreset;

        /// <summary>
        /// Framework가 초기화에 사용할 기본 설정 에셋 구성을 검사한다.
        /// 로케일 프리셋은 선택 사항이며, 미지정 시 로케일 초기화를 건너뛴다.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (inputActionAsset == null)
            {
                error = "초기 InputActionAsset이 지정되지 않았습니다.";
                return false;
            }

            if (displaySettingsPreset == null || audioSettingsPreset == null)
            {
                error = "그래픽, 오디오 기본 프리셋을 모두 지정해야 합니다.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
