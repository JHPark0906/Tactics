using HS.Tactics.Foundation.Geometry;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 같은 적을 노리는 유닛들이 적 주위에 자리를 나눠 서도록 목적지를 흩는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 필요한가.</b> 여러 아군이 가장 가까운 같은 적을 고르고, 추격이 그 적의 정확한 위치를
    /// 목적지로 주므로 모두 한 점으로 몰린다. 같은 목적지에 집중하면 충돌로 이동이 막힐 수 있다.
    /// 자리를 미리 흩어 두면 겹칠 일 자체가 줄고, 적을 둘러싸는 그림이 나와 전술적으로도 낫다.
    /// </para>
    /// <para>
    /// <b>자리는 유닛의 고정된 번호로 정하며 다른 유닛의 수를 보지 않는다.</b>
    /// "이 적을 노리는 아군 몇 명 중 몇 번째"로 정하면 <b>한 명이 죽는 순간 남은 전원이 자리를 바꿔
    /// 우르르 이동한다.</b> 사용자 눈에는 명백한 버그다. 여기서는 자리 수를 고정해 두고 유닛의 번호만으로
    /// 고르므로, 누가 죽든 남은 유닛의 자리는 그대로다.
    /// </para>
    /// <para>
    /// 번호가 겹쳐 같은 자리를 고르는 유닛이 생길 수 있다. 그것은 겹침을 밀어내는 쪽에서 다루며,
    /// 여기서 수를 세어 피하려 하면 다시 다른 유닛에 기대게 된다.
    /// </para>
    /// <para>
    /// <b>자리를 쓸 수 없을 때의 대체도 정해져 있다.</b> 갈 수 없는 자리가 나오면 다음 자리를
    /// 정해진 차례대로 넘어가며 시도하고, 끝내 없으면 적의 자리 자체로 간다. 그 차례가 고정되어 있으므로
    /// 같은 상황에서 언제나 같은 자리를 고른다.
    /// </para>
    /// </remarks>
    public static class ApproachSlotResolver
    {
        /// <summary>자리를 나누지 않을 때 쓰는 값이다.</summary>
        public const int NoSlots = 0;

        /// <summary>
        /// 유닛의 번호로 고른 자리의 좌표를 구한다.
        /// </summary>
        /// <param name="targetPosition">둘러쌀 적의 평면 좌표이다.</param>
        /// <param name="referenceDirection">
        /// 자리를 펼칠 기준 방향이며 적에서 접근하는 쪽을 향한다. 길이가 0이면 +Z를 쓴다.
        /// </param>
        /// <param name="radius">적에서 얼마나 떨어져 설지이며 0 이하이면 적의 자리를 그대로 쓴다.</param>
        /// <param name="slotSeed">유닛의 고정된 번호이며 음수여도 된다.</param>
        /// <param name="slotCount">나눌 자리의 수이며 1 이하이면 흩지 않는다.</param>
        /// <param name="arcDegrees">자리를 펼칠 각도의 폭이다.</param>
        /// <param name="attempt">몇 번째 대체 자리인지이며 0이면 유닛의 원래 자리이다.</param>
        /// <returns>설 자리의 평면 좌표이다.</returns>
        public static PlanarPosition ResolveSlotPosition(
            PlanarPosition targetPosition,
            PlanarPosition referenceDirection,
            float radius,
            int slotSeed,
            int slotCount,
            float arcDegrees = 180f,
            int attempt = 0)
        {
            if (slotCount <= 1 || radius <= 0f)
            {
                return targetPosition;
            }

            var forward = referenceDirection.Normalized;
            if (forward.SqrMagnitude <= Mathf.Epsilon)
            {
                forward = new PlanarPosition(0f, 1f);
            }

            var slotIndex = ResolveSlotIndex(slotSeed, slotCount, attempt);

            // 자리를 각도 폭 안에 고르게 놓되 양 끝에는 두지 않는다. 끝에 두면 두 자리가 정면에서 겹친다.
            var step = arcDegrees / slotCount;
            var angle = -arcDegrees * 0.5f + (slotIndex + 0.5f) * step;
            var radians = angle * Mathf.Deg2Rad;
            var right = forward.PerpendicularClockwise();
            var direction = forward * Mathf.Cos(radians) + right * Mathf.Sin(radians);
            return targetPosition + direction * radius;
        }

        /// <summary>
        /// 유닛의 번호와 시도 횟수로 자리 번호를 구한다.
        /// 음수 번호도 언제나 같은 자리로 떨어지게 감싸 준다.
        /// </summary>
        /// <param name="slotSeed">유닛의 고정된 번호이다.</param>
        /// <param name="slotCount">나눌 자리의 수이다.</param>
        /// <param name="attempt">몇 번째 대체 자리인지이다.</param>
        /// <returns>0 이상 <paramref name="slotCount"/> 미만의 자리 번호이다.</returns>
        public static int ResolveSlotIndex(int slotSeed, int slotCount, int attempt = 0)
        {
            if (slotCount <= 1)
            {
                return 0;
            }

            var shifted = (long)slotSeed + attempt;
            var index = shifted % slotCount;
            return (int)(index < 0 ? index + slotCount : index);
        }
    }
}
