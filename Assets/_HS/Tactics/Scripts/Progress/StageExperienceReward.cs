using HS.Tactics.Units;

namespace HS.Tactics.Progress
{
    /// <summary>
    /// 스테이지가 준 경험치를 파티가 나누는 규칙이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>적게 데려간 쪽이 더 빨리 자란다.</b> 스테이지가 주는 몫은 인원과 무관하게 정해져 있고
    /// 그것을 배치한 인원수로 나누므로, 다섯을 데려가면 한 기당 5분의 1이고 하나만 데려가면 전부이다.
    /// 수를 늘려 밀어붙이는 선택과 소수를 키우는 선택이 서로 값을 치르게 하려는 것이다.
    /// </para>
    /// <para>
    /// <b>죽은 유닛도 인원수에 든다.</b> 세는 것은 살아남은 수가 아니라 <b>데려간 수</b>이다.
    /// 살아남은 수로 세면 일부러 죽게 두어 몫을 키우는 길이 생긴다.
    /// </para>
    /// <para>
    /// <b>나머지는 버린다.</b> 셋이 100을 나누면 33씩이고 남는 1은 사라진다. 누구에게 줄지 정하려면
    /// 순서를 정해야 하는데, 그 순서는 배치한 차례나 이름 같은 <b>플레이와 상관없는 것</b>이 되어
    /// 사용자가 이유를 알 수 없는 차이를 만든다.
    /// </para>
    /// <para>
    /// <b>인원수는 인자로 받는다.</b> 여기서 파티 상한을 읽어 쓰면 상한이 곧 인원수인 것처럼 되어,
    /// 셋만 데려간 판에서도 다섯으로 나눈다. 상한은 <b>넘지 않았는지 보는 데만</b> 쓴다.
    /// </para>
    /// </remarks>
    public static class StageExperienceReward
    {
        /// <summary>
        /// 스테이지가 준 경험치를 한 기가 받을 몫으로 나눈다.
        /// </summary>
        /// <param name="totalExperience">스테이지가 준 경험치이며 0 이하이면 나눌 것이 없다.</param>
        /// <param name="partySize">그 스테이지에 배치한 유닛 수이며 죽은 유닛도 포함한다.</param>
        /// <returns>한 기가 받을 경험치이며 나머지는 버린다.</returns>
        public static int ResolveShare(int totalExperience, int partySize)
        {
            if (totalExperience <= 0 || partySize <= 0)
            {
                return 0;
            }

            return totalExperience / partySize;
        }

        /// <summary>
        /// 배치한 인원수가 파티 규칙 안에 있는지 본다.
        /// </summary>
        /// <remarks>
        /// 나누는 데는 쓰지 않는다. 규칙을 넘긴 인원수는 배치 쪽에서 이미 막혀야 하는 것이므로,
        /// 여기서 걸린다면 <b>배치 상한이 규칙과 어긋났다는 뜻</b>이다.
        /// </remarks>
        /// <param name="partySize">확인할 인원수이다.</param>
        /// <returns>1 이상이고 파티 상한 이하이면 true이다.</returns>
        public static bool IsPartySizeWithinRules(int partySize)
            => partySize >= 1 && partySize <= PartyRules.MaxPartySize;
    }
}
