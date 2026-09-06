using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Character;
using HS.Tactics.Combat;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>엄폐 어빌리티의 예약 수명과 사거리 조건에 따른 유지·해제를 검증한다.</summary>
    /// <remarks>
    /// 에디터는 플레이 모드가 아닐 때 Awake를 호출하지 않으므로 유닛 조립은
    /// <see cref="TacticalUnit.InitializeUnit"/>을 직접 불러 구동하고, 어빌리티 시스템도 직접 만들어 연결한다.
    /// </remarks>
    public sealed class CoverAbilityTests
    {
        /// <summary>엄폐 확보 어빌리티의 식별 태그 이름이다.</summary>
        private const string TakeCoverTagName = "Ability.TakeCover";

        /// <summary>엄폐 유지 어빌리티의 식별 태그 이름이다.</summary>
        private const string MaintainCoverTagName = "Ability.MaintainCover";

        /// <summary>테스트가 만든 오브젝트이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeSet _attributes;
        private GameplayAbilitySystem _abilitySystem;
        private GameObject _unitObject;
        private UnitCoverState _coverState;
        private CoverSensor _sensor;
        private FakeMover _mover;
        private FakeTargetSource _targetSource;
        private CoverPoint _coverPoint;
        private Transform _threat;

        [SetUp]
        public void SetUp()
        {
            _unitObject = CreateObject("Unit");
            _unitObject.transform.position = Vector3.zero;
            var unit = _unitObject.AddComponent<TacticalUnit>();
            unit.SetDefinition(Track(UnitDefinition.CreateRuntime(
                "소총병", 100, default, 3.5f, attackRange: 20f)));
            unit.InitializeUnit();

            _coverState = _unitObject.AddComponent<UnitCoverState>();
            _sensor = StraightLineCoverTravel.AttachSensor(_unitObject);
            _mover = _unitObject.AddComponent<FakeMover>();
            _targetSource = _unitObject.AddComponent<FakeTargetSource>();

            var threatObject = CreateObject("Threat");
            threatObject.transform.position = new Vector3(0f, 0f, 10f);
            _threat = threatObject.transform;
            _targetSource.CurrentTarget = _threat;

            // 위협과 유닛 사이에 두어, 위협 쪽을 막아 주는 자리로 삼는다.
            var coverObject = CreateObject("Cover");
            coverObject.transform.position = new Vector3(0f, 0f, 3f);
            coverObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            _coverPoint = coverObject.AddComponent<CoverPoint>();
            _sensor.SetCoverPoints(new[] { _coverPoint });

            _attributes = new AttributeSet();
            _abilitySystem = new GameplayAbilitySystem(new GameplayEffectRunner(_attributes), _unitObject);
            _abilitySystem.GrantAbility(Track(TakeCoverAbilityDefinition.CreateRuntime(TakeCoverTagName)));
            _abilitySystem.GrantAbility(Track(MaintainCoverAbilityDefinition.CreateRuntime(MaintainCoverTagName)));
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
        public void TakingCoverClaimsThePointAndStartsMoving()
        {
            Assert.That(ActivateTakeCover(), Is.EqualTo(GameplayAbilityActivationResult.Success));

            Assert.That(_coverState.HasCoverClaim, Is.True);
            Assert.That(_coverState.ClaimedCover, Is.SameAs(_coverPoint));
            Assert.That(_mover.LastDestination, Is.EqualTo(_coverPoint.Position));
        }

        [Test]
        public void CancellingBeforeArrivalReleasesTheClaimAndStopsItsMovementOnce()
        {
            ActivateTakeCover();

            _abilitySystem.CancelAbility(GameplayTag.Parse(TakeCoverTagName));
            _abilitySystem.CancelAbility(GameplayTag.Parse(TakeCoverTagName));

            Assert.That(
                _coverState.HasCoverClaim,
                Is.False,
                "도착하지 못한 예약을 쥐고 있으면 아무도 쓰지 않는 자리가 영구 점유된다.");
            Assert.That(_mover.StopCount, Is.EqualTo(1), "엄폐 분기가 끝나면 시작한 이동도 한 번 정리해야 한다.");
        }

        [Test]
        public void ArrivingKeepsTheClaimSoTheUnitDoesNotLoseItsSpot()
        {
            ActivateTakeCover();

            // 자리에 들어섰음을 흉내 낸다.
            _unitObject.transform.position = _coverPoint.Position;
            _mover.HasReachedDestination = true;
            _abilitySystem.Tick(0.1f);

            Assert.That(_abilitySystem.IsActive(GameplayTag.Parse(TakeCoverTagName)), Is.False, "도착하면 확보가 끝난다.");
            Assert.That(_mover.StopCount, Is.EqualTo(1), "도착 판정 이후 남은 경로를 계속 따라가면 안 된다.");
            Assert.That(
                _coverState.HasCoverClaim,
                Is.True,
                "도착해 실제로 쓰고 있는 자리는 확보가 끝나도 유지되어야 한다.");
        }

        [Test]
        public void ARejectedMoveDoesNotStopMovementOwnedElsewhere()
        {
            var otherDestination = new Vector3(5f, 0f, 0f);
            Assert.That(_mover.MoveTo(otherDestination), Is.True);
            _mover.AcceptMoves = false;

            Assert.That(ActivateTakeCover(), Is.EqualTo(GameplayAbilityActivationResult.Success));
            _abilitySystem.Tick(0.1f);

            Assert.That(_coverState.HasCoverClaim, Is.False);
            Assert.That(_mover.StopCount, Is.Zero, "이동 요청을 받아들이지 않았다면 다른 소유자의 이동을 멈추면 안 된다.");
            Assert.That(_mover.LastDestination, Is.EqualTo(otherDestination));
        }

        [Test]
        public void AlreadyCoveredUnitDoesNotTakeCoverAgain()
        {
            ActivateTakeCover();
            _unitObject.transform.position = _coverPoint.Position;
            _mover.HasReachedDestination = true;
            _abilitySystem.Tick(0.1f);

            Assert.That(
                ActivateTakeCover(),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected),
                "이미 위협에 대해 엄폐가 되어 있으면 새 자리를 찾을 이유가 없다.");
        }

        [Test]
        public void AUnitThatCannotShootDoesNotTakeCover()
        {
            var unit = _unitObject.GetComponent<TacticalUnit>();
            var brokenUnitObject = CreateObject("NoDefinitionUnit");
            var brokenUnit = brokenUnitObject.AddComponent<TacticalUnit>();
            brokenUnit.InitializeUnit();
            brokenUnitObject.AddComponent<UnitCoverState>();
            StraightLineCoverTravel.AttachSensor(brokenUnitObject).SetCoverPoints(new[] { _coverPoint });
            brokenUnitObject.AddComponent<FakeMover>();
            brokenUnitObject.AddComponent<FakeTargetSource>().CurrentTarget = _threat;
            var brokenSystem = new GameplayAbilitySystem(
                new GameplayEffectRunner(new AttributeSet()), brokenUnitObject);
            brokenSystem.GrantAbility(Track(TakeCoverAbilityDefinition.CreateRuntime(TakeCoverTagName)));

            Assert.That(unit.Definition, Is.Not.Null);
            Assert.That(
                brokenSystem.TryActivate(GameplayTag.Parse(TakeCoverTagName)),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected),
                "사거리를 모르면 쏠 수 있는 자리인지 가릴 수 없으므로 엄폐를 쓰지 않는다.");
        }

        [Test]
        public void MaintainingRequiresAClaimedCover()
        {
            Assert.That(
                ActivateMaintainCover(),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected),
                "지킬 자리가 없으면 유지할 것도 없다.");
        }

        [Test]
        public void MaintainingKeepsRunningWhileTheCoverStillWorks()
        {
            SettleIntoCover();

            Assert.That(ActivateMaintainCover(), Is.EqualTo(GameplayAbilityActivationResult.Success));

            _abilitySystem.Tick(1f);

            Assert.That(_abilitySystem.IsActive(GameplayTag.Parse(MaintainCoverTagName)), Is.True);
            Assert.That(_coverState.HasCoverClaim, Is.True);
        }

        [Test]
        public void MaintainingReleasesTheCoverWhenTheTargetGoesOutOfRange()
        {
            SettleIntoCover();
            ActivateMaintainCover();

            _threat.position = new Vector3(0f, 0f, 500f);
            _abilitySystem.Tick(1f);

            Assert.That(
                _abilitySystem.IsActive(GameplayTag.Parse(MaintainCoverTagName)),
                Is.False,
                "쏠 수 없는 거리에서 앉아 있으면 교착이 되므로 자리를 내주어야 한다.");
            Assert.That(_coverState.HasCoverClaim, Is.False);
        }

        [Test]
        public void MaintainingReleasesTheCoverWhenTheTargetDisappears()
        {
            SettleIntoCover();
            ActivateMaintainCover();

            _targetSource.CurrentTarget = null;
            _abilitySystem.Tick(1f);

            Assert.That(_coverState.HasCoverClaim, Is.False);
        }

        [Test]
        public void BeingPreemptedDoesNotGiveUpTheCover()
        {
            SettleIntoCover();
            ActivateMaintainCover();

            _abilitySystem.CancelAbility(GameplayTag.Parse(MaintainCoverTagName));

            Assert.That(
                _coverState.HasCoverClaim,
                Is.True,
                "가로채인 것과 자리를 포기한 것은 다르다. 교전이 데려가는 동안에도 자리는 이 유닛의 것이다.");
        }



        /// <summary>엄폐를 확보하고 그 자리에 들어선 상태를 만든다.</summary>
        private void SettleIntoCover()
        {
            ActivateTakeCover();
            _unitObject.transform.position = _coverPoint.Position;
            _mover.HasReachedDestination = true;
            _abilitySystem.Tick(0.1f);
        }

        /// <summary>엄폐 확보 어빌리티 활성화를 시도한다.</summary>
        private GameplayAbilityActivationResult ActivateTakeCover()
        {
            return _abilitySystem.TryActivate(GameplayTag.Parse(TakeCoverTagName));
        }

        /// <summary>엄폐 유지 어빌리티 활성화를 시도한다.</summary>
        private GameplayAbilityActivationResult ActivateMaintainCover()
        {
            return _abilitySystem.TryActivate(GameplayTag.Parse(MaintainCoverTagName));
        }

        /// <summary>정리 목록에 등록된 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        private GameObject CreateObject(string objectName)
        {
            return Track(new GameObject(objectName));
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 객체이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>목적지와 도착 여부를 직접 다룰 수 있는 테스트용 이동 구성요소이다.</summary>
        private sealed class FakeMover : MonoBehaviour, ICharacterMover
        {
            /// <summary>마지막으로 받은 목적지이다.</summary>
            public Vector3 LastDestination { get; private set; }

            public int StopCount { get; private set; }
            public bool AcceptMoves { get; set; } = true;

            /// <inheritdoc />
            public bool HasReachedDestination { get; set; }

            /// <inheritdoc />
            public bool MoveTo(Vector3 destination)
            {
                if (!AcceptMoves)
                {
                    return false;
                }

                LastDestination = destination;
                return true;
            }

            /// <inheritdoc />
            public void Stop()
            {
                StopCount++;
            }
        }

        /// <summary>대상을 직접 지정할 수 있는 테스트용 대상 공급자이다.</summary>
        private sealed class FakeTargetSource : MonoBehaviour, ICombatTargetSource
        {
            /// <inheritdoc />
            public Transform CurrentTarget { get; set; }
        }
    }
}
