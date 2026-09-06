using System.Collections.Generic;
using HS.Framework.Gameplay.Health;
using HS.Framework.Tests.Support;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 죽은 유닛이 전장에서 물러나는지, 그리고 물러나면서 붙잡고 있던 것들을 놓는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>죽는 것과 멈추는 것은 저절로 이어지지 않는다.</b> 체력이 0이 되어도 행동 트리가 계속 돌면
    /// 죽은 유닛이 움직이고 사격한다. 대상으로 잡히지 않을 뿐이라 <b>시체가 산 유닛을 쏴 죽인다.</b>
    /// </para>
    /// <para>
    /// <b>"행동 트리만 멈추면 되네"로 고치면 둘이 남는다.</b> 죽은 유닛이 잡고 있던 엄폐 지점이
    /// 전투가 끝날 때까지 잠기고, 죽은 콜라이더가 탐지 후보 자리를 계속 차지한다.
    /// 둘 다 <c>OnDisable</c>에 걸려 있어 오브젝트를 비활성화해야 함께 풀린다.
    /// 그래서 <b>예약이 풀리는지</b>를 이 파일의 핵심으로 둔다 — 짧아지는 방향의 수정이 그것을 먼저 죽인다.
    /// </para>
    /// </remarks>
    public sealed class DefeatedUnitRetirementTests
    {
        private readonly List<GameObject> _createdObjects = new();

        private TestPublisher<DamageAppliedEvent> _damagePublisher;
        private TestMessageChannel<DeathEvent> _deathChannel;
        private TestUnitAttributes _attributes;
        private GameObject _unitObject;
        private HealthAttributeComponent _health;
        private UnitCoverState _coverState;
        private CoverPoint _coverPoint;

        [SetUp]
        public void SetUp()
        {
            _damagePublisher = new TestPublisher<DamageAppliedEvent>();
            _deathChannel = new TestMessageChannel<DeathEvent>();
            _attributes = new TestUnitAttributes();

            _unitObject = CreateObject("Unit");
            _health = _attributes.AttachHealth(_unitObject);
            _health.InjectMessagePipePublishers(_damagePublisher, _deathChannel);
            _coverState = _unitObject.AddComponent<UnitCoverState>();
            var retirement = _unitObject.AddComponent<DefeatedUnitRetirement>();
            retirement.InjectMessagePipeDependencies(_deathChannel);

            // 예약만 잡고 자리에 서지는 않는다. 유닛과 같은 자리에 두면 엄폐 흡수가 확률로 마지막 타격을 대신 받아
            // 유닛이 죽지 않고, 검사가 실행마다 흔들린다.
            var coverObject = CreateObject("Cover");
            coverObject.transform.position = new Vector3(10f, 0f, 0f);
            _coverPoint = coverObject.AddComponent<CoverPoint>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
            _attributes.Dispose();
        }

        [Test]
        public void ADefeatedUnitLeavesTheField()
        {
            Kill();

            Assert.That(
                _unitObject.activeSelf,
                Is.False,
                "죽은 유닛이 활성으로 남으면 행동 트리가 계속 돌아 시체가 싸운다.");
        }

        /// <remarks>
        /// <b>예약은 물러나는 쪽이 명시적으로 놓는다.</b> 비활성화가 <c>OnDisable</c>을 태워 예약이 알아서
        /// 풀리기를 기대하면, 수명주기가 돌지 않는 자리에서는 그 기대가 성립하지 않는다.
        /// 명시적으로 놓으므로 이 검사는 <b>수명주기가 도는지와 무관하게</b> 성립한다.
        /// </remarks>
        [Test]
        public void ADefeatedUnitReleasesTheCoverItWasHolding()
        {
            _coverState.ClaimCover(_coverPoint);
            Assert.That(_coverPoint.IsOccupied, Is.True);

            Kill();

            Assert.That(
                _coverPoint.IsOccupied,
                Is.False,
                "죽은 유닛이 예약을 쥐고 있으면 그 엄폐물은 전투가 끝날 때까지 아무도 못 쓴다.");
            Assert.That(_coverState.HasCoverClaim, Is.False);
        }

        [Test]
        public void AnotherUnitCanTakeTheCoverAfterTheHolderDies()
        {
            _coverState.ClaimCover(_coverPoint);
            var survivorObject = CreateObject("Survivor");
            var survivorCover = survivorObject.AddComponent<UnitCoverState>();

            Kill();

            Assert.That(
                survivorCover.ClaimCover(_coverPoint),
                Is.True,
                "놓인 자리를 산 유닛이 실제로 쓸 수 있어야 한다. 점유 표시만 지우고 못 쓰면 고친 것이 아니다.");
        }

        [Test]
        public void ADefeatedUnitStopsBeingFoundByPhysicsQueries()
        {
            _unitObject.AddComponent<BoxCollider>();

            Kill();

            // 비활성 오브젝트의 콜라이더는 물리 질의에 잡히지 않는다. 그래서 탐지 버퍼를 더는 차지하지 않는다.
            Assert.That(
                _unitObject.activeInHierarchy,
                Is.False,
                "죽은 콜라이더가 남으면 탐지 후보 자리를 계속 차지해 산 유닛이 밀려난다.");
        }

        [Test]
        public void ADeathOfAnotherUnitDoesNotRetireThisOne()
        {
            var otherObject = CreateObject("Other");

            _deathChannel.Publish(new DeathEvent(otherObject, null));

            Assert.That(
                _unitObject.activeSelf,
                Is.True,
                "남의 사망 알림에 물러나면 전장이 한 번에 비워진다.");
        }

        [Test]
        public void TheDeathIsStillAnnouncedSoTheOutcomeCanBeDecided()
        {
            var received = 0;
            using var subscription = _deathChannel.Subscribe<DeathEvent>(_ => received++);

            Kill();

            Assert.That(
                received,
                Is.EqualTo(1),
                "물러나는 것이 알림을 가로채면 승패 집계가 유닛을 놓친다.");
        }

        /// <summary>유닛이 죽을 만큼 피해를 준다.</summary>
        private void Kill()
        {
            _health.ApplyDamage(_health.MaxHealth, null);
        }


        /// <summary>정리 목록에 등록된 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        /// <returns>만든 GameObject이다.</returns>
        private GameObject CreateObject(string objectName)
        {
            var created = new GameObject(objectName);
            _createdObjects.Add(created);
            return created;
        }
    }
}
