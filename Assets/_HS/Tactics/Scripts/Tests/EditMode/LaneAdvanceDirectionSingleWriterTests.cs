using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Behaviour;
using HS.Tactics.Lane;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 전진 방향은 행동 트리의 RefreshAdvanceDirectionService가 갱신한다.
    /// 서비스는 MonoBehaviour나 유닛 조립 구성요소가 아니며 트리의 시간 공급으로 실행된다.
    /// 진영이 없으면 키를 유지하고 진영이 정해지면 다음 갱신이 방향을 쓰는지, 자식 실행 전에 갱신하는지 검증한다.
    /// </summary>
    public sealed class LaneAdvanceDirectionSingleWriterTests
    {
        private const float Tolerance = 1e-4f;

        private GameObject _laneObject;
        private GameObject _unitObject;
        private UnitDefinition _definition;
        private TacticalUnit _unit;
        private TeamMember _teamMember;
        private RefreshAdvanceDirectionService _service;

        [TearDown]
        public void TearDown()
        {
            if (_unitObject != null)
            {
                Object.DestroyImmediate(_unitObject);
                _unitObject = null;
            }

            if (_laneObject != null)
            {
                Object.DestroyImmediate(_laneObject);
                _laneObject = null;
            }

            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
                _definition = null;
            }

            _unit = null;
            _teamMember = null;
            _service = null;
        }

        [Test]
        public void ATeamTakenFromTheDefinitionIsSeenByTheFirstRefresh()
        {
            CreateLane(Vector3.zero, new Vector3(0f, 0f, 20f), new TeamId(1));
            CreateUnit(Vector3.zero, TeamId.None, new TeamId(1));

            _unit.InitializeUnit();
            Assert.That(_unit.Team.TeamId, Is.EqualTo(new TeamId(1)), "조립이 정의의 기본 진영을 넣어야 한다.");
            _service.RefreshAdvanceDirection(_unit.BehaviourContext);

            AssertDirection(Vector3.forward);
        }

        [Test]
        public void WithoutATeamTheKeyIsLeftAloneUntilTheTeamArrives()
        {
            CreateLane(Vector3.zero, new Vector3(0f, 0f, 20f), new TeamId(1));
            CreateUnit(Vector3.zero, TeamId.None, TeamId.None);
            _unit.InitializeUnit();

            _service.RefreshAdvanceDirection(_unit.BehaviourContext);
            Assert.That(_service.HasDirection, Is.False, "진영이 없으면 방향을 정할 수 없어 키를 건드리지 않는다.");
            Assert.That(_unit.BehaviourContext.TryGetValue(UnitBehaviourKeys.AdvanceDirection, out Vector3 _), Is.False);

            _teamMember.SetTeam(new TeamId(1));
            _service.RefreshAdvanceDirection(_unit.BehaviourContext);

            AssertDirection(Vector3.forward);
        }

        [Test]
        public void TheTreeServiceWritesTheDirectionBeforeItsChildRuns()
        {
            CreateLane(Vector3.zero, new Vector3(0f, 0f, 20f), new TeamId(1));
            CreateUnit(Vector3.zero, new TeamId(1), new TeamId(1));
            _unit.InitializeUnit();
            var tree = new BehaviourTreeInstance();
            var service = tree.SetRoot(_service);
            var childSawDirection = false;
            tree.AddChild(service, new ActionBehaviour(context =>
            {
                childSawDirection = context.TryGetValue(UnitBehaviourKeys.AdvanceDirection, out Vector3 _);
                return BehaviourStatus.Success;
            }));

            var status = tree.Tick(_unit.BehaviourContext);

            Assert.That(status, Is.EqualTo(BehaviourStatus.Success));
            Assert.That(childSawDirection, Is.True, "서비스가 자식보다 먼저 방향을 써야 전진 노드가 첫 틱에 읽는다.");
            AssertDirection(Vector3.forward);
        }

        /// <summary>시작점과 종점을 자식으로 둔 레인을 만들고 씬의 대표 레인으로 등록한다.</summary>
        private void CreateLane(Vector3 start, Vector3 end, TeamId forwardTeam)
        {
            _laneObject = new GameObject("Lane");
            var lane = _laneObject.AddComponent<BattleLane>();
            lane.SetLane(CreatePoint("Start", start), CreatePoint("End", end), forwardTeam);
            lane.InitializeLane();
        }

        /// <summary>레인의 끝점 역할을 하는 자식 Transform을 만든다.</summary>
        private Transform CreatePoint(string pointName, Vector3 position)
        {
            var point = new GameObject(pointName).transform;
            point.SetParent(_laneObject.transform);
            point.position = position;
            return point;
        }

        /// <summary>전진 방향 서비스를 갖춘 유닛을 만든다. 진영은 구성요소에 직접 넣는 것과 정의의 기본값을 따로 받는다.</summary>
        /// <remarks>
        /// 서비스는 간격 0으로 만든다. <see cref="TheTreeServiceWritesTheDirectionBeforeItsChildRuns"/>가 실제
        /// 트리로 부를 때 매번 실행되게 하기 위해서이며, 나머지 검사는 <see cref="RefreshAdvanceDirectionService.RefreshAdvanceDirection"/>을
        /// 직접 불러 간격과 무관하게 계산한다.
        /// </remarks>
        private void CreateUnit(Vector3 position, TeamId team, TeamId definitionTeam)
        {
            _unitObject = new GameObject("Unit");
            _unitObject.transform.position = position;
            _unit = _unitObject.AddComponent<TacticalUnit>();
            _teamMember = _unitObject.GetComponent<TeamMember>();
            _teamMember.SetTeam(team);
            _definition = UnitDefinition.CreateRuntime("소총병", 100, definitionTeam);
            _unit.SetDefinition(_definition);
            _service = new RefreshAdvanceDirectionService(
                _unitObject.transform, _teamMember, LaneAdvanceCalculator.DefaultArrivalDistance, 0f,
                timeProvider: () => 0f);
        }

        /// <summary>컨텍스트에 기대한 전진 방향이 기록되었는지 확인한다.</summary>
        private void AssertDirection(Vector3 expected)
        {
            Assert.That(
                _unit.BehaviourContext.TryGetValue(UnitBehaviourKeys.AdvanceDirection, out Vector3 direction),
                Is.True,
                "전진 방향이 기록되지 않았다.");
            Assert.That(
                Vector3.Distance(direction, expected),
                Is.LessThan(Tolerance),
                $"기대한 방향은 {expected}이고 실제 방향은 {direction}이다.");
        }
    }
}
