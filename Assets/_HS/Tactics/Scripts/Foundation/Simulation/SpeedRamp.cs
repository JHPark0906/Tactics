using UnityEngine;

namespace HS.Tactics.Foundation.Simulation
{
    /// <summary>
    /// 한 스텝 동안 속도가 어떻게 바뀌고 얼마나 나아가는지를 담는다.
    /// </summary>
    public readonly struct SpeedStep
    {
        /// <summary>스텝이 끝났을 때의 속도(미터/초)이다.</summary>
        public float Speed { get; }

        /// <summary>이 스텝에 나아갈 거리(미터)이다.</summary>
        public float Distance { get; }

        /// <summary>스텝의 결과를 만든다.</summary>
        /// <param name="speed">스텝이 끝났을 때의 속도이다.</param>
        /// <param name="distance">이 스텝에 나아갈 거리이다.</param>
        public SpeedStep(float speed, float distance)
        {
            Speed = speed;
            Distance = distance;
        }
    }

    /// <summary>
    /// 정지에서 최고 속도까지 오르고 목적지 앞에서 다시 멈추는 속도 변화를 계산한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 필요한가.</b> 이동을 직접 계산하면서 처음부터 끝까지 최고 속도로 움직이면, 유닛이 출발하는
    /// 순간 튀어 나가고 목적지에서 급정거한다. 여기서 속도 변화를 그대로 계산하면 자연스러운 가감속을
    /// 얻으면서도 이동을 직접 쥘 수 있다.
    /// </para>
    /// <para>
    /// <b>바깥을 보지 않는다.</b> 이 계산은 지금 속도, 최고 속도, 가속도, 남은 거리, 스텝 길이만 쓴다.
    /// 시계를 읽지 않고 바깥 상태를 보지 않으므로 같은 입력이면 언제나 같은 결과가 나오고, 씬 없이 검증된다.
    /// </para>
    /// <para>
    /// <b>스텝의 이동 거리는 시작 속도와 끝 속도의 평균으로 잡는다.</b> 한쪽 값만 쓰면 가속 중에는 매 스텝
    /// 조금씩 적게 가고 감속 중에는 조금씩 많이 가서, 긴 이동에서 그 치우침이 쌓인다.
    /// 가속도가 일정한 구간에서 평균을 쓰는 것은 근사가 아니라 정확한 값이다.
    /// </para>
    /// <para>
    /// <b>감속률은 가속도와 같다고 본다.</b> 다른 값을 요구하는 근거가 따로 없어 고른 가장 단순한
    /// 선택이다 — 가속과 감속을 별도 수치로 나눌 필요가 생기면 그때 갈라도 된다.
    /// </para>
    /// </remarks>
    public static class SpeedRamp
    {
        /// <summary>
        /// 한 스텝만큼 속도를 올리거나 내리고, 그 사이에 나아갈 거리를 구한다.
        /// </summary>
        /// <remarks>
        /// 남은 거리보다 더 가지 않는다. 목적지를 지나쳐 놓고 되돌아오면 그 자리에서 떨기 때문이다.
        /// </remarks>
        /// <param name="currentSpeed">스텝을 시작할 때의 속도(미터/초)이며 음수는 0으로 본다.</param>
        /// <param name="maxSpeed">최고 속도(미터/초)이다.</param>
        /// <param name="acceleration">
        /// 가속도(미터/초²)이다. 0 이하이면 가속을 계산하지 않고 곧바로 최고 속도로 움직인다.
        /// </param>
        /// <param name="remainingDistance">목적지까지 남은 거리(미터)이다.</param>
        /// <param name="deltaTime">이 스텝의 길이(초)이며 0 이하이면 나아가지 않는다.</param>
        /// <param name="brakeOnArrival">목적지 앞에서 속도를 줄일지 여부이다.</param>
        /// <returns>스텝이 끝났을 때의 속도와 이 스텝에 나아갈 거리이다.</returns>
        public static SpeedStep Advance(
            float currentSpeed,
            float maxSpeed,
            float acceleration,
            float remainingDistance,
            float deltaTime,
            bool brakeOnArrival = true)
        {
            var speed = Mathf.Max(0f, currentSpeed);
            var top = Mathf.Max(0f, maxSpeed);
            var remaining = Mathf.Max(0f, remainingDistance);

            if (deltaTime <= 0f)
            {
                return new SpeedStep(speed, 0f);
            }

            if (acceleration <= 0f)
            {
                // 가속을 모형화하지 않는 경우이다. 곧바로 최고 속도로 움직인다.
                return new SpeedStep(top, Mathf.Min(top * deltaTime, remaining));
            }

            var target = brakeOnArrival && remaining <= ResolveBrakingDistance(speed, acceleration)
                ? 0f
                : top;

            var nextSpeed = Mathf.MoveTowards(speed, target, acceleration * deltaTime);

            // 가속도가 일정한 구간에서는 평균 속도로 잰 거리가 정확한 값이다.
            var distance = (speed + nextSpeed) * 0.5f * deltaTime;
            return new SpeedStep(nextSpeed, Mathf.Min(distance, remaining));
        }

        /// <summary>
        /// 지금 속도에서 멈추기까지 필요한 거리를 구한다.
        /// </summary>
        /// <remarks>
        /// 남은 거리가 이 값 이하로 떨어지는 순간부터 속도를 줄여야 목적지에서 멈춘다.
        /// </remarks>
        /// <param name="speed">지금 속도(미터/초)이다.</param>
        /// <param name="acceleration">감속에 쓸 가속도(미터/초²)이다.</param>
        /// <returns>멈추기까지 필요한 거리(미터)이며 가속도가 0 이하이면 0이다.</returns>
        public static float ResolveBrakingDistance(float speed, float acceleration)
        {
            if (acceleration <= 0f)
            {
                return 0f;
            }

            var current = Mathf.Max(0f, speed);
            return current * current / (2f * acceleration);
        }

        /// <summary>
        /// 가속과 감속 때문에 잃는 시간을 구한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>"구간의 길이"와 "잃는 시간"은 다르다.</b> 최고 속도에 오르는 데 <c>속도/가속도</c>초가 걸리지만
        /// 그 구간에서도 유닛은 움직인다. 잃는 시간은 <b>같은 거리를 최고 속도로 갔을 때와의 차이</b>이며
        /// 가속 구간에서 <c>속도/(2×가속도)</c>이다. 감속도 대칭이므로 둘을 합하면 <c>속도/가속도</c>가 된다.
        /// 구간의 길이를 그대로 쓰면 값이 두 배가 되고, 허용 범위로 쓰면 진짜 어긋남을 놓친다.
        /// </para>
        /// <para>
        /// 이동 거리가 짧아 최고 속도에 닿지 못하면 잃는 시간은 이보다 <b>작다.</b>
        /// 따라서 이 값은 거리와 무관한 <b>최댓값</b>이며 허용 범위로 쓰기에 알맞다.
        /// </para>
        /// </remarks>
        /// <param name="maxSpeed">최고 속도(미터/초)이다.</param>
        /// <param name="acceleration">가속도(미터/초²)이다.</param>
        /// <param name="brakeOnArrival">목적지 앞에서 감속하는지 여부이며, 아니면 절반만 잃는다.</param>
        /// <returns>잃는 시간의 최댓값(초)이며 가속도가 0 이하이면 0이다.</returns>
        public static float ResolveLostTime(float maxSpeed, float acceleration, bool brakeOnArrival = true)
        {
            if (acceleration <= 0f)
            {
                return 0f;
            }

            var rampLoss = Mathf.Max(0f, maxSpeed) / (2f * acceleration);
            return brakeOnArrival ? rampLoss * 2f : rampLoss;
        }
    }
}
