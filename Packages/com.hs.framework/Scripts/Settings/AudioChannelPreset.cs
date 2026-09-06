using UnityEngine;
using UnityEngine.Audio;

namespace HS.Framework.Settings
{
    /// <summary>
    /// 하나의 프로젝트 오디오 채널과 믹서 노출 파라미터의 연결을 정의한다.
    /// </summary>
    [CreateAssetMenu(menuName = "HS/Settings/Audio Channel Preset", fileName = "AudioChannelPreset")]
    public sealed class AudioChannelPreset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] [Range(0f, 1f)] private float defaultVolume;
        [SerializeField] private bool isDefaultMuted;
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private string exposedVolumeParameter;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public float DefaultVolume => Mathf.Clamp01(defaultVolume);
        public bool IsDefaultMuted => isDefaultMuted;

        internal void Apply(float volume, bool isMuted)
        {
            if (mixer == null || string.IsNullOrWhiteSpace(exposedVolumeParameter))
            {
                return;
            }

            var linearVolume = isMuted ? 0f : Mathf.Clamp01(volume);
            mixer.SetFloat(exposedVolumeParameter, linearVolume <= 0f ? -80f : Mathf.Log10(linearVolume) * 20f);
        }
    }
}
