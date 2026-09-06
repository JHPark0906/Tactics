using HS.Framework.Tests.Support;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Flow;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>유닛 조립 규약이 정의 수치와 행동 분기를 실제로 반영하는지 검증한다.</summary>
    /// <remarks>
    /// 에디터는 플레이 모드가 아닐 때 Awake를 호출하지 않으므로 조립은
    /// <see cref="TacticalUnit.InitializeUnit"/>을 직접 불러 구동한다.
    /// 그래서 이 테스트는 수명주기 우회 없이도 실제 조립 경로를 그대로 검증한다.
    /// </remarks>
    public sealed class TacticalUnitTests
    {
        private GameObject _unitObject;
        private UnitDefinition _definition;
        private BehaviourTreeAsset _treeAsset;
        private TestUnitAttributes _attributes;

        [SetUp]
        public void SetUp()
        {
            _attributes = new TestUnitAttributes();
        }

        [TearDown]
        public void TearDown()
        {
            if (_unitObject != null)
            {
                Object.DestroyImmediate(_unitObject);
                _unitObject = null;
            }

            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
                _definition = null;
            }

            if (_treeAsset != null)
            {
                Object.DestroyImmediate(_treeAsset);
                _treeAsset = null;
            }

            _attributes.Dispose();
        }

        /// <remarks>
        /// <b>모든 유닛이 승패 집계 대상이다.</b> 예외가 없으므로 붙일지 말지는 고를 것이 아니고,
        /// 빠뜨리면 그 유닛이 집계에 들어가지 않아 <b>적을 다 죽였는데 전투가 끝나지 않는다.</b>
        /// 오류도 경고도 없으므로 원인을 찾을 단서가 없다.
        /// </remarks>
        [Test]
        public void EveryUnitBringsTheThingsThatMustNotBeForgotten()
        {
            var unitObject = new GameObject("BareUnit");

            try
            {
                unitObject.AddComponent<TacticalUnit>();

                Assert.That(
                    unitObject.GetComponent<BattleUnitRegistrant>(),
                    Is.Not.Null,
                    "집계 등록이 빠지면 적을 다 죽여도 전투가 끝나지 않는다.");
                Assert.That(
                    unitObject.GetComponent<DefeatedUnitRetirement>(),
                    Is.Not.Null,
                    "물러남이 빠지면 죽은 유닛이 계속 싸운다.");
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void RequiredFrameworkComponentsAreAddedWithTheUnit()
        {
            var unit = CreateUnit();

            Assert.That(unit.GetComponent<HealthAttributeComponent>(), Is.Not.Null, "체력 문이 빠지면 유닛이 피해를 받지 못한다.");
            Assert.That(unit.GetComponent<TeamMember>(), Is.Not.Null);
            Assert.That(unit.GetComponent<BehaviourTreeRunner>(), Is.Not.Null);
            Assert.That(unit, Is.InstanceOf<CharacterBase>());
        }

        [Test]
        public void DefinitionValuesAreAppliedToTheFrameworkComponents()
        {
            var unit = CreateUnit();
            _definition = _attributes.CreateUnitDefinition("소총병", 250, new TeamId(2));
            unit.SetDefinition(_definition);

            unit.InitializeUnit();

            Assert.That(unit.IsUnitInitialized, Is.True);
            Assert.That(unit.Health.MaxHealth, Is.EqualTo(250));
            Assert.That(unit.Health.CurrentHealth, Is.EqualTo(250));
            Assert.That(unit.Team.TeamId, Is.EqualTo(new TeamId(2)));
        }

        [Test]
        public void AnAlreadyAssignedTeamSurvivesTheDefinitionDefault()
        {
            var unit = CreateUnit();
            unit.GetComponent<TeamMember>().SetTeam(new TeamId(7));
            _definition = UnitDefinition.CreateRuntime("소총병", 100, new TeamId(2));
            unit.SetDefinition(_definition);

            unit.InitializeUnit();

            Assert.That(unit.Team.TeamId, Is.EqualTo(new TeamId(7)));
        }

        [Test]
        public void InitializeUnitRunsOnlyOnce()
        {
            var unit = CreateUnit();
            _definition = _attributes.CreateUnitDefinition("소총병", 250);
            unit.SetDefinition(_definition);
            unit.InitializeUnit();

            // 피해를 주려면 알림 발행자가 있어야 한다. 없으면 체력 문이 오류를 남기며, 그것은 조용한 좀비를 막는 계약이다.
            unit.Health.InjectMessagePipePublishers(new TestPublisher<DamageAppliedEvent>(), new TestPublisher<DeathEvent>());
            unit.Health.ApplyDamage(50, null);
            unit.InitializeUnit();

            Assert.That(unit.Health.CurrentHealth, Is.EqualTo(200));
        }

        [Test]
        public void ATreeAssetStartsTheBehaviourTree()
        {
            var unit = CreateUnit();
            _unitObject.AddComponent<StubMover>();
            _definition = _attributes.CreateUnitDefinition("소총병", 100);
            unit.SetDefinition(_definition);
            _treeAsset = MinimalTreeAsset();
            SetPrivate(unit, "behaviourTree", _treeAsset);

            unit.InitializeUnit();

            Assert.That(unit.GetComponent<BehaviourTreeRunner>().IsInitialized, Is.True);
        }

        [Test]
        public void WithoutATreeAssetTheBehaviourTreeDoesNotStart()
        {
            var unit = CreateUnit();
            _unitObject.AddComponent<StubMover>();
            _definition = _attributes.CreateUnitDefinition("소총병", 100);
            unit.SetDefinition(_definition);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("행동 트리 에셋이 없어"));

            unit.InitializeUnit();

            Assert.That(unit.GetComponent<BehaviourTreeRunner>().IsInitialized, Is.False,
                "무엇을 할지 적힌 것이 없는데 돌리면 유닛이 아무것도 안 하는 채로 매 틱 트리를 훑는다.");
        }


        [Test]
        public void SettingADefinitionAfterAssemblyIsRejected()
        {
            var unit = CreateUnit();
            _definition = UnitDefinition.CreateRuntime("소총병", 250);
            unit.SetDefinition(_definition);
            unit.InitializeUnit();

            var replacement = UnitDefinition.CreateRuntime("교체", 500);
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("TacticalUnit"));
                unit.SetDefinition(replacement);

                Assert.That(unit.Definition, Is.SameAs(_definition));
            }
            finally
            {
                Object.DestroyImmediate(replacement);
            }
        }

        private TacticalUnit CreateUnit()
        {
            _unitObject = new GameObject("Unit");
            return _unitObject.AddComponent<TacticalUnit>();
        }

        /// <summary>순차 자리 하나만 담은, 어느 유닛으로도 지을 수 있는 가장 작은 트리 에셋을 만든다.</summary>
        private static BehaviourTreeAsset MinimalTreeAsset()
        {
            var asset = ScriptableObject.CreateInstance<BehaviourTreeAsset>();
            SetPrivate(asset, "nodes", new System.Collections.Generic.List<HS.Framework.AI.Behaviour.BehaviourNodeDefinition>
            {
                new HS.Framework.AI.Behaviour.SequenceDefinition()
            });
            SetPrivate(asset, "parents", new System.Collections.Generic.List<int> { BehaviourTreeAsset.NoParent });
            return asset;
        }

        /// <summary>편집기가 채워 줄 직렬화 필드를 검사에서 채운다. 필드가 없으면 검사가 붉어진다.</summary>
        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, $"{target.GetType().Name}에 '{field}' 필드가 없다. 이름이 바뀌었다면 이 검사도 함께 고쳐야 한다.");
            info.SetValue(target, value);
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
