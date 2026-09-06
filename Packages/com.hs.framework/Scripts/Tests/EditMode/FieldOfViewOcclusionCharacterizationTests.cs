using System.Collections.Generic;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 3D 시야 판정에서 엄폐물 위치와 관찰 방향에 따른 가림 결과를 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 시야 광선은 관찰자의 눈높이에서 대상의 발밑으로 내려간다. 같은 엄폐물이라도 관찰자에게
    /// 가까우면 광선이 높아 넘어가고, 대상에게 가까우면 광선이 낮아 가려진다.
    /// 따라서 엄폐한 유닛은 보이지 않으면서 엄폐물 너머를 볼 수 있다.
    /// </para>
    /// <para>
    /// <see cref="FixtureBlocksARayAimedStraightIntoTheCover"/>는 테스트 환경의 물리 질의가
    /// 엄폐물 콜라이더를 감지하는지 확인한다.
    /// </para>
    /// </remarks>
    public sealed class FieldOfViewOcclusionCharacterizationTests
    {
        /// <summary>엄폐물 윗면의 높이(미터)이다.</summary>
        private const float CoverTopHeight = 1f;

        /// <summary>유닛의 눈높이(미터)이며 엄폐물보다 높다.</summary>
        private const float EyeHeight = 1.4f;

        private readonly List<GameObject> _createdObjects = new();
        private LayerMask _occlusionMask;

        [SetUp]
        public void SetUp()
        {
            // 기본 레이어만 쓰므로 프로젝트에 레이어를 새로 만들지 않아도 된다.
            _occlusionMask = 1 << 0;
            CreateCover();
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
        }

        [Test]
        public void FixtureBlocksARayAimedStraightIntoTheCover()
        {
            // 이 확인이 실패하면 뒤의 두 테스트는 아무것도 검증하지 못한다.
            // 물리 질의가 동작하지 않는 환경에서 조용히 통과하는 것을 막으려고 먼저 확인한다.
            Physics.SyncTransforms();

            var isBlocked = Physics.Raycast(
                new Vector3(0f, 0.5f, -5f),
                Vector3.forward,
                10f,
                _occlusionMask,
                QueryTriggerInteraction.Ignore);

            Assert.That(isBlocked, Is.True, "엄폐물 콜라이더가 광선을 막지 못하면 이 테스트 묶음은 의미가 없다.");
        }

        [Test]
        public void CoveredUnitIsHiddenFromADistantObserver()
        {
            // 관찰자가 멀리 있으면 광선이 엄폐물에 이를 즈음 이미 낮아져 막힌다.
            var observer = CreateTransform("Observer", new Vector3(0f, EyeHeight, -10f), Vector3.forward);
            var target = CreateTransform("Target", new Vector3(0f, 0f, 1f), Vector3.back);
            var sensor = new FieldOfViewSensor(120f, 30f, _occlusionMask);
            Physics.SyncTransforms();

            Assert.That(
                sensor.CanSee(observer, target),
                Is.False,
                "엄폐물 뒤에 자리 잡은 유닛은 멀리 있는 적에게 보이지 않아야 한다.");
        }

        [Test]
        public void UnitStandingBesideCoverStillSeesPastIt()
        {
            // 관찰자가 엄폐물에 붙어 있으면 광선이 아직 높아 그 엄폐물을 넘어간다.
            var observer = CreateTransform("Observer", new Vector3(0f, EyeHeight, -1f), Vector3.forward);
            var target = CreateTransform("Target", new Vector3(0f, 0f, 10f), Vector3.back);
            var sensor = new FieldOfViewSensor(120f, 30f, _occlusionMask);
            Physics.SyncTransforms();

            Assert.That(
                sensor.CanSee(observer, target),
                Is.True,
                "자기가 붙어 있는 엄폐물에 시야를 잃으면 유닛이 숨은 채 아무것도 하지 않게 된다.");
        }

        [Test]
        public void SeeingPastNearbyCoverDoesNotDependOnOccupyingIt()
        {
            // 엄폐 경쟁에서 진 유닛도 엄폐물 옆에 서게 된다. 그 유닛이 눈이 멀면 안 된다.
            // 지금 판정은 점유 여부를 아예 알지 못하므로, 옆으로 비켜서도 결과가 같아야 한다.
            var beside = CreateTransform("Beside", new Vector3(1.5f, EyeHeight, -1f), Vector3.forward);
            var target = CreateTransform("Target", new Vector3(0f, 0f, 10f), Vector3.back);
            var sensor = new FieldOfViewSensor(120f, 30f, _occlusionMask);
            Physics.SyncTransforms();

            Assert.That(
                sensor.CanSee(beside, target),
                Is.True,
                "엄폐물을 차지하지 못한 유닛도 그 엄폐물 너머를 볼 수 있어야 한다.");
        }

        [Test]
        public void VisionIsAsymmetricBetweenTheTwoSidesOfTheSameCover()
        {
            // 같은 엄폐물, 같은 두 지점인데 방향에 따라 결과가 갈린다는 것이 이 성질의 핵심이다.
            var nearSide = CreateTransform("NearSide", new Vector3(0f, EyeHeight, -1f), Vector3.forward);
            var farSide = CreateTransform("FarSide", new Vector3(0f, EyeHeight, 10f), Vector3.back);
            var nearFoot = CreateTransform("NearFoot", new Vector3(0f, 0f, -1f), Vector3.forward);
            var farFoot = CreateTransform("FarFoot", new Vector3(0f, 0f, 10f), Vector3.back);
            var sensor = new FieldOfViewSensor(120f, 30f, _occlusionMask);
            Physics.SyncTransforms();

            Assert.That(
                sensor.CanSee(nearSide, farFoot),
                Is.True,
                "엄폐물에 붙어 있는 쪽에서는 넘겨다본다.");
            Assert.That(
                sensor.CanSee(farSide, nearFoot),
                Is.False,
                "엄폐물에 붙어 있는 상대를 멀리서는 보지 못한다.");
        }

        /// <summary>원점에 낮고 넓은 엄폐물 콜라이더를 만든다.</summary>
        private void CreateCover()
        {
            var cover = new GameObject("Cover");
            _createdObjects.Add(cover);
            cover.layer = 0;
            cover.transform.position = new Vector3(0f, CoverTopHeight * 0.5f, 0f);
            var collider = cover.AddComponent<BoxCollider>();
            collider.size = new Vector3(3f, CoverTopHeight, 0.5f);
        }

        /// <summary>지정한 위치와 방향을 가진 빈 오브젝트를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        /// <param name="position">배치할 월드 좌표이다.</param>
        /// <param name="forward">바라볼 방향이다.</param>
        /// <returns>만든 오브젝트의 Transform이다.</returns>
        private Transform CreateTransform(string objectName, Vector3 position, Vector3 forward)
        {
            var created = new GameObject(objectName);
            _createdObjects.Add(created);
            created.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            return created.transform;
        }
    }
}
