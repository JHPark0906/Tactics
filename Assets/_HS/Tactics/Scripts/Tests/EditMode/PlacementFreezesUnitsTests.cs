using System.Collections.Generic;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Combat;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>배치 단계에는 유닛의 행동 트리가 돌지 않고, 전투가 시작되는 스텝에 처음부터 도는 것을 고정한다.</summary>
    /// <remarks>
    /// <para>
    /// 스폰되는 순간 트리와 이동기가 돌면 플레이어가 놓은 자리에서 유닛이 걸어 나간다. 유닛은 스폰 자체를
    /// 미루지 않고(배치 중에도 보여야 한다) 도는 것만 멈춘다.
    /// </para>
    /// <para>
    /// 「전투가 시작됐는가」는 배치 컨트롤러의 <see cref="UnitPlacementController.IsBattlePhase"/> 한 곳이
    /// 답하고, 유닛은 그것만 묻는다. 배치 컨트롤러가 없는 무대(대부분의 검사가 그렇다)에서는 곧바로 돈다.
    /// </para>
    /// </remarks>
    public sealed class PlacementFreezesUnitsTests
    {
        /// <summary>정본 에셋의 경로이며, 다시 채우는 메뉴(UnitBehaviourTreeAssetBuilder)와 같은 값이다.</summary>
        private const string AssetPath = "Assets/_HS/Tactics/AI/UnitBehaviourTree.asset";

        /// <summary>고정 스텝 하나가 나타내는 시간(초)이다.</summary>
        private const float Step = 0.02f;

        private readonly List<Object> _created = new();
        private UnitPlacementController _placement;

        [SetUp]
        public void SetUp()
        {
            _placement = CreateObject("Placement").AddComponent<UnitPlacementController>();
            _placement.BeginPlacement();
            _placement.Plan.SetArea(new PlacementArea(Vector3.zero, new Vector3(20f, 10f, 20f)));
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
        public void IsBattlePhaseFollowsThePhase()
        {
            var fresh = CreateObject("Fresh").AddComponent<UnitPlacementController>();
            Assert.That(fresh.IsBattlePhase, Is.False, "준비 단계는 전투가 아니다.");
            Assert.That(_placement.IsBattlePhase, Is.False, "배치 중은 전투가 아니다.");

            StartBattle();

            Assert.That(_placement.IsBattlePhase, Is.True);
        }

        [Test]
        public void TheTreeDoesNotTickWhileUnitsAreBeingPlaced()
        {
            var unit = CreateUnit(_placement);

            unit.Runner.Tick(Step);

            Assert.That(unit.Mover.MoveCount, Is.EqualTo(0), "배치 중에는 이동기에 명령이 가지 않아야 한다.");
            Assert.That(unit.Runner.LastStatus, Is.EqualTo(BehaviourStatus.Failure), "트리가 한 번도 돌지 않아야 한다.");
        }

        [Test]
        public void TheTreeTicksOnTheFirstStepAfterBattleBegins()
        {
            var unit = CreateUnit(_placement);
            unit.Runner.Tick(Step);

            StartBattle();
            unit.Runner.Tick(Step);

            Assert.That(unit.Mover.MoveCount, Is.EqualTo(1), "전투가 시작된 첫 스텝에 트리가 이동을 명령해야 한다.");
            Assert.That(unit.Runner.LastStatus, Is.EqualTo(BehaviourStatus.Running));
        }

        [Test]
        public void WithoutAPlacementControllerTheTreeTicksAsBefore()
        {
            var unit = CreateUnit(null);

            unit.Runner.Tick(Step);

            Assert.That(unit.Mover.MoveCount, Is.EqualTo(1), "배치 단계가 없는 무대에서는 조립 즉시 돌아야 한다.");
        }

        [Test]
        public void GoingBackToPlacementStopsTheUnitAndTheNextBattleStartsOver()
        {
            var unit = CreateUnit(_placement);
            StartBattle();
            unit.Runner.Tick(Step);
            Assert.That(unit.Mover.MoveCount, Is.EqualTo(1));
            Assert.That(unit.Mover.StopCount, Is.EqualTo(0));

            _placement.BeginPlacement();
            Assert.That(unit.Mover.StopCount, Is.EqualTo(1), "전투에서 나오면 걷던 자리가 멈춤을 내야 한다.");
            unit.Runner.Tick(Step);
            Assert.That(unit.Mover.MoveCount, Is.EqualTo(1), "다시 배치 중에는 트리가 돌지 않는다.");

            Assert.That(_placement.TryStartBattle(), Is.True);
            unit.Runner.Tick(Step);
            Assert.That(unit.Mover.MoveCount, Is.EqualTo(2), "다음 전투는 처음부터 다시 돈다.");
        }

        /// <remarks>씬에 놓인 유닛은 깨어나 조립을 마친 뒤에 주입을 받는다.</remarks>
        [Test]
        public void AControllerGivenAfterAssemblyStopsATreeThatWasAlreadyRunning()
        {
            var unit = CreateUnit(null);
            unit.Runner.Tick(Step);
            Assert.That(unit.Mover.MoveCount, Is.EqualTo(1));

            unit.Unit.ConfigurePlacement(_placement);
            unit.Runner.Tick(Step);

            Assert.That(unit.Mover.StopCount, Is.EqualTo(1), "전투 전이면 그동안 돌던 것을 되돌려 걷던 자리가 멈춰야 한다.");
            Assert.That(unit.Mover.MoveCount, Is.EqualTo(1), "문이 걸린 뒤에는 돌지 않는다.");
        }

        /// <summary>항목 하나를 배치하고 전투를 시작한다. 배치한 것이 없으면 전투를 시작할 수 없다.</summary>
        private void StartBattle()
        {
            var definition = Track(UnitDefinition.CreateRuntime("소총병", 100));
            Assert.That(
                _placement.Plan.TryPlaceAtCell(definition, 0, out _),
                Is.EqualTo(PlacementResult.Success),
                "검사가 기대는 준비 단계가 실패했다. 0번 칸에 배치 항목을 넣지 못했다.");
            Assert.That(_placement.TryStartBattle(), Is.True, "배치한 것이 있으면 전투가 시작되어야 한다.");
        }

        /// <summary>정본 트리 에셋으로 조립한 유닛을 세운다. 전진 가지가 첫 실행에서 이동을 명령한다.</summary>
        /// <param name="placement">유닛에 줄 배치 컨트롤러이며, 배치 단계가 없는 무대를 흉내 내려면 null이다.</param>
        /// <returns>유닛과 실행기, 명령을 세는 이동 수단이다.</returns>
        private UnitUnderTest CreateUnit(UnitPlacementController placement)
        {
            var unitObject = CreateObject("Unit");
            var unit = unitObject.AddComponent<TacticalUnit>();
            var mover = unitObject.AddComponent<CountingMover>();
            unitObject.AddComponent<EnemyDetector>();
            unit.SetDefinition(Track(UnitDefinition.CreateRuntime("소총병", 100, new TeamId(1))));
            AssignTree(unit);
            unit.ConfigurePlacement(placement);
            unit.InitializeUnit();

            var runner = unitObject.GetComponent<BehaviourTreeRunner>();
            Assert.That(runner.IsInitialized, Is.True, "정본 에셋으로 트리가 지어져야 한다.");
            return new UnitUnderTest(unit, runner, mover);
        }

        /// <summary>유닛의 행동 트리 에셋 칸에 정본 에셋을 넣는다. 칸은 비공개 직렬화 필드라 편집기의 직렬화 경로로 쓴다.</summary>
        /// <param name="unit">에셋을 넣을 유닛이다.</param>
        private static void AssignTree(TacticalUnit unit)
        {
            var asset = AssetDatabase.LoadAssetAtPath<BehaviourTreeAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null, $"정본 에셋이 {AssetPath}에 있어야 한다.");
            var serialized = new SerializedObject(unit);
            serialized.FindProperty("behaviourTree").objectReferenceValue = asset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private GameObject CreateObject(string objectName)
        {
            return Track(new GameObject(objectName));
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        /// <summary>검사가 함께 다루는 유닛과 실행기, 이동 수단이다.</summary>
        private sealed class UnitUnderTest
        {
            public UnitUnderTest(TacticalUnit unit, BehaviourTreeRunner runner, CountingMover mover)
            {
                Unit = unit;
                Runner = runner;
                Mover = mover;
            }

            public TacticalUnit Unit { get; }

            public BehaviourTreeRunner Runner { get; }

            public CountingMover Mover { get; }
        }

        /// <summary>이동과 멈춤 명령을 세는 검사용 이동 구성요소이다. 목적지에는 닿지 않는다.</summary>
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
