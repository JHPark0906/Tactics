using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using HS.Tactics.Behaviour;
using HS.Tactics.Combat;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 게임 자리 정의가 유닛에서 필요한 것을 찾아 실제 자리를 만드는지, 없으면 만들지 않는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>정의는 에셋에 실리는 설명이고 자리는 그것이 만든 실행체다.</b> 여기서 확인하는 것은
    /// 그 사이의 배선이다 — 어느 정의가 어느 자리를 만들고, 무엇을 유닛에서 찾으며,
    /// 찾지 못하면 어떻게 답하는가.
    /// </para>
    /// <para>
    /// <b>필요한 것이 없을 때 null을 돌려주는 것</b>도 지켜야 할 약속이다. 억지로 만들면 실행 중에
    /// 그 자리에서 null을 만나 트리가 멈추고, 화면에는 "유닛이 가만히 서 있는 것"만 남는다.
    /// </para>
    /// </remarks>
    public sealed class UnitBehaviourDefinitionTests
    {
        private readonly List<Object> _createdObjects = new();

        private GameObject _unitObject;
        private UnitDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _unitObject = CreateObject("Unit");
            var unit = _unitObject.AddComponent<TacticalUnit>();
            _definition = Track(UnitDefinition.CreateRuntime("소총병", 100, default, 3.5f, attackRange: 12f));
            unit.SetDefinition(_definition);
            unit.InitializeUnit();
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
        public void HoldWithinAttackRangeIsBuiltWhenTheUnitCanMove()
        {
            _unitObject.AddComponent<FakeMover>();

            Assert.That(Build(new HoldWithinAttackRangeDefinition()), Is.InstanceOf<HoldWithinRangeBehaviour>());
        }

        [Test]
        public void HoldWithinAttackRangeIsNotBuiltWithoutAMover()
        {
            Assert.That(Build(new HoldWithinAttackRangeDefinition()), Is.Null, "멈추게 할 이동 수단이 없으면 자리를 만들 수 없다.");
        }

        [Test]
        public void SpreadChaseTargetIsBuiltWhenTheUnitCanMove()
        {
            _unitObject.AddComponent<FakeMover>();

            Assert.That(Build(new SpreadChaseTargetDefinition()), Is.InstanceOf<SpreadChaseTargetBehaviour>());
        }

        [Test]
        public void SpreadChaseTargetIsNotBuiltWithoutAMover()
        {
            Assert.That(Build(new SpreadChaseTargetDefinition()), Is.Null);
        }

        [Test]
        public void SelectCoverDestinationIsBuiltWhenTheUnitHasCoverParts()
        {
            _unitObject.AddComponent<CoverSensor>();
            _unitObject.AddComponent<UnitCoverState>();

            Assert.That(Build(new SelectCoverDestinationDefinition()), Is.InstanceOf<SelectCoverDestinationBehaviour>());
        }

        [Test]
        public void SelectCoverDestinationIsNotBuiltWithoutASensor()
        {
            _unitObject.AddComponent<UnitCoverState>();

            Assert.That(Build(new SelectCoverDestinationDefinition()), Is.Null, "엄폐 지점을 찾을 센서가 없으면 고를 수 없다.");
        }

        [Test]
        public void MaintainCoverIsBuiltWhenTheUnitHasCoverState()
        {
            _unitObject.AddComponent<UnitCoverState>();

            Assert.That(Build(new MaintainCoverDefinition()), Is.InstanceOf<MaintainCoverBehaviour>());
        }

        [Test]
        public void MaintainCoverIsNotBuiltWithoutCoverState()
        {
            Assert.That(Build(new MaintainCoverDefinition()), Is.Null);
        }

        [Test]
        public void DetectEnemyServiceIsBuiltWhenTheUnitHasADetector()
        {
            _unitObject.AddComponent<EnemyDetector>();

            Assert.That(Build(new DetectEnemyServiceDefinition()), Is.InstanceOf<DetectEnemyService>());
        }

        [Test]
        public void DetectEnemyServiceIsNotBuiltWithoutADetector()
        {
            Assert.That(Build(new DetectEnemyServiceDefinition()), Is.Null, "적을 찾을 탐지기가 없으면 서비스가 할 일이 없다.");
        }

        [Test]
        public void RefreshAdvanceDirectionServiceOnlyNeedsTheOwner()
        {
            Assert.That(Build(new RefreshAdvanceDirectionDefinition()), Is.InstanceOf<RefreshAdvanceDirectionService>());
        }

        [Test]
        public void TargetInRangeServiceOnlyNeedsTheOwner()
        {
            Assert.That(Build(new TargetInAttackRangeServiceDefinition()), Is.InstanceOf<TargetInRangeService>());
        }

        [Test]
        public void NothingIsBuiltWithoutAnOwner()
        {
            var context = new BehaviourBuildContext(null, new BehaviourContext());

            Assert.That(new HoldWithinAttackRangeDefinition().CreateBehaviour(context), Is.Null);
            Assert.That(new SpreadChaseTargetDefinition().CreateBehaviour(context), Is.Null);
            Assert.That(new SelectCoverDestinationDefinition().CreateBehaviour(context), Is.Null);
            Assert.That(new MaintainCoverDefinition().CreateBehaviour(context), Is.Null);
            Assert.That(new DetectEnemyServiceDefinition().CreateBehaviour(context), Is.Null);
            Assert.That(new RefreshAdvanceDirectionDefinition().CreateBehaviour(context), Is.Null);
            Assert.That(new TargetInAttackRangeServiceDefinition().CreateBehaviour(context), Is.Null);
        }

        [Test]
        public void EveryDefinitionHasADisplayName()
        {
            var definitions = new BehaviourNodeDefinition[]
            {
                new HoldWithinAttackRangeDefinition(),
                new SpreadChaseTargetDefinition(),
                new SelectCoverDestinationDefinition(),
                new MaintainCoverDefinition(),
                new DetectEnemyServiceDefinition(),
                new RefreshAdvanceDirectionDefinition(),
                new TargetInAttackRangeServiceDefinition()
            };

            foreach (var definition in definitions)
            {
                Assert.That(definition.DisplayName, Is.Not.Null.And.Not.Empty, $"{definition.GetType().Name}은 편집기에서 이름이 보여야 한다.");
            }
        }

        /// <summary>지금 조립된 유닛으로 정의가 자리를 만들게 한다.</summary>
        /// <param name="definition">자리를 만들 정의이다.</param>
        /// <returns>만들어진 자리이며 만들 수 없으면 null이다.</returns>
        private IBehaviour Build(BehaviourNodeDefinition definition)
            => definition.CreateBehaviour(new BehaviourBuildContext(_unitObject, new BehaviourContext()));

        /// <summary>정리 목록에 등록된 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        /// <returns>만든 GameObject이다.</returns>
        private GameObject CreateObject(string objectName) => Track(new GameObject(objectName));

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 객체이다.</param>
        /// <returns>등록한 객체를 그대로 돌려준다.</returns>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>아무 일도 하지 않는 검사용 이동 구성요소이다.</summary>
        private sealed class FakeMover : MonoBehaviour, ICharacterMover
        {
            /// <inheritdoc />
            public bool HasReachedDestination => true;

            /// <inheritdoc />
            public bool MoveTo(Vector3 destination) => true;

            /// <inheritdoc />
            public void Stop()
            {
            }
        }
    }
}
