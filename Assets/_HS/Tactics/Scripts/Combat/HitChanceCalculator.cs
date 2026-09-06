using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 기본 명중률과 난수 굴림으로 명중 여부를 판정한다.
    /// 엄폐 여부는 명중률을 바꾸지 않는다. 엄폐의 이득은 회피 어빌리티가 피해를 엄폐물에 넘기는 규칙에서 나온다.
    /// </summary>
    public static class HitChanceCalculator
    {
        /// <summary>
        /// 굴린 난수로 명중 여부를 판정한다.
        /// </summary>
        /// <param name="hitChance">최종 명중률(0~1)이다.</param>
        /// <param name="roll">0 이상 1 미만의 난수이다.</param>
        /// <returns>명중이면 true이다.</returns>
        /// <remarks>
        /// 명중률이 0이면 어떤 난수에도 빗나가고, 1이면 항상 명중한다.
        /// 난수는 호출하는 쪽이 넘기므로 테스트에서 원하는 값을 지정할 수 있다.
        /// </remarks>
        public static bool IsHit(float hitChance, float roll)
        {
            return roll < Mathf.Clamp01(hitChance);
        }

        /// <summary>
        /// 지정한 확률로 성공하는지 Unity 난수로 굴린다. 명중과 엄폐 흡수가 같은 굴림을 쓴다.
        /// </summary>
        /// <param name="chance">성공 확률(0~1)이다.</param>
        /// <returns>성공이면 true이다.</returns>
        /// <remarks>
        /// <see cref="UnityEngine.Random.value"/>는 1.0을 포함하므로 확률 1을 굴림 비교만으로 다루면 아주 드물게 실패한다.
        /// 그래서 확률이 1 이상이면 굴리지 않고 성공, 0 이하면 굴리지 않고 실패로 답한다. 검사는 이 두 값으로
        /// 결과를 정하므로, 그 답이 언제나 같아야 어떤 검사도 가끔 붉어지지 않는다.
        /// </remarks>
        public static bool Roll(float chance)
        {
            if (chance >= 1f)
            {
                return true;
            }

            if (chance <= 0f)
            {
                return false;
            }

            return IsHit(chance, UnityEngine.Random.value);
        }
    }
}
