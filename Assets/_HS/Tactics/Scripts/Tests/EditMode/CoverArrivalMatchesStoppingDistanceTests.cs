using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Character;
using HS.Tactics.Character;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 이동기가 멈춘 자리가 엄폐 도착으로 인정되어, 엄폐를 놓았다 잡기를 되풀이하지 않는 것을 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 도착은 두 곳이 잰다. 이동기는 목적지 앞 멈춤 거리(PlanarCharacterMover의 stoppingDistance, 기본 0.5m) 안에 들면
    /// 도착이라 하고, 엄폐 상태는 엄폐 지점까지의 평면 거리가 도착 반경(UnitCoverState의 coverArrivalRadius, 기본 1m)
    /// 안이면 자리 잡았다고 한다. 둘은 같은 프리팹의 서로 다른 인스펙터 값이라 어긋날 수 있다. 어긋나면 확보는
    /// 이동기의 말을 듣고 끝나며 자리 잡지 못했다고 예약을 놓고, 다음 판단이 같은 자리를 다시 잡고, 이동기는 이미
    /// 그 자리라 곧바로 도착했다고 한다 — 그 되풀이다.
    /// </para>
    /// <para>
    /// 관계는 <see cref="UnitCoverState.CoverArrivalRadius"/> 한 곳이 정한다. 설정한 반경을 쓰되 이동기가 알려 주는
    /// 멈춤 거리 아래로는 내려가지 않는다. 여기서는 설정된 1m보다 멀리(1.5m)에서 멈추는 이동기로 그 관계를 밟는다.
    /// </para>
    /// </remarks>
    public sealed class CoverArrivalMatchesStoppingDistanceTests
    {
        private static readonly GameplayTag TakeCoverTag = GameplayTag.Parse(UnitAbilityTags.TakeCover);

        /// <summary>설정된 도착 반경(1m)보다 멀리서 멈추는 이동기의 멈춤 거리(미터)이다.</summary>
        private const float MoverStoppingDistance = 1.5f;

        /// <summary>멈춤 거리 안이지만 설정된 도착 반경 밖인, 이동기가 실제로 서는 거리(미터)이다.</summary>
        private const float StoppedShortBy = 1.4f;

        private readonly List<Object> _createdObjects = new();

        private AttributeSet _attributes;
        private GameplayAbilitySystem _abilitySystem;
        private GameObject _unitObject;
        private UnitCoverState _coverState;
        private StoppingMover _mover;
        private CoverPoint _coverPoint;

        [SetUp]
        public void SetUp()
        {
            _unitObject = CreateObject("Unit");
            var unit = _unitObject.AddComponent<TacticalUnit>();
            unit.SetDefinition(Track(UnitDefinition.CreateRuntime("소총병", 100, default, 3.5f, attackRange: 20f)));
            unit.InitializeUnit();

            _coverState = _unitObject.AddComponent<UnitCoverState>();
            var sensor = StraightLineCoverTravel.AttachSensor(_unitObject);
            _mover = _unitObject.AddComponent<StoppingMover>();
            var targetSource = _unitObject.AddComponent<CoverAbilityTestUnit.FakeTargetSource>();

            var threatObject = CreateObject("Threat");
            threatObject.transform.position = new Vector3(0f, 0f, 10f);
            targetSource.CurrentTarget = threatObject.transform;

            // 위협과 유닛 사이에 두어, 위협 쪽을 막아 주는 자리로 삼는다.
            var coverObject = CreateObject("Cover");
            coverObject.transform.position = new Vector3(0f, 0f, 3f);
            coverObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            _coverPoint = coverObject.AddComponent<CoverPoint>();
            sensor.SetCoverPoints(new[] { _coverPoint });

            _attributes = new AttributeSet();
            _abilitySystem = new GameplayAbilitySystem(new GameplayEffectRunner(_attributes), _unitObject);
            _abilitySystem.GrantAbility(Track(TakeCoverAbilityDefinition.CreateRuntime(UnitAbilityTags.TakeCover)));
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
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
        public void TheArrivalRadiusNeverFallsBelowWhereTheMoverStops()
        {
            LogAssert.Expect(LogType.Warning, new Regex("도착 반경"));

            Assert.That(
                _coverState.CoverArrivalRadius,
                Is.GreaterThanOrEqualTo(MoverStoppingDistance),
                "설정된 1m보다 멀리서 멈추는 이동기라면 그 멈춘 자리가 도착이어야 한다.");
        }

        [Test]
        public void StandingWhereTheMoverStoppedCountsAsInCover()
        {
            Assert.That(_coverState.ClaimCover(_coverPoint), Is.True);

            StopShortOfCover();

            Assert.That(_coverState.IsInCover, Is.True, "이동기가 멈춘 자리에서 자리 잡은 것으로 인정되어야 한다.");
        }

        [Test]
        public void TakingCoverDoesNotReclaimInALoopWhenTheMoverStopsShort()
        {
            Assert.That(_abilitySystem.TryActivate(TakeCoverTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(_mover.MoveCount, Is.EqualTo(1));

            StopShortOfCover();
            _abilitySystem.Tick(0.1f);

            Assert.That(_abilitySystem.IsActive(TakeCoverTag), Is.False, "이동기가 도착했다고 하면 확보가 끝난다.");
            Assert.That(_coverState.HasCoverClaim, Is.True, "멈춘 자리가 도착이므로 예약을 놓지 않는다.");
            for (var attempt = 0; attempt < 3; attempt++)
            {
                Assert.That(
                    _abilitySystem.TryActivate(TakeCoverTag),
                    Is.Not.EqualTo(GameplayAbilityActivationResult.Success),
                    "자리 잡은 유닛은 같은 자리를 다시 잡지 않는다.");
            }

            Assert.That(_coverPoint.Occupant, Is.SameAs(_unitObject), "자리는 계속 이 유닛의 것이다.");
            Assert.That(_mover.MoveCount, Is.EqualTo(1), "다시 잡아 다시 가라는 명령이 없다.");
        }

        [Test]
        public void WithoutAMoverThatReportsItsStoppingDistanceTheConfiguredRadiusIsAllThereIs()
        {
            var plainUnit = CreateObject("PlainUnit");
            var coverState = plainUnit.AddComponent<UnitCoverState>();
            plainUnit.AddComponent<CoverAbilityTestUnit.FakeMover>();

            Assert.That(
                coverState.CoverArrivalRadius,
                Is.EqualTo(1f).Within(0.001f),
                "멈춤 거리를 알려 주지 않는 이동기에는 관계를 걸 수 없어 설정한 반경이 전부다.");
        }

        /// <summary>이동기가 멈춤 거리 안, 설정된 도착 반경 밖에 유닛을 세운 것을 흉내 낸다.</summary>
        private void StopShortOfCover()
        {
            _unitObject.transform.position = _coverPoint.Position + Vector3.back * StoppedShortBy;
            _mover.HasReachedDestination = true;
        }

        private GameObject CreateObject(string objectName)
        {
            return Track(new GameObject(objectName));
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>설정된 도착 반경보다 멀리서 멈추는, 멈춤 거리를 알려 주는 테스트용 이동 구성요소이다.</summary>
        private sealed class StoppingMover : MonoBehaviour, ICharacterMover, IStoppingDistanceProvider
        {
            /// <summary>이동 명령을 받은 횟수이다.</summary>
            public int MoveCount { get; private set; }

            /// <inheritdoc />
            public bool HasReachedDestination { get; set; }

            /// <inheritdoc />
            public float StoppingDistance => MoverStoppingDistance;

            /// <inheritdoc />
            public bool MoveTo(Vector3 destination)
            {
                MoveCount++;
                return true;
            }

            /// <inheritdoc />
            public void Stop()
            {
            }
        }
    }
}
