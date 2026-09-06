using UnityEngine;

namespace HS.Tactics.Foundation.Simulation
{
    /// <summary>
    /// 두 로직 스텝 사이를 화면에서 메울 때 쓰는 계산이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 로직은 정해진 주기로만 움직이므로, 화면이 그보다 빠르면 그대로 그릴 때 끊겨 보인다.
    /// 직전 스텝과 지금 스텝 사이 어디쯤인지를 구해 그 사이를 이어 주면 부드러워진다.
    /// </para>
    /// <para>
    /// <b>이 값은 화면 쪽으로만 흐른다.</b> 여기서 나온 비율이나 그것으로 만든 위치가 판단으로 돌아오면
    /// <b>프레임 시각이 결과를 바꾼다.</b> 같은 시드로 두 번 돌려도 화면 사정에 따라 다른 전투가 되는데,
    /// 이 실수는 아무 신호도 남기지 않아 조용하다.
    /// </para>
    /// </remarks>
    public static class FixedStepInterpolation
    {
        /// <summary>
        /// 아직 스텝을 채우지 못한 시간을 스텝 크기에 대한 비율로 바꾼다.
        /// </summary>
        /// <param name="pendingTime">스텝을 채우지 못하고 남아 있는 시간(초)이다.</param>
        /// <param name="stepDuration">한 스텝이 나타내는 시간(초)이며 0 이하이면 0을 반환한다.</param>
        /// <returns>0 이상 1 이하의 비율이다.</returns>
        public static float ResolveAlpha(float pendingTime, float stepDuration)
        {
            if (stepDuration <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(pendingTime / stepDuration);
        }
    }
}
