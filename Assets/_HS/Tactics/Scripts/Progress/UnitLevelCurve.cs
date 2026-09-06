using System.Collections.Generic;
using UnityEngine;

namespace HS.Tactics.Progress
{
    /// <summary>
    /// 레벨이 오르는 데 드는 경험치와 레벨이 스탯을 얼마나 밀어 올리는지를 담는 곡선이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>여기 들어 있는 수는 전부 임시값이다.</b> 경험치 요구량은 직선으로 늘고 스탯 배수는 1.0으로
    /// 아무 효과가 없다. 곡선을 정하는 것은 밸런스이며 사용자 몫이므로, 코드는 <b>모양만</b> 갖춰 두고
    /// 값은 에셋에서 채우게 한다.
    /// </para>
    /// <para>
    /// <b>스탯 배수가 1.0인 것은 빠뜨린 것이 아니다.</b> 레벨이 올라도 아직 아무것도 세지지 않는 것이
    /// 지금의 의도된 상태이다. 값을 넣는 순간부터 세지므로, 밸런스를 잡을 때 곡선만 만지면 된다.
    /// </para>
    /// <para>
    /// <b>레벨 1의 배율도 데이터에 있다.</b> 코드가 레벨 1을 1.0으로 특별 취급하지 않고 배열의 첫 항목을
    /// 읽는다. 특별 취급하면 <b>레벨 1의 값을 에셋에서 바꿔도 아무 일도 일어나지 않아</b>, 밸런스를 잡는
    /// 쪽에서는 값이 반영되지 않는 이유를 코드를 열기 전에는 알 수 없다.
    /// </para>
    /// <para>
    /// <b>레벨은 1부터 센다.</b> 배열은 0부터 세므로 <see cref="GetXpToNext"/>가 그 차이를 흡수한다.
    /// 부르는 쪽이 그 변환을 알아야 하면 부르는 곳마다 어긋날 수 있다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "UnitLevelCurve", menuName = "Tactics/Unit Level Curve")]
    public sealed class UnitLevelCurve : ScriptableObject
    {
        /// <summary>곡선이 비어 있을 때 한 레벨을 올리는 데 드는 경험치이며 임시값이다.</summary>
        public const int FallbackXpToNext = 100;

        [Tooltip("올라갈 수 있는 가장 높은 레벨이다. 임시값이다.")]
        [SerializeField]
        [Min(1)]
        private int maxLevel = 10;

        [Tooltip("레벨 1에서 2로 올라가는 데 드는 경험치부터 차례로 적는다. 임시값이다.")]
        [SerializeField]
        private int[] xpToNext = { 100, 200, 300, 400, 500, 600, 700, 800, 900 };

        [Tooltip("레벨 1일 때부터 차례로 적는 체력 배수이다. 임시값이며 지금은 전부 1이라 효과가 없다.")]
        [SerializeField]
        private float[] healthMultipliers = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

        [Tooltip("레벨 1일 때부터 차례로 적는 공격력 배수이다. 임시값이며 지금은 전부 1이라 효과가 없다.")]
        [SerializeField]
        private float[] attackMultipliers = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

        /// <summary>이미 알린 배수이며 같은 경고를 되풀이하지 않으려고 기억해 둔다.</summary>
        private readonly HashSet<string> _warnedMultipliers = new();

        /// <summary>올라갈 수 있는 가장 높은 레벨이며 항상 1 이상이다.</summary>
        public int MaxLevel => Mathf.Max(UnitLevelProgress.StartingLevel, maxLevel);

        /// <summary>
        /// 그 레벨에서 다음 레벨로 올라가는 데 드는 경험치를 읽는다.
        /// </summary>
        /// <remarks>
        /// <b>최대 레벨에서는 올라갈 곳이 없다는 뜻으로 0을 돌려준다.</b> 큰 수를 돌려주면
        /// 부르는 쪽이 "아직 모자란 것"과 "더는 오를 수 없는 것"을 구별하지 못한다.
        /// </remarks>
        /// <param name="level">지금 레벨이다.</param>
        /// <returns>다음 레벨까지 드는 경험치이며, 더 오를 수 없으면 0이다.</returns>
        public int GetXpToNext(int level)
        {
            if (level >= MaxLevel)
            {
                return 0;
            }

            var index = Mathf.Max(UnitLevelProgress.StartingLevel, level) - UnitLevelProgress.StartingLevel;
            return xpToNext != null && index < xpToNext.Length
                ? Mathf.Max(1, xpToNext[index])
                : FallbackXpToNext;
        }

        /// <summary>그 레벨에서의 체력 배수를 읽는다.</summary>
        /// <param name="level">지금 레벨이다.</param>
        /// <returns>체력에 곱할 배수이며, 레벨 1이면 적힌 첫 값이다.</returns>
        public float GetHealthMultiplier(int level) => ReadMultiplier(healthMultipliers, level, "체력");

        /// <summary>그 레벨에서의 공격력 배수를 읽는다.</summary>
        /// <param name="level">지금 레벨이다.</param>
        /// <returns>공격력에 곱할 배수이며, 레벨 1이면 적힌 첫 값이다.</returns>
        public float GetAttackMultiplier(int level) => ReadMultiplier(attackMultipliers, level, "공격력");

        /// <summary>
        /// 레벨에 해당하는 배수를 읽는다. 적힌 범위를 벗어나면 마지막 값으로 붙잡고 알린다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>범위 밖을 마지막 값으로 붙잡는 것은 1로 물러나는 것보다 덜 놀랍기 때문이다.</b> 배열이
        /// 아홉 칸인데 열 번째 레벨을 물으면, 1로 물러날 경우 <b>레벨이 올랐는데 스탯이 도로 내려간다.</b>
        /// 마지막 값으로 붙잡으면 더 자라지 않을 뿐 뒷걸음질하지는 않는다.
        /// </para>
        /// <para>
        /// 붙잡을 때 알리는 것은 그것이 곡선과 최대 레벨이 어긋났다는 뜻이기 때문이다. 곡선마다 배열마다
        /// 한 번만 알린다 — 매 프레임 부를 수 있는 자리라 매번 알리면 같은 줄로 화면이 덮인다.
        /// </para>
        /// <para>
        /// 아무것도 적혀 있지 않은 배열만 1로 물러난다. 붙잡을 마지막 값조차 없기 때문이며,
        /// 배수가 없는 것과 곱해도 달라지지 않는 것은 같다.
        /// </para>
        /// </remarks>
        /// <param name="multipliers">읽을 배수 배열이다.</param>
        /// <param name="level">지금 레벨이다.</param>
        /// <param name="multiplierName">알릴 때 쓰는 배수의 이름이다.</param>
        /// <returns>읽은 배수이다.</returns>
        private float ReadMultiplier(float[] multipliers, int level, string multiplierName)
        {
            if (multipliers == null || multipliers.Length == 0)
            {
                WarnOnce(multiplierName, "배수가 하나도 적혀 있지 않아 1을 쓴다. 레벨이 올라도 스탯이 자라지 않는다.");
                return 1f;
            }

            var index = Mathf.Max(UnitLevelProgress.StartingLevel, level) - UnitLevelProgress.StartingLevel;
            if (index < multipliers.Length)
            {
                return multipliers[index];
            }

            WarnOnce(
                multiplierName,
                $"레벨 {level}의 배수가 적혀 있지 않아 마지막 값을 쓴다. " +
                $"적힌 것은 레벨 {multipliers.Length}까지인데 최대 레벨은 {MaxLevel}이다.");
            return multipliers[multipliers.Length - 1];
        }

        /// <summary>그 배수에 대해 아직 알리지 않았으면 한 번 알린다.</summary>
        /// <param name="multiplierName">알릴 배수의 이름이다.</param>
        /// <param name="message">알릴 내용이다.</param>
        private void WarnOnce(string multiplierName, string message)
        {
            if (!_warnedMultipliers.Add(multiplierName))
            {
                return;
            }

            Debug.LogWarning($"[UnitLevelCurve] {multiplierName} {message}", this);
        }
    }
}
