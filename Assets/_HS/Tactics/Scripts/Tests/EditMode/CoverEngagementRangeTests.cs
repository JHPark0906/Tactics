using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using HS.Framework.Tests.Support;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 엄폐 관련 행동 노드의 사거리 조건과 CoverSelection의 후보 필터를 검증한다.
    /// 노드 직접 검사는 어빌리티 경로를 실행하지 않는다. 실제 엄폐 어빌리티의 유지·해제는 CoverAbilityTests가 검증한다.
    /// 원거리에서 엄폐 분기가 실패해 접근으로 넘어가는 동작과 사거리 밖 후보 제외를 확인한다.
    /// </summary>
    public sealed class CoverEngagementRangeTests
    {
        private const float AttackRange = 8f;

        private GameObject _unitObject;
        private GameObject _coverObject;
        private GameObject _threatObject;
        private CoverSensor _sensor;
        private UnitCoverState _coverState;
        private CoverPoint _coverPoint;
        private BehaviourContext _context;

        [SetUp]
        public void SetUp()
        {
            _unitObject = new GameObject("Unit");
            _coverObject = new GameObject("Cover");
            _threatObject = new GameObject("Threat");
            _sensor = StraightLineCoverTravel.AttachSensor(_unitObject);
            _coverState = _unitObject.AddComponent<UnitCoverState>();
            _coverPoint = _coverObject.AddComponent<CoverPoint>();

            // 엄폐물은 북쪽에서 오는 위협을 막고, 위협은 북쪽 멀리에 둔다.
            _coverObject.transform.position = Vector3.zero;
            _coverObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            _unitObject.transform.position = Vector3.zero;
            _threatObject.transform.position = new Vector3(0f, 0f, 20f);
            _sensor.SetCoverPoints(new[] { _coverPoint });

            _context = new BehaviourContext();
            _context.SetValue(UnitBehaviourKeys.Target, _threatObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_unitObject);
            Object.DestroyImmediate(_coverObject);
            Object.DestroyImmediate(_threatObject);
        }

        [Test]
        public void MaintainFailsWhenTheTargetIsOutOfAttackRange()
        {
            _coverState.ClaimCover(_coverPoint);
            var node = new MaintainCoverBehaviour(_coverState, AttackRange);

            // 위협은 20미터, 사거리는 8미터라 이 자리에서는 쏠 수 없다.
            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Failure),
                "쏠 수 없는 자리에 앉아 있으면 교착이 되므로 유지를 그만둬야 한다.");
        }

        [Test]
        public void MaintainKeepsRunningWhenTheTargetIsWithinAttackRange()
        {
            _threatObject.transform.position = new Vector3(0f, 0f, 5f);
            _coverState.ClaimCover(_coverPoint);
            var node = new MaintainCoverBehaviour(_coverState, AttackRange);

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Running),
                "사거리 안이면 엄폐를 유지한 채 사격할 수 있다.");
            Assert.That(_coverState.HasCoverClaim, Is.True, "유지 중에는 자리를 계속 잡고 있어야 한다.");
        }

        [Test]
        public void MaintainReleasesTheCoverClaimWhenItGivesUp()
        {
            _coverState.ClaimCover(_coverPoint);
            Assert.That(_coverPoint.IsOccupied, Is.True);
            var node = new MaintainCoverBehaviour(_coverState, AttackRange);

            node.Tick(_context);

            Assert.That(_coverState.HasCoverClaim, Is.False, "떠나면서 예약을 쥐고 있으면 안 된다.");
            Assert.That(_coverPoint.IsOccupied, Is.False, "놓아 준 자리는 다른 유닛이 쓸 수 있어야 한다.");
        }

        [Test]
        public void MaintainReleasesTheClaimWhenTheTargetDisappears()
        {
            _coverState.ClaimCover(_coverPoint);
            _context.RemoveValue(UnitBehaviourKeys.Target);
            var node = new MaintainCoverBehaviour(_coverState, AttackRange);

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(_coverState.HasCoverClaim, Is.False, "적이 없으면 자리를 잡고 있을 이유가 없다.");
        }

        [Test]
        public void SelectDoesNotClaimCoverThatCannotShootTheTarget()
        {
            var node = new SelectCoverDestinationBehaviour(_sensor, _coverState, AttackRange);

            // 엄폐 지점은 위협에서 20미터라 그 자리에 가도 쏠 수 없다.
            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Failure),
                "그 자리에서 쏠 수 없으면 애초에 고르지 않아야 접근으로 넘어간다.");
            Assert.That(_coverState.HasCoverClaim, Is.False);
        }

        [Test]
        public void SelectClaimsCoverThatCanShootTheTarget()
        {
            _threatObject.transform.position = new Vector3(0f, 0f, 6f);
            var node = new SelectCoverDestinationBehaviour(_sensor, _coverState, AttackRange);

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Success),
                "사거리 안에서 위협을 막아 주는 엄폐는 골라야 한다.");
            Assert.That(_coverState.ClaimedCover, Is.SameAs(_coverPoint));
        }

        [Test]
        public void WholeCoverBranchFailsAtLongRangeSoApproachCanRun()
        {
            // 자식을 보는 자리는 트리를 지어야 확인된다. 자식은 노드가 아니라 트리가 들고 있다.
            var mover = new FakeCharacterMover();
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            var sequence = tree.AddChild(root, new SequenceBehaviour());
            tree.AddChild(sequence, new SelectCoverDestinationBehaviour(_sensor, _coverState, AttackRange));
            tree.AddChild(sequence, new MoveToPositionBehaviour(mover, UnitBehaviourKeys.Destination));
            tree.AddChild(root, new MaintainCoverBehaviour(_coverState, AttackRange));

            Assert.That(tree.Tick(_context), Is.EqualTo(BehaviourStatus.Failure),
                "사거리 밖에서는 엄폐 분기 전체가 실패해야 접근이 실행된다.");
        }

        [Test]
        public void CoveredUnitStopsHoldingWhenTheTargetRetreatsOutOfRange()
        {
            _threatObject.transform.position = new Vector3(0f, 0f, 5f);
            _coverState.ClaimCover(_coverPoint);
            var node = new MaintainCoverBehaviour(_coverState, AttackRange);
            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Running));

            _threatObject.transform.position = new Vector3(0f, 0f, 25f);

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Failure),
                "적이 물러나 사거리를 벗어나면 자리를 버리고 쫓아가야 한다.");
            Assert.That(_coverState.HasCoverClaim, Is.False);
        }

        [Test]
        public void UnitWithoutDefinitionDoesNotOccupyCover()
        {
            _threatObject.transform.position = new Vector3(0f, 0f, 6f);
            // 유닛 정의가 없으면 사거리를 알 수 없어 0이 넘어온다.
            var select = new SelectCoverDestinationBehaviour(_sensor, _coverState, 0f);
            var maintain = new MaintainCoverBehaviour(_coverState, 0f);

            Assert.That(select.Tick(_context), Is.EqualTo(BehaviourStatus.Failure),
                "쏠 수 없는 유닛이 엄폐에 앉아 자리를 막지 않아야 한다.");
            Assert.That(_coverState.HasCoverClaim, Is.False);
            Assert.That(maintain.Tick(_context), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void SelectionSkipsCandidatesTooFarFromTheThreat()
        {
            var threat = new Vector3(0f, 0f, 20f);
            var candidates = new List<CoverCandidate>
            {
                // 유닛에 가깝지만 위협에서 멀어 그 자리에서는 쏠 수 없다.
                new CoverCandidate(Vector3.zero, false),
                // 조금 더 멀지만 위협을 사거리 안에 둔다.
                new CoverCandidate(new Vector3(0f, 0f, 14f), false)
            };

            Assert.That(
                CoverSelection.SelectBestIndex(candidates, Vector3.zero, threat, 20f),
                Is.EqualTo(0),
                "사거리 제한이 없으면 가장 가까운 후보를 고른다.");

            Assert.That(
                CoverSelection.SelectBestIndex(candidates, Vector3.zero, threat, 20f, AttackRange),
                Is.EqualTo(1),
                "사거리 제한을 주면 그 자리에서 쏠 수 있는 후보만 고른다.");
        }

        [Test]
        public void SelectionReturnsNoCoverWhenEveryCandidateIsOutOfFiringRange()
        {
            var threat = new Vector3(0f, 0f, 40f);
            var candidates = new List<CoverCandidate>
            {
                new CoverCandidate(Vector3.zero, false)
            };

            Assert.That(
                CoverSelection.SelectBestIndex(candidates, Vector3.zero, threat, 20f, AttackRange),
                Is.EqualTo(CoverSelection.NoCoverIndex));
        }

        /// <summary>이동 요청을 받아들이는 테스트용 이동 구성요소이다.</summary>
        private sealed class FakeCharacterMover : ICharacterMover
        {
            public bool HasReachedDestination { get; set; }

            public bool MoveTo(Vector3 destination)
            {
                return true;
            }

            public void Stop()
            {
            }
        }
    }
}
