using System.Text.RegularExpressions;
using HS.Framework.Character;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Behaviour;
using HS.Tactics.Lane;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>레인이 유닛의 전진 방향을 행동 트리 컨텍스트에 실제로 채워 넣는지 검증한다.</summary>
    /// <remarks>
    /// <see cref="RefreshAdvanceDirectionService.RefreshAdvanceDirection"/>은 문맥을 인자로 받아
    /// 트리 없이도 직접 부를 수 있으므로, 여기서는 그 메서드를 직접 불러 계산 자체를 검증한다.
    /// 서비스가 자기 간격으로 그것을 부르는지, 조립이 그것을 부르지 않는지는
    /// <see cref="LaneAdvanceDirectionSingleWriterTests"/>가 따로 검증한다.
    /// </remarks>
    public sealed class RefreshAdvanceDirectionServiceTests
    {
        private const float Tolerance = 1e-4f;

        private GameObject _laneObject;
        private GameObject _unitObject;
        private UnitDefinition _definition;
        private TacticalUnit _unit;
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
            _service = null;
        }

        [Test]
        public void TheForwardTeamIsSentTowardTheEndOfTheLane()
        {
            CreateLane(Vector3.zero, new Vector3(0f, 0f, 20f), new TeamId(1));
            CreateUnit(Vector3.zero, new TeamId(1));

            _unit.InitializeUnit();
            _service.RefreshAdvanceDirection(_unit.BehaviourContext);

            AssertAdvanceDirection(Vector3.forward);
            Assert.That(_service.HasDirection, Is.True);
        }

        [Test]
        public void TheOpposingTeamIsSentTowardTheOppositeEnd()
        {
            CreateLane(Vector3.zero, new Vector3(0f, 0f, 20f), new TeamId(1));
            CreateUnit(new Vector3(0f, 0f, 20f), new TeamId(2));

            _unit.InitializeUnit();
            _service.RefreshAdvanceDirection(_unit.BehaviourContext);

            AssertAdvanceDirection(Vector3.back);
        }

        [Test]
        public void AUnitOffTheAxisIsSentDiagonallyTowardTheGoal()
        {
            CreateLane(Vector3.zero, new Vector3(0f, 0f, 20f), new TeamId(1));
            CreateUnit(new Vector3(6f, 0f, 12f), new TeamId(1));

            _unit.InitializeUnit();
            _service.RefreshAdvanceDirection(_unit.BehaviourContext);

            AssertAdvanceDirection(new Vector3(-6f, 0f, 8f).normalized);
        }

        [Test]
        public void ReachingTheEndOfTheLaneClearsTheAdvanceDirection()
        {
            CreateLane(Vector3.zero, new Vector3(0f, 0f, 20f), new TeamId(1));
            CreateUnit(new Vector3(0f, 0f, 19.6f), new TeamId(1));

            _unit.InitializeUnit();
            _service.RefreshAdvanceDirection(_unit.BehaviourContext);

            Assert.That(TryGetAdvanceDirection(out var direction), Is.True);
            Assert.That(direction, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void MovingTheUnitUpdatesTheDirectionOnTheNextRefresh()
        {
            CreateLane(Vector3.zero, new Vector3(0f, 0f, 20f), new TeamId(1));
            CreateUnit(new Vector3(6f, 0f, 12f), new TeamId(1));
            _unit.InitializeUnit();
            _service.RefreshAdvanceDirection(_unit.BehaviourContext);

            _unitObject.transform.position = Vector3.zero;
            _service.RefreshAdvanceDirection(_unit.BehaviourContext);

            AssertAdvanceDirection(Vector3.forward);
        }

        [Test]
        public void AnInjectedTargetSourceReplacesTheLane()
        {
            CreateUnit(Vector3.zero, new TeamId(1), new StubTargetSource(new Vector3(10f, 0f, 0f)));

            _unit.InitializeUnit();
            _service.RefreshAdvanceDirection(_unit.BehaviourContext);

            AssertAdvanceDirection(Vector3.right);
        }

        [Test]
        public void AUnitWithoutATeamKeepsTheAdvanceDirectionEmpty()
        {
            CreateLane(Vector3.zero, new Vector3(0f, 0f, 20f), new TeamId(1));
            CreateUnit(Vector3.zero, TeamId.None);

            _unit.InitializeUnit();
            _service.RefreshAdvanceDirection(_unit.BehaviourContext);

            Assert.That(TryGetAdvanceDirection(out _), Is.False);
            Assert.That(_service.HasDirection, Is.False);
        }

        [Test]
        public void AnUnconfiguredLaneSuppliesNoTarget()
        {
            _laneObject = new GameObject("Lane");
            var lane = _laneObject.AddComponent<BattleLane>();
            lane.InitializeLane();

            Assert.That(lane.IsConfigured, Is.False);
            Assert.That(lane.TryGetAdvanceTarget(new TeamId(1), Vector3.zero, out _), Is.False);
        }

        [Test]
        public void WithoutALaneTheDirectionIsLeftToTheNodeFallback()
        {
            CreateUnit(Vector3.zero, new TeamId(1));
            LogAssert.Expect(LogType.Warning, new Regex("RefreshAdvanceDirectionService"));

            _unit.InitializeUnit();
            _service.RefreshAdvanceDirection(_unit.BehaviourContext);

            Assert.That(TryGetAdvanceDirection(out _), Is.False);
            Assert.That(_service.HasDirection, Is.False);
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

        /// <summary>전진 방향 서비스와 이동 구성요소를 갖춘 유닛을 만든다.</summary>
        /// <param name="position">유닛을 세울 좌표이다.</param>
        /// <param name="team">유닛에 지정할 진영이다.</param>
        /// <param name="targetSource">서비스에 주입할 목표 공급 원본이며, 비우면 씬의 레인을 쓴다.</param>
        private void CreateUnit(Vector3 position, TeamId team, IAdvanceTargetSource targetSource = null)
        {
            _unitObject = new GameObject("Unit");
            _unitObject.transform.position = position;
            _unit = _unitObject.AddComponent<TacticalUnit>();
            _unitObject.AddComponent<StubMover>();
            var teamMember = _unitObject.GetComponent<TeamMember>();
            teamMember.SetTeam(team);
            _definition = UnitDefinition.CreateRuntime("소총병", 100);
            _unit.SetDefinition(_definition);
            _service = new RefreshAdvanceDirectionService(
                _unitObject.transform, teamMember, LaneAdvanceCalculator.DefaultArrivalDistance, 0f,
                targetSource);
        }

        /// <summary>컨텍스트에 기록된 전진 방향을 읽는다.</summary>
        private bool TryGetAdvanceDirection(out Vector3 direction)
        {
            return _unit.BehaviourContext.TryGetValue(UnitBehaviourKeys.AdvanceDirection, out direction);
        }

        /// <summary>컨텍스트에 기대한 전진 방향이 기록되었는지 확인한다.</summary>
        private void AssertAdvanceDirection(Vector3 expected)
        {
            Assert.That(TryGetAdvanceDirection(out var direction), Is.True, "전진 방향이 기록되지 않았다.");
            Assert.That(
                Vector3.Distance(direction, expected),
                Is.LessThan(Tolerance),
                $"기대한 방향은 {expected}이고 실제 방향은 {direction}이다.");
        }

        /// <summary>레인 없이 고정된 목표만 돌려주는 테스트용 목표 공급 원본이다.</summary>
        private sealed class StubTargetSource : IAdvanceTargetSource
        {
            private readonly Vector3 _target;

            public StubTargetSource(Vector3 target)
            {
                _target = target;
            }

            public bool TryGetAdvanceTarget(TeamId team, Vector3 position, out Vector3 target)
            {
                target = _target;
                return true;
            }
        }

        /// <summary>이동 계약만 만족시키는 테스트용 구성요소이다.</summary>
        private sealed class StubMover : MonoBehaviour, ICharacterComponent, ICharacterMover
        {
            public bool HasReachedDestination => true;

            public void Initialize(CharacterBase characterBase)
            {
            }

            public bool MoveTo(Vector3 destination) => true;

            public void Stop()
            {
            }
        }
    }
}
