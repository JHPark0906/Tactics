using UnityEngine;
using UnityEngine.Audio;

namespace HS.Framework.Audio
{
    /// <summary>
    /// 사운드 큐에서 확정된 1회 재생 정보이다.
    /// </summary>
    public readonly struct AudioCuePlayback
    {
        /// <summary>재생할 클립이다.</summary>
        public AudioClip Clip { get; }

        /// <summary>변주가 적용된 볼륨이다.</summary>
        public float Volume { get; }

        /// <summary>변주가 적용된 피치이다.</summary>
        public float Pitch { get; }

        /// <summary>루프 재생 여부이다.</summary>
        public bool IsLoop { get; }

        /// <summary>출력 채널 id이다. 지정하지 않았으면 비어 있다.</summary>
        public string OutputChannelId { get; }

        /// <summary>채널 라우팅보다 우선하는 직접 출력 믹서 그룹이다.</summary>
        public AudioMixerGroup OutputGroupOverride { get; }

        /// <summary>확정된 재생 정보를 생성한다.</summary>
        public AudioCuePlayback(
            AudioClip clip,
            float volume,
            float pitch,
            bool isLoop,
            string outputChannelId,
            AudioMixerGroup outputGroupOverride)
        {
            Clip = clip;
            Volume = volume;
            Pitch = pitch;
            IsLoop = isLoop;
            OutputChannelId = outputChannelId;
            OutputGroupOverride = outputGroupOverride;
        }
    }
}
