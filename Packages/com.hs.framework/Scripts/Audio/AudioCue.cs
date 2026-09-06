using UnityEngine;
using UnityEngine.Audio;

namespace HS.Framework.Audio
{
    /// <summary>
    /// 재생할 클립 후보와 볼륨·피치 변주 범위, 출력 대상을 정의하는 사운드 큐 에셋이다.
    /// </summary>
    [CreateAssetMenu(menuName = "HS/Audio/Audio Cue", fileName = "AudioCue")]
    public sealed class AudioCue : ScriptableObject
    {
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private AudioCueClipSelectionMode clipSelectionMode;
        [SerializeField] [Range(0f, 1f)] private float minVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float maxVolume = 1f;
        [SerializeField] [Range(0.1f, 3f)] private float minPitch = 1f;
        [SerializeField] [Range(0.1f, 3f)] private float maxPitch = 1f;
        [SerializeField] private bool isLoop;
        [SerializeField] private string outputChannelId;
        [SerializeField] private AudioMixerGroup outputGroupOverride;

        [System.NonSerialized] private int _previousClipIndex = -1;

        /// <summary>클립 선택 방식이다.</summary>
        public AudioCueClipSelectionMode ClipSelectionMode => clipSelectionMode;

        /// <summary>루프 재생 여부이다.</summary>
        public bool IsLoop => isLoop;

        /// <summary>출력 채널 id이다. 지정하지 않았으면 비어 있다.</summary>
        public string OutputChannelId => outputChannelId;

        /// <summary>채널 라우팅보다 우선하는 직접 출력 믹서 그룹이다.</summary>
        public AudioMixerGroup OutputGroupOverride => outputGroupOverride;

        /// <summary>
        /// 클립을 고르고 변주를 적용해 1회 재생 정보를 확정한다. 재생할 수 있는 클립이 없으면 false를 반환한다.
        /// </summary>
        public bool TryCreatePlayback(out AudioCuePlayback playback)
        {
            if (clips == null || clips.Length == 0)
            {
                playback = default;
                return false;
            }

            var clipIndex = AudioCuePlaybackResolver.SelectClipIndex(
                clipSelectionMode, clips.Length, _previousClipIndex, Random.value);
            var clip = clipIndex >= 0 ? clips[clipIndex] : null;
            if (clip == null)
            {
                playback = default;
                return false;
            }

            _previousClipIndex = clipIndex;
            playback = new AudioCuePlayback(
                clip,
                AudioCuePlaybackResolver.ResolveInRange(minVolume, maxVolume, Random.value),
                AudioCuePlaybackResolver.ResolveInRange(minPitch, maxPitch, Random.value),
                isLoop,
                outputChannelId,
                outputGroupOverride);
            return true;
        }
    }
}
