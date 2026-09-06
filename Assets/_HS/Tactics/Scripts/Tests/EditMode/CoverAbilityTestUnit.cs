using System;
using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Character;
using HS.Tactics.Combat;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 엄폐 확보 어빌리티를 실제 활성화·예약 경로로 구동하는 테스트용 유닛이다.
    /// EditMode에서는 TacticalUnit.InitializeUnit을 직접 호출해 조립하고 어빌리티 시스템을 연결한다.
    /// </summary>
    internal sealed class CoverAbilityTestUnit : IDisposable
    {
        /// <summary>엄폐 확보 어빌리티의 식별 태그이다.</summary>
        private static readonly GameplayTag TakeCoverTag =
            GameplayTag.Parse(UnitAbilityTags.TakeCover);

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly AttributeSet _attributes;
        private readonly GameplayAbilitySystem _abilitySystem;

        /// <summary>엄폐 확보 어빌리티를 갖춘 유닛을 만든다.</summary>
        /// <param name="position">유닛을 세울 좌표이다.</param>
        /// <param name="attackRange">유닛의 사거리이며 쏠 수 있는 엄폐만 고르는 데 쓰인다.</param>
        /// <param name="threat">이 유닛이 노리는 위협이다.</param>
        /// <param name="coverPoints">이 유닛이 볼 수 있는 엄폐 지점들이다.</param>
        internal CoverAbilityTestUnit(
            Vector3 position,
            float attackRange,
            Transform threat,
            params CoverPoint[] coverPoints)
        {
            GameObject = Track(new GameObject("Unit"));
            GameObject.transform.position = position;

            var unit = GameObject.AddComponent<TacticalUnit>();
            unit.SetDefinition(Track(UnitDefinition.CreateRuntime(
                "소총병", 100, default, 3.5f, attackRange: attackRange)));
            unit.InitializeUnit();

            CoverState = GameObject.AddComponent<UnitCoverState>();
            Sensor = StraightLineCoverTravel.AttachSensor(GameObject);
            Mover = GameObject.AddComponent<FakeMover>();
            TargetSource = GameObject.AddComponent<FakeTargetSource>();
            TargetSource.CurrentTarget = threat;
            Sensor.SetCoverPoints(coverPoints);

            _attributes = new AttributeSet();
            _abilitySystem = new GameplayAbilitySystem(new GameplayEffectRunner(_attributes), GameObject);
            _abilitySystem.GrantAbility(Track(TakeCoverAbilityDefinition.CreateRuntime(
                UnitAbilityTags.TakeCover)));
        }

        /// <summary>유닛의 GameObject이다.</summary>
        internal GameObject GameObject { get; }

        /// <summary>유닛의 엄폐 상태이다.</summary>
        internal UnitCoverState CoverState { get; }

        /// <summary>유닛의 엄폐 센서이다.</summary>
        internal CoverSensor Sensor { get; }

        /// <summary>목적지를 확인할 수 있는 테스트용 이동 구성요소이다.</summary>
        internal FakeMover Mover { get; }

        /// <summary>대상을 갈아 끼울 수 있는 테스트용 대상 공급자이다.</summary>
        internal FakeTargetSource TargetSource { get; }

        /// <summary>엄폐 확보 어빌리티 활성화를 시도한다. 이것이 프로덕션이 예약을 수행하는 경로이다.</summary>
        /// <returns>활성화 결과이다.</returns>
        internal GameplayAbilityActivationResult TakeCover()
        {
            return _abilitySystem.TryActivate(TakeCoverTag);
        }

        /// <summary>활성 중인 엄폐 확보를 취소한다.</summary>
        internal void CancelTakeCover()
        {
            _abilitySystem.CancelAbility(TakeCoverTag);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _attributes?.Dispose();
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 객체이다.</param>
        /// <returns>등록한 객체를 그대로 돌려준다.</returns>
        private T Track<T>(T createdObject) where T : UnityEngine.Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>목적지와 도착 여부를 직접 다룰 수 있는 테스트용 이동 구성요소이다.</summary>
        internal sealed class FakeMover : MonoBehaviour, ICharacterMover
        {
            /// <summary>마지막으로 받은 목적지이다.</summary>
            internal Vector3 LastDestination { get; private set; }

            /// <inheritdoc />
            public bool HasReachedDestination { get; set; }

            /// <inheritdoc />
            public bool MoveTo(Vector3 destination)
            {
                LastDestination = destination;
                return true;
            }

            /// <inheritdoc />
            public void Stop()
            {
            }
        }

        /// <summary>대상을 직접 지정할 수 있는 테스트용 대상 공급자이다.</summary>
        internal sealed class FakeTargetSource : MonoBehaviour, ICombatTargetSource
        {
            /// <inheritdoc />
            public Transform CurrentTarget { get; set; }
        }
    }
}
