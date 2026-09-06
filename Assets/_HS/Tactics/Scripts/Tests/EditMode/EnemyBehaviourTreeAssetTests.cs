using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.BehaviourTree;
using HS.Framework.Ability.Tags;
using HS.Framework.Character;
using HS.Framework.Foundation.Collections;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Tests.Support;
using HS.Tactics.Behaviour;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 적 전용 행동 트리 에셋이 읽히고, 표적이 없으면 유닛이 아무것도 하지 않으며,
    /// 표적을 잡으면 추격하고 놓치면 다시 서는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 적은 상대(플레이어 쪽 아군)를 발견하기 전까지 가만히 있는다. 이 에셋은 정본 아군 트리에서 전진 가지를 뺀 것이며,
    /// 표적 조건이 닫히면 뿌리 Selector 에 다른 가지가 없어 실패하고 유닛은 선다. 교전 가지(사격·엄폐 유지·확보·추격)는
    /// 아군 트리와 같은 모양이다. 아군 트리는 그대로 전진한다.
    /// </para>
    /// <para>
    /// 아군 트리 검사와 같은 방식으로 세운다. 실제 에셋에서 모든 노드가 만들어지는지 확인하고, 표적은 탐지기가
    /// 레지스트리에 등록된 적대 진영을 원-겹침으로 찾게 한다. 탐지 서비스의 간격은 검사에서 흘릴 수 없으므로 그 서비스가 하는 일을
    /// 필요한 자리에서 탐지기에 직접 시킨다.
    /// </para>
    /// <para>
    /// <b>놓치면 서는 것은 조건 문이 닫히며 아래를 되돌리기 때문이다.</b> 표적 키가 사라진 다음 실행에서 표적 조건이
    /// 닫히고, 문은 도는 중이던 추격을 처음으로 되돌리며, 추격은 되돌려질 때 이동에 멈춤을 보낸다.
    /// </para>
    /// </remarks>
    public sealed class EnemyBehaviourTreeAssetTests
    {
        /// <summary>적 전용 에셋의 경로이다.</summary>
        private const string AssetPath = "Assets/_HS/Tactics/AI/EnemyBehaviourTree.asset";

        /// <summary>검사 유닛의 사거리(미터)이다.</summary>
        private const float AttackRange = 12f;

        /// <summary>탐지기의 기본 반경(20미터) 너머라 알아채지 못하는 자리이다.</summary>
        private static readonly Vector3 OutOfReach = new(0f, 0f, 30f);

        /// <summary>탐지 반경 안이지만 사거리 밖인 자리이다.</summary>
        private static readonly Vector3 InReachOutOfRange = new(0f, 0f, 15f);

        private readonly List<Object> _created = new();

        private GameObject _unitObject;
        private GameObject _targetObject;
        private TacticalUnit _unit;
        private CountingMover _mover;
        private EnemyDetector _detector;
        private UnitSpatialRegistry _registry;
        private BehaviourTreeInstance _tree;

        [SetUp]
        public void SetUp()
        {
            _unitObject = Track(new GameObject("Enemy"));
            _targetObject = Track(new GameObject("Ally"));
            var targetTeam = _targetObject.AddComponent<TeamMember>();
            targetTeam.SetTeam(new TeamId(1));
            _registry = Track(new GameObject("Registry")).AddComponent<UnitSpatialRegistry>();
            _registry.Register(targetTeam, _targetObject.transform, 0.5f);
            PlaceTarget(OutOfReach);

            _unit = _unitObject.AddComponent<TacticalUnit>();
            _mover = _unitObject.AddComponent<CountingMover>();
            _detector = Ensure<EnemyDetector>();
            _detector.InjectSpatialRegistry(_registry);
            Ensure<GameplayAbilitySystemComponent>();
            _unit.SetDefinition(Track(UnitDefinition.CreateRuntime("소총병", 100, new TeamId(2), 3.5f, attackRange: AttackRange)));
            _unit.InitializeUnit();

            var asset = AssetDatabase.LoadAssetAtPath<BehaviourTreeAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null, $"적 전용 에셋이 {AssetPath}에 있어야 한다.");
            Assert.That(asset.IsValid(out var error), Is.True, error);
            _tree = asset.CreateRuntimeTree(new BehaviourBuildContext(_unitObject, _unit.BehaviourContext));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void EverySlotIsBuiltInTheStoredShape()
        {
            Assert.That(_tree.Count, Is.EqualTo(10), "자리 하나가 빠지면 그 아래가 통째로 사라진다.");

            Assert.That(_tree.Root.Value, Is.TypeOf<DetectEnemyService>());
            Assert.That(Down(0).Value, Is.TypeOf<TargetInRangeService>());
            Assert.That(Down(0, 0).Value, Is.TypeOf<SelectorBehaviour>());
            Assert.That(Down(0, 0).Children.Count, Is.EqualTo(1), "적 트리에는 전진 가지가 없다. 표적 가지 하나뿐이다.");

            var hasTarget = (ContextValueConditionBehaviour)Down(0, 0, 0).Value;
            Assert.That(hasTarget.Key, Is.EqualTo(UnitBehaviourKeys.Target));
            Assert.That(hasTarget.AbortScope, Is.EqualTo(BehaviourAbortScope.LowerPriority));
            Assert.That(Down(0, 0, 0, 0).Value, Is.TypeOf<SelectorBehaviour>());

            var inRange = (ContextValueConditionBehaviour)Down(0, 0, 0, 0, 0).Value;
            Assert.That(inRange.Key, Is.EqualTo(UnitBehaviourKeys.TargetInRange));
            Assert.That(inRange.AbortScope, Is.EqualTo(BehaviourAbortScope.LowerPriority), "사거리 조건은 추격을 끊어야 한다.");
            var attack = (ActivateAbilityBehaviour)Down(0, 0, 0, 0, 0, 0).Value;
            Assert.That(attack.AbilityTag, Is.EqualTo(GameplayTag.Parse(UnitAbilityTags.Attack)));

            var maintainCover = (ActivateAbilityBehaviour)Down(0, 0, 0, 0, 1).Value;
            Assert.That(maintainCover.AbilityTag, Is.EqualTo(GameplayTag.Parse(UnitAbilityTags.MaintainCover)));
            var takeCover = (ActivateAbilityBehaviour)Down(0, 0, 0, 0, 2).Value;
            Assert.That(takeCover.AbilityTag, Is.EqualTo(GameplayTag.Parse(UnitAbilityTags.TakeCover)));
            Assert.That(Down(0, 0, 0, 0, 3).Value, Is.TypeOf<SpreadChaseTargetBehaviour>());
        }

        [Test]
        public void WithoutATargetTheUnitDoesNothing()
        {
            var context = _unit.BehaviourContext;

            for (var tick = 0; tick < 3; tick++)
            {
                Assert.That(_tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure), "표적이 없으면 트리는 실패로 끝나고 유닛은 선다.");
            }

            Assert.That(_mover.MoveCount, Is.Zero, "표적이 없는 동안 이동 명령이 있으면 안 된다.");
            Assert.That(_mover.StopCount, Is.Zero, "돌던 것이 없으므로 멈춤도 없다.");
        }

        [Test]
        public void TheUnitChasesAFoundTargetAndStandsAgainWhenItIsLost()
        {
            var context = _unit.BehaviourContext;
            var chase = Down(0, 0, 0, 0, 3);

            Assert.That(_tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure), "무대 확인: 표적이 반경 밖이면 선다.");

            // 표적이 나타나는 것은 탐지 서비스가 다시 찾을 때이며, 그 간격은 검사에서 흘릴 수 없다.
            // 그래서 탐지기에는 찾게만 하고, 서비스가 그 자리에서 담을 값을 그대로 담는다.
            // 키의 임자는 서비스이므로 찾는 것과 담는 것이 나뉜다.
            PlaceTarget(InReachOutOfRange);
            Assert.That(_detector.RefreshTarget(), Is.SameAs(_targetObject.transform), "반경에 든 상대를 탐지기가 표적으로 잡아야 한다.");
            context.SetValue(UnitBehaviourKeys.Target, _targetObject.transform);
            Assert.That(_tree.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            Assert.That(_tree.IsRunning(chase), Is.True, "표적이 있고 사거리 밖이면 추격한다.");
            Assert.That(_mover.MoveCount, Is.GreaterThan(0), "추격은 이동 명령으로 나타난다.");

            // 표적을 놓치는 것도 같은 길이다. 탐지기가 못 찾으면 서비스가 그 자리에서 키를 지운다.
            PlaceTarget(OutOfReach);
            Assert.That(_detector.RefreshTarget(), Is.Null, "반경 밖으로 나간 상대는 표적에서 빠진다.");
            context.RemoveValue(UnitBehaviourKeys.Target);
            Assert.That(_tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure), "표적을 놓치면 트리는 다시 실패로 끝난다.");

            Assert.That(_tree.IsRunning(chase), Is.False);
            Assert.That(_mover.StopCount, Is.EqualTo(1), "놓친 다음 실행에서 추격이 되돌려져 멈춤을 받아야 한다.");
            var movesAfterLoss = _mover.MoveCount;
            _tree.Tick(context);
            Assert.That(_mover.MoveCount, Is.EqualTo(movesAfterLoss), "놓친 뒤에는 이동 명령이 더 없다. 유닛은 선다.");
        }

        /// <summary>뿌리에서 자식 번호를 따라 내려간 자리를 돌려준다.</summary>
        /// <param name="path">뿌리부터 차례로 고를 자식 번호이다.</param>
        /// <returns>그 자리이다.</returns>
        private TreeNode<IBehaviour> Down(params int[] path)
        {
            var node = _tree.Root;
            foreach (var index in path)
            {
                Assert.That(node.Children.Count, Is.GreaterThan(index), $"{node.Value.GetType().Name} 아래에 {index}번째 자식이 없다.");
                node = node.Children[index];
            }

            return node;
        }

        /// <summary>표적을 그 자리로 옮긴다. 탐지기는 레지스트리의 원-겹침으로 후보를 모은다.</summary>
        /// <param name="position">표적을 세울 세계 좌표이다.</param>
        private void PlaceTarget(Vector3 position)
        {
            _targetObject.transform.position = position;
        }

        /// <summary>유닛에 그 구성요소가 없으면 붙이고, 있으면 그것을 돌려준다.</summary>
        /// <typeparam name="TComponent">확보할 구성요소이다.</typeparam>
        /// <returns>유닛에 붙어 있는 구성요소이다.</returns>
        private TComponent Ensure<TComponent>() where TComponent : Component
        {
            return _unitObject.TryGetComponent<TComponent>(out var existing)
                ? existing
                : _unitObject.AddComponent<TComponent>();
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        /// <summary>이동 명령과 멈춤을 몇 번 받았는지 세는 검사용 이동 구성요소이다. 목적지에는 닿지 않는다.</summary>
        private sealed class CountingMover : MonoBehaviour, ICharacterMover
        {
            public int MoveCount { get; private set; }

            public int StopCount { get; private set; }

            public bool HasReachedDestination => false;

            public bool MoveTo(Vector3 destination)
            {
                MoveCount++;
                return true;
            }

            public void Stop() => StopCount++;
        }
    }
}
