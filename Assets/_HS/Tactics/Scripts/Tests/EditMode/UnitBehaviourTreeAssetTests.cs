using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.BehaviourTree;
using HS.Framework.Ability.Tags;
using HS.Framework.Character;
using HS.Framework.Foundation.Collections;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Behaviour;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 정본 유닛 행동 트리 에셋이 읽히고, 그 모양이 실제로 전진을 끊고 추격으로 넘어가는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 트리가 있는 까닭은 하나다. <b>전진 중에 대상이 나타나면 전진을 끊고 추격한다.</b> 조건 자리가
    /// 뒤엣 형제를 끊는 범위로 걸려 있고, 트리가 끊기를 실행 앞에서 적용해야 그것이 된다. 어느 한쪽이
    /// 빠지면 유닛은 전진 한 걸음이 끝날 때까지 적을 무시한다.
    /// </para>
    /// <para>
    /// 에셋을 실제로 읽어 모든 노드가 만들어지는지도 확인한다.
    /// 자리 하나가 빠지면 그 아래가 통째로 사라지는데 그것은 컴파일로 잡히지 않는다.
    /// </para>
    /// <para>
    /// <b>대상은 탐지기가 찾게 한다.</b> 트리의 뿌리는 적을 다시 찾는 서비스이며, 못 찾으면 대상 키를
    /// 지운다. 검사가 손으로 담은 대상은 그 서비스가 도는 순간 지워지므로, 대상은 레지스트리에 등록해
    /// 적대 진영을 갖춘 오브젝트로 세우고 시야 밖에서 안으로 옮겨 나타나게 한다. 서비스가 다시 재는
    /// 간격은 검사에서 흘릴 수 없으므로, 그 서비스가 하는 일은 필요한 자리에서 직접 시킨다.
    /// </para>
    /// </remarks>
    public sealed class UnitBehaviourTreeAssetTests
    {
        /// <summary>정본 에셋의 경로이며, 다시 채우는 메뉴(UnitBehaviourTreeAssetBuilder)와 같은 값이다.</summary>
        private const string AssetPath = "Assets/_HS/Tactics/AI/UnitBehaviourTree.asset";

        /// <summary>검사 유닛의 사거리(미터)이다.</summary>
        private const float AttackRange = 12f;

        /// <summary>탐지기의 기본 시야 거리(20미터) 너머라 보이지 않는 자리이다.</summary>
        private static readonly Vector3 OutOfSight = new(0f, 0f, 30f);

        /// <summary>시야 안이지만 사거리 밖인 자리이다.</summary>
        private static readonly Vector3 InSightOutOfRange = new(0f, 0f, 15f);

        /// <summary>사거리 안인 자리이다.</summary>
        private static readonly Vector3 WithinRange = new(0f, 0f, 5f);

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
            _unitObject = Track(new GameObject("Unit"));
            _targetObject = Track(new GameObject("Target"));
            var targetTeam = _targetObject.AddComponent<TeamMember>();
            targetTeam.SetTeam(new TeamId(2));
            _registry = Track(new GameObject("Registry")).AddComponent<UnitSpatialRegistry>();
            _registry.Register(targetTeam, _targetObject.transform, 0.5f);
            PlaceTarget(OutOfSight);

            _unit = _unitObject.AddComponent<TacticalUnit>();
            _mover = _unitObject.AddComponent<CountingMover>();
            _detector = Ensure<EnemyDetector>();
            _detector.InjectSpatialRegistry(_registry);
            Ensure<GameplayAbilitySystemComponent>();
            _unit.SetDefinition(Track(UnitDefinition.CreateRuntime("소총병", 100, new TeamId(1), 3.5f, attackRange: AttackRange)));
            _unit.InitializeUnit();

            var asset = AssetDatabase.LoadAssetAtPath<BehaviourTreeAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null, $"정본 에셋이 {AssetPath}에 있어야 한다.");
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
            Assert.That(_tree.Count, Is.EqualTo(14), "자리 하나가 빠지면 그 아래가 통째로 사라진다.");

            Assert.That(_tree.Root.Value, Is.TypeOf<DetectEnemyService>());
            Assert.That(Down(0).Value, Is.TypeOf<TargetInRangeService>());
            Assert.That(Down(0, 0).Value, Is.TypeOf<SelectorBehaviour>());

            var hasTarget = (ContextValueConditionBehaviour)Down(0, 0, 0).Value;
            Assert.That(hasTarget.Key, Is.EqualTo(UnitBehaviourKeys.Target));
            Assert.That(hasTarget.AbortScope, Is.EqualTo(BehaviourAbortScope.LowerPriority), "대상 조건은 전진 가지를 끊어야 한다.");
            Assert.That(Down(0, 0, 0, 0).Value, Is.TypeOf<SelectorBehaviour>());

            var inRange = (ContextValueConditionBehaviour)Down(0, 0, 0, 0, 0).Value;
            Assert.That(inRange.Key, Is.EqualTo(UnitBehaviourKeys.TargetInRange));
            Assert.That(inRange.AbortScope, Is.EqualTo(BehaviourAbortScope.LowerPriority), "사거리 조건은 추격을 끊어야 한다.");
            var attack = (ActivateAbilityBehaviour)Down(0, 0, 0, 0, 0, 0).Value;
            Assert.That(attack.AbilityTag, Is.EqualTo(GameplayTag.Parse(UnitAbilityTags.Attack)));

            var maintainCover = (ActivateAbilityBehaviour)Down(0, 0, 0, 0, 1).Value;
            Assert.That(maintainCover.AbilityTag, Is.EqualTo(GameplayTag.Parse(UnitAbilityTags.MaintainCover)),
                "사격 다음, 추격보다는 앞에서 지키던 자리를 다시 살펴야 한다.");
            var takeCover = (ActivateAbilityBehaviour)Down(0, 0, 0, 0, 2).Value;
            Assert.That(takeCover.AbilityTag, Is.EqualTo(GameplayTag.Parse(UnitAbilityTags.TakeCover)),
                "자리를 지키지 못하면 새로 잡을 수 있는지를 추격보다 먼저 본다.");
            Assert.That(Down(0, 0, 0, 0, 3).Value, Is.TypeOf<SpreadChaseTargetBehaviour>());

            Assert.That(Down(0, 0, 1).Value, Is.TypeOf<RefreshAdvanceDirectionService>());
            Assert.That(Down(0, 0, 1, 0).Value, Is.TypeOf<SequenceBehaviour>());
            Assert.That(Down(0, 0, 1, 0, 0).Value, Is.TypeOf<StepAlongDirectionBehaviour>());
            Assert.That(Down(0, 0, 1, 0, 1).Value, Is.TypeOf<MoveToPositionBehaviour>());
        }

        [Test]
        public void AdvancingIsCutOffAndChaseRunsOnTheTickAfterATargetAppears()
        {
            var context = _unit.BehaviourContext;
            var advanceMove = Down(0, 0, 1, 0, 1);
            var chase = Down(0, 0, 0, 0, 3);

            Assert.That(_tree.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            Assert.That(_tree.IsRunning(advanceMove), Is.True, "대상이 없으면 전진한다.");
            Assert.That(_mover.StopCount, Is.EqualTo(0));

            // 대상이 나타나는 것은 탐지 서비스가 다시 찾을 때이며, 그 간격은 검사에서 흘릴 수 없다.
            // 그래서 서비스가 그 자리에서 담을 값을 그대로 담는다 — 아래 사거리 표식과 같은 방법이다.
            // 탐지기는 「누구를 찾았는가」에만 답하고 키에는 쓰지 않으므로, 찾는 것과 담는 것을 나눠 확인한다.
            PlaceTarget(InSightOutOfRange);
            Assert.That(_detector.RefreshTarget(), Is.SameAs(_targetObject.transform), "시야에 든 적을 탐지기가 대상으로 잡아야 한다.");
            context.SetValue(UnitBehaviourKeys.Target, _targetObject.transform);
            Assert.That(_tree.Tick(context), Is.EqualTo(BehaviourStatus.Running));

            Assert.That(_mover.StopCount, Is.EqualTo(1), "대상이 나타난 다음 실행에서 전진이 되돌려져 멈춤을 받아야 한다.");
            Assert.That(_tree.IsRunning(advanceMove), Is.False);
            Assert.That(_tree.IsRunning(chase), Is.True, "전진이 끊긴 자리에서 추격이 돌아야 한다.");
        }

        [Test]
        public void ChaseIsCutOffOnTheTickAfterTheTargetComesIntoRange()
        {
            var context = _unit.BehaviourContext;
            var chase = Down(0, 0, 0, 0, 3);

            // 첫 실행에서는 대상이 시야 밖이라 전진한다. 대상을 시야 안으로 옮긴 뒤, 뿌리의 탐지 서비스가
            // 간격마다 담을 값을 그대로 담는다. 서비스가 다시 돌아도 같은 대상을 다시 담을 뿐 지우지 않는다.
            _tree.Tick(context);
            PlaceTarget(InSightOutOfRange);
            Assert.That(_detector.RefreshTarget(), Is.SameAs(_targetObject.transform), "시야에 든 적을 탐지기가 대상으로 잡아야 한다.");
            context.SetValue(UnitBehaviourKeys.Target, _targetObject.transform);
            _tree.Tick(context);
            Assert.That(_tree.IsRunning(chase), Is.True, "대상이 있고 사거리 밖이면 추격한다.");
            var stopsBeforeTheMark = _mover.StopCount;

            // 사거리 표식은 서비스가 간격마다 재서 담는 값이며 그 간격은 검사에서 흘릴 수 없다.
            // 대상을 사거리 안으로 옮기고, 서비스가 그 자리에서 담을 표식을 그대로 담는다.
            PlaceTarget(WithinRange);
            context.SetValue(UnitBehaviourKeys.TargetInRange, true);
            _tree.Tick(context);

            Assert.That(_mover.StopCount, Is.EqualTo(stopsBeforeTheMark + 1),
                "사거리 안에 든 다음 실행에서 추격이 되돌려져 멈춤을 받아야 한다.");
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

        /// <summary>대상을 그 자리로 옮긴다. 탐지기는 레지스트리의 원-겹침으로 후보를 모은다.</summary>
        /// <param name="position">대상을 세울 세계 좌표이다.</param>
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

        /// <summary>멈춤을 몇 번 받았는지 세는 검사용 이동 구성요소이다. 목적지에는 닿지 않는다.</summary>
        private sealed class CountingMover : MonoBehaviour, ICharacterMover
        {
            public int StopCount { get; private set; }

            public bool HasReachedDestination => false;

            public bool MoveTo(Vector3 destination) => true;

            public void Stop() => StopCount++;
        }
    }
}
