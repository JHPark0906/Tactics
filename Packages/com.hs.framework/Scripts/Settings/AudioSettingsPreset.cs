using UnityEngine;

namespace HS.Framework.Settings
{
    /// <summary>
    /// 프로젝트의 기본 마스터 및 오디오 채널 구성을 정의한다.
    /// </summary>
    [CreateAssetMenu(menuName = "HS/Settings/Audio Settings Preset", fileName = "AudioSettingsPreset")]
    public sealed class AudioSettingsPreset : ScriptableObject
    {
        [SerializeField] [Range(0f, 1f)] private float masterVolume;
        [SerializeField] private bool isMasterMuted;
        [SerializeField] private AudioChannelPreset[] channelPresets;

        /// <summary>
        /// 마스터 채널의 기본 볼륨을 가져오며, 0과 1 사이로 제한된다.
        /// </summary>
        public float MasterVolume => Mathf.Clamp01(masterVolume);

        /// <summary>
        /// 마스터 채널의 기본 음소거 여부를 가져온다.
        /// </summary>
        public bool IsMasterMuted => isMasterMuted;

        /// <summary>
        /// 프리셋이 정의한 오디오 채널 프리셋 목록을 가져오며, 정의되지 않았으면 빈 배열이다.
        /// </summary>
        public AudioChannelPreset[] ChannelPresets => channelPresets ?? System.Array.Empty<AudioChannelPreset>();

        /// <summary>
        /// 테스트와 런타임 조립에 사용할 비저장 프리셋을 만든다.
        /// </summary>
        /// <param name="masterVolume">마스터 채널의 기본 볼륨이다.</param>
        /// <param name="isMasterMuted">마스터 채널의 기본 음소거 여부이다.</param>
        /// <param name="channelPresets">포함할 오디오 채널 프리셋 목록이며, null이면 채널이 없다.</param>
        /// <returns>생성된 비저장 오디오 설정 프리셋을 반환한다.</returns>
        public static AudioSettingsPreset CreateRuntime(
            float masterVolume,
            bool isMasterMuted = false,
            AudioChannelPreset[] channelPresets = null)
        {
            var preset = CreateInstance<AudioSettingsPreset>();
            preset.masterVolume = Mathf.Clamp01(masterVolume);
            preset.isMasterMuted = isMasterMuted;
            preset.channelPresets = channelPresets;
            return preset;
        }
    }
}
