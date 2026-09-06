using System;
using HS.Framework.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace HS.Framework.Audio
{
    /// <summary>
    /// 오디오 채널 프리셋과 재생 출력 믹서 그룹의 연결을 정의한다.
    /// 채널 id를 지정한 사운드 큐는 이 연결을 통해 해당 믹서 그룹으로 출력된다.
    /// </summary>
    [Serializable]
    public struct SoundChannelRoute
    {
        [SerializeField] private AudioChannelPreset channel;
        [SerializeField] private AudioMixerGroup outputGroup;

        /// <summary>연결된 채널의 id이다. 채널이 지정되지 않았으면 null이다.</summary>
        public string ChannelId => channel != null ? channel.Id : null;

        /// <summary>채널 소속 재생이 출력될 믹서 그룹이다.</summary>
        public AudioMixerGroup OutputGroup => outputGroup;
    }
}
