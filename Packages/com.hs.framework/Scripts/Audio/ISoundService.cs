using UnityEngine;

namespace HS.Framework.Audio
{
    /// <summary>
    /// 사운드 큐 기반 효과음·배경음 재생을 제공하는 계약이다.
    /// </summary>
    public interface ISoundService
    {
        /// <summary>효과음 큐를 2D로 재생한다.</summary>
        void PlaySfx(AudioCue cue);

        /// <summary>효과음 큐를 지정 위치에서 3D로 재생한다.</summary>
        void PlaySfx(AudioCue cue, Vector3 position);

        /// <summary>재생 중인 모든 효과음을 중지하고 소스를 풀로 반환한다.</summary>
        void StopAllSfx();

        /// <summary>배경음 큐를 크로스페이드로 재생한다. 재생 중인 배경음은 같은 시간 동안 페이드아웃된다.</summary>
        /// <param name="fadeSeconds">크로스페이드 시간(초)이다. 0 이하이면 즉시 전환한다.</param>
        void PlayBgm(AudioCue cue, float fadeSeconds);

        /// <summary>재생 중인 배경음을 페이드아웃으로 중지한다.</summary>
        /// <param name="fadeSeconds">페이드아웃 시간(초)이다. 0 이하이면 즉시 중지한다.</param>
        void StopBgm(float fadeSeconds);
    }
}
