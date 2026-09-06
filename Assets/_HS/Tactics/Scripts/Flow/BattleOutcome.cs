using HS.Framework.Gameplay.Teams;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Flow
{
    /// <summary>
    /// 한 전투의 판정 결과이다.
    /// </summary>
    /// <remarks>
    /// 기획상 무승부와 제한 시간은 존재하지 않는다. 일자형 맵에서 적을 발견할 때까지 무조건 전진하므로
    /// 양측이 접촉하지 못한 채 멈추는 교착이 성립하지 않고, 따라서 승패 외의 결말을 두지 않는다.
    /// </remarks>
    public enum BattleOutcome
    {
        /// <summary>아직 어느 쪽도 전멸하지 않아 결과가 확정되지 않았다.</summary>
        Undecided = 0,

        /// <summary>적 진영이 전멸해 플레이어가 승리했다.</summary>
        Victory = 1,

        /// <summary>아군 진영이 전멸해 플레이어가 패배했다.</summary>
        Defeat = 2
    }

    /// <summary>
    /// 전투 결과가 확정된 사실을 알리는 게임 레이어 이벤트이다.
    /// </summary>
    /// <remarks>
    /// 결과 표시 창과 스테이지 진행 흐름이 이 이벤트를 구독한다.
    /// 한 전투에서 정확히 한 번만 발행되며, 결과는 뒤집히지 않는다.
    /// </remarks>
    public readonly struct BattleOutcomeDecidedEvent
    {
        /// <summary>확정된 전투 결과이며 <see cref="BattleOutcome.Undecided"/>로는 발행되지 않는다.</summary>
        public BattleOutcome Outcome { get; }

        /// <summary>결과 확정 시점에 살아 있는 아군 유닛 수이다.</summary>
        public int FriendlyAliveCount { get; }

        /// <summary>결과 확정 시점에 살아 있는 적 유닛 수이다.</summary>
        public int HostileAliveCount { get; }

        /// <summary>전투 결과 확정 이벤트를 생성한다.</summary>
        /// <param name="outcome">확정된 전투 결과이다.</param>
        /// <param name="friendlyAliveCount">살아 있는 아군 유닛 수이다.</param>
        /// <param name="hostileAliveCount">살아 있는 적 유닛 수이다.</param>
        public BattleOutcomeDecidedEvent(BattleOutcome outcome, int friendlyAliveCount, int hostileAliveCount)
        {
            Outcome = outcome;
            FriendlyAliveCount = friendlyAliveCount;
            HostileAliveCount = hostileAliveCount;
        }
    }

    /// <summary>
    /// 전투에 참여하는 유닛을 승패 판정 집계에 넣고 빼는 경계 계약이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>배치 코드와의 경계.</b> 유닛을 스폰하는 코드가 이 계약을 사용한다.
    /// 권장 경로는 유닛 프리팹에 <see cref="BattleUnitRegistrant"/>를 붙여 유닛이 스스로 등록하는 방식이다.
    /// 그러면 배치 코드는 스폰 후 VContainer의 <c>IObjectResolver.InjectGameObject</c>로 계층에 주입하기만 하면 되고,
    /// 등록을 빠뜨릴 수 없으며 파괴·비활성화 시 해제도 자동으로 이루어진다.
    /// 스폰 시점을 배치 쪽이 완전히 통제하고 싶거나 풀링으로 프리팹 구성을 바꾸기 어려우면
    /// 배치 코드가 이 계약을 주입받아 <see cref="RegisterUnit(TacticalUnit)"/>를 직접 호출해도 된다.
    /// </para>
    /// <para>
    /// <b>진영 판정.</b> 아군과 적의 구분은 등록 시점에 한 번 계산해 보관한다.
    /// 따라서 등록 이후 유닛의 진영을 바꿔도 집계는 따라가지 않으며, 진영을 바꾸려면 해제 후 다시 등록해야 한다.
    /// 어느 쪽과도 적대하지 않는 중립 유닛은 승패 집계에 포함되지 않는다.
    /// </para>
    /// <para>
    /// <b>전투 시작 조건.</b> 아군과 적이 각각 한 번 이상 등록되기 전에는 결과를 판정하지 않는다.
    /// 배치가 진행되는 동안 한쪽만 스폰된 순간이 있어도 그때 전멸로 오판하지 않게 하기 위한 것이다.
    /// </para>
    /// </remarks>
    public interface IBattleUnitRegistry
    {
        /// <summary>
        /// 전술 유닛을 승패 집계에 등록한다. 진영은 유닛의 <see cref="TeamMember"/>에서 읽는다.
        /// </summary>
        /// <param name="unit">등록할 전술 유닛이다.</param>
        /// <returns>새로 등록했으면 true이며, null이거나 이미 등록되어 있으면 false이다.</returns>
        bool RegisterUnit(TacticalUnit unit);

        /// <summary>
        /// 임의의 GameObject를 지정한 진영으로 승패 집계에 등록한다.
        /// <see cref="TacticalUnit"/>이 아닌 파괴 가능한 목표물을 전멸 판정에 포함해야 할 때 사용한다.
        /// </summary>
        /// <param name="unitObject">등록할 대상이다.</param>
        /// <param name="teamId">대상이 속한 진영이다.</param>
        /// <returns>새로 등록했으면 true이며, null이거나 이미 등록되어 있으면 false이다.</returns>
        bool RegisterUnit(GameObject unitObject, TeamId teamId);

        /// <summary>
        /// 대상을 승패 집계에서 제거한다.
        /// 사망이 아닌 회수·파괴 경로이므로 이 호출만으로는 결과를 판정하지 않는다.
        /// </summary>
        /// <param name="unitObject">제거할 대상이다.</param>
        /// <returns>실제로 제거했으면 true이다.</returns>
        bool UnregisterUnit(GameObject unitObject);
    }
}
