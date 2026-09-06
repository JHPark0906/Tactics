using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Tactics.Cover;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 엄폐 지점의 점유가 "도착"이 아니라 "선택" 시점에 일어난다는 것을 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>점유는 고르는 순간에 일어난다.</b> 엄폐 확보 어빌리티가 활성화하는 그 자리에서 예약까지 마치므로,
    /// 같은 지점을 두고 다투는 두 유닛의 승자는 <b>먼저 고른 쪽</b>이다. 이동은 승패에 관여하지 않는다.
    /// </para>
    /// <para>
    /// <b>이 사실은 코드에 적혀 있어야 한다.</b> "먼저 도착한 유닛이 이긴다"고
    /// 읽으면, 이동 방식이 다른 유닛들의 도착 시각을 맞춰야 한다는 요구가 뒤따라 생긴다.
    /// 그 요구는 근거가 없고, 근거가 없다는 것을 말해 주는 것이 이 검사이다.
    /// </para>
    /// <para>
    /// <b>도착을 흉내 내는 장치를 여기에 넣지 마라.</b> 유닛을 엄폐 지점으로 옮겨 놓거나 도착 판정을
    /// 거들어 주면, 도착이 승패에 관여한다는 잘못된 인상이 되살아난다. 두 유닛은 처음부터 끝까지
    /// 제자리에 서 있어야 하고, 그런데도 예약이 갈린다는 것이 이 검사의 전부이다.
    /// </para>
    /// </remarks>
    public sealed class CoverClaimTimingTests
    {
        /// <summary>엄폐 지점에서 위협까지의 거리보다 넉넉한 사거리이다.</summary>
        private const float AttackRange = 30f;

        private readonly List<GameObject> _createdObjects = new();
        private readonly List<CoverAbilityTestUnit> _units = new();

        private GameObject _coverObject;
        private CoverPoint _coverPoint;
        private Transform _threat;

        [SetUp]
        public void SetUp()
        {
            _coverObject = CreateObject("Cover", Vector3.zero);
            _coverObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            _coverPoint = _coverObject.AddComponent<CoverPoint>();
            _threat = CreateObject("Threat", new Vector3(0f, 0f, 20f)).transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var unit in _units)
            {
                unit.Dispose();
            }

            _units.Clear();
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void TheCoverIsClaimedWhenItIsChosenNotWhenItIsReached()
        {
            var unit = CreateUnit(new Vector3(-5f, 0f, -5f));

            Assert.That(
                unit.TakeCover(),
                Is.EqualTo(GameplayAbilityActivationResult.Success),
                "빈 엄폐 지점은 고를 수 있어야 한다.");

            // 이 둘이 동시에 성립하는 것이 이 검사의 핵심이다.
            Assert.That(
                unit.CoverState.IsInCover,
                Is.False,
                "고른 유닛은 아직 그 자리에 도착하지 않았다.");
            Assert.That(
                _coverPoint.IsOccupied,
                Is.True,
                "그런데도 지점은 이미 점유되어 있다. 점유는 고르는 순간에 일어난다.");
            Assert.That(
                _coverPoint.Occupant,
                Is.SameAs(unit.GameObject),
                "점유자는 먼저 고른 유닛이다.");
        }

        [Test]
        public void TheUnitThatChoosesFirstWinsWhileNeitherHasMoved()
        {
            var first = CreateUnit(new Vector3(-5f, 0f, -5f));
            var second = CreateUnit(new Vector3(5f, 0f, -5f));

            first.TakeCover();

            Assert.That(
                second.TakeCover(),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected),
                "먼저 고른 유닛이 이긴다. 나중에 고른 유닛은 같은 지점을 쓸 수 없다.");
            Assert.That(second.CoverState.HasCoverClaim, Is.False);
            Assert.That(
                _coverPoint.Occupant,
                Is.SameAs(first.GameObject),
                "진 유닛이 점유를 빼앗지 못한다.");
        }

        [Test]
        public void BeingCloserToTheCoverDoesNotWinIt()
        {
            var first = CreateUnit(new Vector3(-5f, 0f, -5f));

            // 나중에 고르는 유닛을 엄폐 지점 바로 옆에 세운다. 도착이 승패에 관여한다면 이쪽이 이겨야 한다.
            var second = CreateUnit(new Vector3(0.2f, 0f, 0f));

            first.TakeCover();

            Assert.That(
                second.TakeCover(),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected),
                "가까이 서 있는 것은 예약에 아무 힘도 없다. 순서를 정하는 것은 고른 차례뿐이다.");
            Assert.That(_coverPoint.Occupant, Is.SameAs(first.GameObject));
        }

        /// <summary>엄폐 지점을 볼 수 있는 유닛을 만든다.</summary>
        /// <param name="position">유닛을 세울 좌표이다.</param>
        /// <returns>만든 유닛이다.</returns>
        private CoverAbilityTestUnit CreateUnit(Vector3 position)
        {
            var unit = new CoverAbilityTestUnit(position, AttackRange, _threat, _coverPoint);
            _units.Add(unit);
            return unit;
        }

        /// <summary>정리 목록에 등록된 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        /// <param name="position">오브젝트를 놓을 좌표이다.</param>
        /// <returns>만든 GameObject이다.</returns>
        private GameObject CreateObject(string objectName, Vector3 position)
        {
            var created = new GameObject(objectName);
            created.transform.position = position;
            _createdObjects.Add(created);
            return created;
        }
    }
}
