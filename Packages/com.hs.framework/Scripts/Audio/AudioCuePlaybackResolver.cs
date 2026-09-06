using UnityEngine;

namespace HS.Framework.Audio
{
    /// <summary>
    /// 사운드 큐의 클립 선택과 변주 값 계산을 담당하는 순수 로직이다.
    /// 난수 입력을 매개변수로 받아 결정적으로 검증할 수 있다.
    /// </summary>
    public static class AudioCuePlaybackResolver
    {
        /// <summary>
        /// 선택 방식에 따라 다음에 재생할 클립 인덱스를 계산한다. 후보가 없으면 -1을 반환한다.
        /// </summary>
        /// <param name="mode">클립 선택 방식이다.</param>
        /// <param name="clipCount">후보 클립 수이다.</param>
        /// <param name="previousIndex">직전에 재생한 클립 인덱스이다. 재생 이력이 없으면 -1이다.</param>
        /// <param name="random01">[0, 1] 범위의 난수 값이다. 범위를 벗어나면 잘라서 사용한다.</param>
        public static int SelectClipIndex(AudioCueClipSelectionMode mode, int clipCount, int previousIndex, float random01)
        {
            if (clipCount <= 0)
            {
                return -1;
            }

            if (clipCount == 1)
            {
                return 0;
            }

            if (mode == AudioCueClipSelectionMode.Sequential)
            {
                var nextIndex = previousIndex + 1;
                return nextIndex < 0 || nextIndex >= clipCount ? 0 : nextIndex;
            }

            var index = (int)(Mathf.Clamp01(random01) * clipCount);
            if (index >= clipCount)
            {
                index = clipCount - 1;
            }

            if (index == previousIndex)
            {
                index = (index + 1) % clipCount;
            }

            return index;
        }

        /// <summary>
        /// 변주 범위 안에서 난수 값에 해당하는 값을 계산한다. 최소가 최대보다 크면 서로 바꿔 해석한다.
        /// </summary>
        /// <param name="min">범위 최소값이다.</param>
        /// <param name="max">범위 최대값이다.</param>
        /// <param name="random01">[0, 1] 범위의 난수 값이다. 범위를 벗어나면 잘라서 사용한다.</param>
        public static float ResolveInRange(float min, float max, float random01)
        {
            if (min > max)
            {
                (min, max) = (max, min);
            }

            return Mathf.Lerp(min, max, Mathf.Clamp01(random01));
        }
    }
}
