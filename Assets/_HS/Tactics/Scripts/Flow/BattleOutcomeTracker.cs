using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using UnityEngine;

namespace HS.Tactics.Flow
{
    /// <summary>
    /// 진영별 생존 유닛 수를 추적해 전멸을 감지하고 승패를 판정하는 순수 클래스이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity 수명주기에 의존하지 않고 등록·해제·사망 통지라는 입력만으로 동작하므로,
    /// EditMode 테스트에서 이벤트 입력만으로 모든 판정 경로를 검증할 수 있다.
    /// 이벤트 버스 구독과 결과 발행은 <see cref="BattleOutcomeService"/>가 맡는다.
    /// </para>
    /// <para>
    /// 판정 규칙은 적 전멸이면 승리, 아군 전멸이면 패배이다. 결과는 한 번 확정되면 바뀌지 않는다. 다음 전투는 새 씬에서 새 판정기로 시작한다.
    /// </para>
    /// </remarks>
    public sealed class BattleOutcomeTracker
    {
        /// <summary>등록된 유닛과 그 유닛이 속한 편을 보관한다.</summary>
        private readonly Dictionary<GameObject, TeamRelation> _trackedUnits = new();

        /// <summary>플레이어가 조작하는 진영이다.</summary>
        private readonly TeamId _playerTeamId;

        /// <summary>등록 시점의 피아 판정에 사용하는 규칙이다.</summary>
        private readonly ITeamRelationPolicy _relationPolicy;

        /// <summary>아군이 한 번이라도 등록되었는지 여부이다.</summary>
        private bool _hasFriendlyJoined;

        /// <summary>적이 한 번이라도 등록되었는지 여부이다.</summary>
        private bool _hasHostileJoined;

        /// <summary>승패 판정기를 생성한다.</summary>
        /// <param name="playerTeamId">플레이어가 조작하는 진영이다.</param>
        /// <param name="relationPolicy">피아 판정에 사용할 규칙이며, null이면 프레임워크 기본 규칙을 사용한다.</param>
        public BattleOutcomeTracker(TeamId playerTeamId, ITeamRelationPolicy relationPolicy = null)
        {
            _playerTeamId = playerTeamId;
            _relationPolicy = relationPolicy ?? DefaultTeamRelationPolicy.Instance;
        }

        /// <summary>확정된 전투 결과이며, 아직 확정 전이면 <see cref="BattleOutcome.Undecided"/>이다.</summary>
        public BattleOutcome Outcome { get; private set; }

        /// <summary>전투 결과가 확정되었는지 여부이다.</summary>
        public bool IsDecided => Outcome != BattleOutcome.Undecided;

        /// <summary>살아 있는 아군 유닛 수이다.</summary>
        public int FriendlyAliveCount { get; private set; }

        /// <summary>살아 있는 적 유닛 수이다.</summary>
        public int HostileAliveCount { get; private set; }

        /// <summary>
        /// 아군과 적이 모두 한 번 이상 등록되어 판정을 시작할 수 있는 상태인지 여부이다.
        /// 배치가 끝나기 전 한쪽만 스폰된 순간에 전멸로 오판하지 않도록 하는 조건이다.
        /// </summary>
        public bool HasBattleStarted { get; private set; }

        /// <summary>현재 집계에 포함된 유닛 수이며 중립 유닛은 포함하지 않는다.</summary>
        public int TrackedUnitCount => _trackedUnits.Count;

        /// <summary>
        /// 대상을 지정한 진영으로 집계에 등록한다.
        /// 플레이어 진영과 적대하지도 협력하지도 않는 중립 대상은 승패에 영향을 주지 않으므로 등록하지 않는다.
        /// </summary>
        /// <param name="unitObject">등록할 대상이다.</param>
        /// <param name="teamId">대상이 속한 진영이다.</param>
        /// <returns>집계에 새로 등록했으면 true이다.</returns>
        public bool Register(GameObject unitObject, TeamId teamId)
        {
            return Register(unitObject, _relationPolicy.GetRelation(teamId, _playerTeamId));
        }

        /// <summary>
        /// 피아 관계를 직접 지정해 대상을 집계에 등록한다.
        /// 유닛이 자신의 <see cref="TeamMember"/>에 주입된 규칙으로 이미 관계를 판정한 경우에 사용한다.
        /// </summary>
        /// <param name="unitObject">등록할 대상이다.</param>
        /// <param name="relationToPlayer">대상이 플레이어 진영을 어떤 관계로 보는지이다.</param>
        /// <returns>집계에 새로 등록했으면 true이다.</returns>
        public bool Register(GameObject unitObject, TeamRelation relationToPlayer)
        {
            if (unitObject == null || relationToPlayer == TeamRelation.Neutral || _trackedUnits.ContainsKey(unitObject))
            {
                return false;
            }

            _trackedUnits.Add(unitObject, relationToPlayer);
            if (relationToPlayer == TeamRelation.Friendly)
            {
                FriendlyAliveCount++;
                _hasFriendlyJoined = true;
            }
            else
            {
                HostileAliveCount++;
                _hasHostileJoined = true;
            }

            HasBattleStarted = _hasFriendlyJoined && _hasHostileJoined;
            return true;
        }

        /// <summary>
        /// 대상을 집계에서 제거한다. 사망이 아닌 회수 경로이므로 결과를 판정하지 않는다.
        /// </summary>
        /// <param name="unitObject">제거할 대상이다.</param>
        /// <returns>실제로 제거했으면 true이다.</returns>
        public bool Unregister(GameObject unitObject)
        {
            return RemoveTrackedUnit(unitObject);
        }

        /// <summary>
        /// 대상의 사망을 통지하고 그 결과로 전멸이 발생했는지 판정한다.
        /// 집계에 없는 대상이면 아무 일도 하지 않으므로, 전투와 무관한 파괴물의 사망 이벤트를 그대로 넘겨도 안전하다.
        /// </summary>
        /// <param name="unitObject">사망한 대상이다.</param>
        /// <returns>집계에 있던 대상이어서 실제로 반영했으면 true이다.</returns>
        public bool NotifyDeath(GameObject unitObject)
        {
            if (!RemoveTrackedUnit(unitObject))
            {
                return false;
            }

            EvaluateOutcome();
            return true;
        }

        /// <summary>집계에서 대상을 빼고 해당 편의 생존 수를 줄인다.</summary>
        /// <param name="unitObject">제거할 대상이다.</param>
        /// <returns>실제로 제거했으면 true이다.</returns>
        private bool RemoveTrackedUnit(GameObject unitObject)
        {
            if (unitObject == null || !_trackedUnits.TryGetValue(unitObject, out var relation))
            {
                return false;
            }

            _trackedUnits.Remove(unitObject);
            if (relation == TeamRelation.Friendly)
            {
                FriendlyAliveCount--;
            }
            else
            {
                HostileAliveCount--;
            }

            return true;
        }

        /// <summary>
        /// 현재 생존 수로 승패를 판정한다.
        /// 양측이 동시에 비는 경우에는 승리로 판정하는데, 기획에 무승부가 없고
        /// 마지막 교전에서 서로 쓰러지는 결말을 패배로 처리할 근거가 없기 때문이다.
        /// </summary>
        private void EvaluateOutcome()
        {
            if (IsDecided || !HasBattleStarted)
            {
                return;
            }

            if (HostileAliveCount <= 0)
            {
                Outcome = BattleOutcome.Victory;
            }
            else if (FriendlyAliveCount <= 0)
            {
                Outcome = BattleOutcome.Defeat;
            }
        }
    }
}
