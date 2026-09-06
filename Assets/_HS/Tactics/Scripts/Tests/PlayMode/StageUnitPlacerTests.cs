using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Battlefield;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.PlayMode
{
    /// <summary>
    /// <see cref="StageUnitPlacer"/>가 배치 리스트를 실제 스포너로 세우는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>보는 것은 배치기의 계약이다</b> — 항목마다 스포너를 한 번 부르는지, 위치·진영·회전이 항목과 인자 그대로
    /// 유닛에 닿는지, 세우지 못한 항목을 건너뛰고 나머지를 계속 세우는지, 결과가 세운 순서로 짝지어지는지,
    /// 그리고 정의·진영이 조립 뒤에 뒤늦게 닿는 일이 없는지. 의존성 주입이 계층에 닿는지는 이 검사의 범위 밖이다.
    /// </para>
    /// <para>
    /// <b>resolver는 null로 넘긴다.</b> 스포너는 컨테이너를 받으면 계층 전체에 주입하는데, 유닛 프리팹의 요구 컴포넌트들이
    /// <c>UnitProgressionService</c>·<c>IBattleUnitRegistry</c>·<c>ISubscriber&lt;DeathEvent&gt;</c>까지 주입받으려 하므로
    /// 빈 컨테이너로는 예외가 난다. 주입 없이 세우면 스포너가 스포너마다 한 번 경고하며, 그 경고를 그대로 기대한다.
    /// </para>
    /// <para>
    /// <b>프리팹 대역은 비활성으로 만든다.</b> PlayMode에서는 활성 오브젝트에 <see cref="TacticalUnit"/>을 붙이는 순간
    /// Awake가 돌아 정의 없이 조립된다. 먼저 꺼 둔 오브젝트에 붙이면 조립이 미뤄지고, 스포너는 비활성으로 저장된
    /// 뿌리도 자리를 맞춘 뒤 활성화하므로 조립은 스폰 자리에서 한 번, 정의와 진영이 들어간 채로 일어난다.
    /// 대역 정의에는 어빌리티 집합이, 대역 프리팹에는 행동 트리 에셋이 없어 조립이 그 둘을 유닛마다 경고한다.
    /// </para>
    /// <para>
    /// <b>스폰 동안의 로그를 직접 받아 둔다.</b> 기대하지 않은 로그가 하나라도 있으면 실패하는 단언은 이 검사와 무관한
    /// 컴포넌트가 남기는 로그에도 넘어져 원인을 가리키지 못한다. 그래서 로그를 받아 두고 특정 문구만 찾는다:
    /// 「이미 조립되어」는 정의·진행이 조립 뒤에 닿았을 때 유닛이 남기는 문구라 스폰 순서가 어긋났음을 뜻하고,
    /// 「[UnitSpawner]」는 스포너가 불렸다는 흔적이라 배치기가 스포너를 부르지 않고 걸러야 하는 항목을 가른다.
    /// </para>
    /// </remarks>
    public sealed class StageUnitPlacerTests
    {
        private const float Tolerance = 1e-4f;

        /// <summary>검사가 적으로 쓰는 진영이다. 프로덕션에 적 진영 값이 정해진 곳은 없고, 검사들의 관례를 따른다.</summary>
        private static readonly TeamId EnemyTeam = new(2);

        private readonly List<Object> _created = new();
        private readonly List<string> _capturedLogs = new();
        private StageUnitPlacer _placer;
        private Transform _unitParent;

        [SetUp]
        public void SetUp()
        {
            var parentObject = new GameObject("EnemyUnits");
            _created.Add(parentObject);
            _unitParent = parentObject.transform;
            _placer = new StageUnitPlacer(_unitParent);

            _capturedLogs.Clear();
            Application.logMessageReceived += CaptureLog;
        }

        /// <summary>
        /// 만든 것을 그 자리에서 지운다. 스폰된 유닛은 부모 아래에 있으므로 부모와 함께 사라진다.
        /// 지우기 전에 꺼서 수명주기가 더는 돌지 않게 한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= CaptureLog;

            _placer?.Dispose();
            _placer = null;

            foreach (var created in _created)
            {
                if (created == null)
                {
                    continue;
                }

                if (created is GameObject createdObject)
                {
                    createdObject.SetActive(false);
                }

                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void TwoPlacementsBecomeTwoUnitsAtTheirPositionsOnTheGivenTeamInOrder()
        {
            var definition = CreateDefinition(CreateUnitTemplate());
            var first = new UnitPlacement(definition, 1, new Vector3(2f, 0.1f, -40f));
            var second = new UnitPlacement(definition, 2, new Vector3(-2f, 0.1f, -40f));
            var stage = Track(StageData.CreateRuntime(84f, first, second));

            ExpectSpawnerWithoutResolverWarning();
            ExpectUnitAssemblyWarnings();
            ExpectUnitAssemblyWarnings();
            var placed = _placer.Place(stage, EnemyTeam, Quaternion.identity, resolver: null);

            Assert.That(placed.Count, Is.EqualTo(2), "배치 항목 둘이면 유닛 둘이어야 한다.");
            Assert.That(placed[0].Placement, Is.SameAs(first), "첫 결과는 첫 항목과 짝지어져야 한다.");
            Assert.That(placed[1].Placement, Is.SameAs(second), "두 번째 결과는 두 번째 항목과 짝지어져야 한다.");
            Assert.That(placed[0].Unit, Is.Not.Null);
            Assert.That(placed[1].Unit, Is.Not.Null);
            Assert.That(placed[0].Unit, Is.Not.SameAs(placed[1].Unit), "항목마다 다른 유닛이 서야 한다.");

            for (var index = 0; index < placed.Count; index++)
            {
                var unit = placed[index].Unit;
                var placement = placed[index].Placement;
                Assert.That(
                    Vector3.Distance(unit.transform.position, placement.Position),
                    Is.LessThan(Tolerance),
                    $"{index}번 유닛의 자리는 {unit.transform.position}이고 항목의 자리는 {placement.Position}이다.");
                Assert.That(unit.transform.parent, Is.SameAs(_unitParent), $"{index}번 유닛이 넘긴 부모 아래에 있지 않다.");
                Assert.That(unit.Team, Is.Not.Null, $"{index}번 유닛의 진영 구성요소가 없다.");
                Assert.That(unit.Team.TeamId, Is.EqualTo(EnemyTeam), $"{index}번 유닛의 진영은 {unit.Team.TeamId}이다.");
            }

            AssertNothingWasConfiguredAfterAssembly();
        }

        [Test]
        public void APlacementWithoutADefinitionIsSkippedAndTheRestAreStillPlaced()
        {
            var definition = CreateDefinition(CreateUnitTemplate());
            var withoutDefinition = new UnitPlacement(null, 1, new Vector3(10f, 0f, 0f));
            var valid = new UnitPlacement(definition, 1, new Vector3(20f, 0f, 0f));
            var placements = new List<UnitPlacement> { withoutDefinition, valid };

            LogAssert.Expect(LogType.Warning, new Regex("유닛 정의가 없어 건너뛴다"));
            ExpectSpawnerWithoutResolverWarning();
            ExpectUnitAssemblyWarnings();
            var placed = _placer.Place(placements, EnemyTeam, Quaternion.identity, resolver: null);

            Assert.That(placed.Count, Is.EqualTo(1), "정의 없는 항목은 빠지고 나머지 하나만 서야 한다.");
            Assert.That(placed[0].Placement, Is.SameAs(valid), "남은 결과는 정의가 있는 항목과 짝지어져야 한다.");
            Assert.That(
                Vector3.Distance(placed[0].Unit.transform.position, valid.Position),
                Is.LessThan(Tolerance),
                $"유닛의 자리는 {placed[0].Unit.transform.position}이고 항목의 자리는 {valid.Position}이다.");

            // 정의 없는 항목은 배치기가 걸러야 하므로, 스포너가 정의 없음을 경고한 흔적이 있으면 안 된다.
            AssertNoCapturedLogContains("[UnitSpawner] 유닛 정의가 없어");
        }

        [Test]
        public void TheSpawnRotationIsAppliedToThePlacedUnit()
        {
            var definition = CreateDefinition(CreateUnitTemplate());
            var placements = new List<UnitPlacement> { new(definition, 1, new Vector3(5f, 0f, 5f)) };
            var rotation = Quaternion.Euler(0f, 180f, 0f);

            ExpectSpawnerWithoutResolverWarning();
            ExpectUnitAssemblyWarnings();
            var placed = _placer.Place(placements, EnemyTeam, rotation, resolver: null);

            Assert.That(placed.Count, Is.EqualTo(1));
            Assert.That(
                Quaternion.Angle(placed[0].Unit.transform.rotation, rotation),
                Is.LessThan(0.01f),
                $"유닛의 회전은 {placed[0].Unit.transform.rotation.eulerAngles}이고 넘긴 회전은 {rotation.eulerAngles}이다.");
        }

        [Test]
        public void APlacementWhoseDefinitionHasNoPrefabIsSkippedWithoutSpawning()
        {
            var definitionWithoutPrefab = CreateDefinition(null);
            var placements = new List<UnitPlacement> { new(definitionWithoutPrefab, 1, Vector3.zero) };

            LogAssert.Expect(LogType.Warning, new Regex("프리팹이 없어 건너뛴다"));
            var placed = _placer.Place(placements, EnemyTeam, Quaternion.identity, resolver: null);

            Assert.That(placed.Count, Is.EqualTo(0), "프리팹 없는 정의로는 아무것도 서지 않아야 한다.");
            // 배치기가 걸렀으므로 스포너는 불리지 않았어야 한다 — 스포너가 남긴 로그가 하나라도 있으면 걸러지지 않은 것이다.
            AssertNoCapturedLogContains("[UnitSpawner]");
        }

        [Test]
        public void APlacementTheSpawnerCannotBuildIsReportedAndSkipped()
        {
            // TacticalUnit이 없는 프리팹은 배치기의 사전 확인은 지나지만 스포너가 세우지 못한다.
            var template = new GameObject("NotAUnit");
            _created.Add(template);
            template.SetActive(false);
            var definition = CreateDefinition(template);
            var placements = new List<UnitPlacement> { new(definition, 1, new Vector3(3f, 0f, 3f)) };

            LogAssert.Expect(LogType.Warning, new Regex("TacticalUnit이 없어"));
            LogAssert.Expect(LogType.Warning, new Regex("세우지 못했다"));
            var placed = _placer.Place(placements, EnemyTeam, Quaternion.identity, resolver: null);

            Assert.That(placed.Count, Is.EqualTo(0), "스포너가 세우지 못한 항목은 결과에 들어가지 않아야 한다.");
        }

        /// <summary>
        /// 비활성으로 저장된 프리팹처럼, 꺼진 채 <see cref="TacticalUnit"/>만 붙인 대역을 만든다.
        /// 요구 컴포넌트(진영 구성요소 등)는 붙이는 순간 함께 붙는다.
        /// </summary>
        private GameObject CreateUnitTemplate()
        {
            var template = new GameObject("UnitTemplate");
            _created.Add(template);
            template.SetActive(false);
            template.AddComponent<TacticalUnit>();
            return template;
        }

        /// <summary>어빌리티 집합 없이 프리팹만 연결한 대역 정의를 만든다. 프리팹이 null이면 스폰할 수 없는 정의가 된다.</summary>
        private UnitDefinition CreateDefinition(GameObject prefab)
        {
            return Track(UnitDefinition.CreateRuntime("Grunt", 100, unitPrefab: prefab));
        }

        private T Track<T>(T asset) where T : Object
        {
            _created.Add(asset);
            return asset;
        }

        private void CaptureLog(string condition, string stackTrace, LogType type)
        {
            _capturedLogs.Add(condition);
        }

        /// <summary>
        /// 스폰 동안 받은 로그에 정의·진행이 조립 뒤에 닿았다는 경고가 없어야 한다.
        /// 그 경고는 유닛이 정의를 받기 전에 깨어났다는 뜻이며, 스포너가 막아야 하는 순서 어긋남이다.
        /// </summary>
        private void AssertNothingWasConfiguredAfterAssembly()
        {
            AssertNoCapturedLogContains("이미 조립되어");
        }

        /// <summary>스폰 동안 받은 로그에 그 문구가 든 것이 없어야 한다.</summary>
        /// <param name="fragment">있으면 안 되는 문구이다.</param>
        private void AssertNoCapturedLogContains(string fragment)
        {
            var matches = _capturedLogs.FindAll(log => log.Contains(fragment));
            Assert.That(
                matches,
                Is.Empty,
                $"「{fragment}」가 든 로그가 남았다: " + string.Join(" | ", matches));
        }

        /// <summary>컨테이너 없이 세우면 스포너가 스포너마다 한 번 남기는 경고이다. 첫 스폰에서 난다.</summary>
        private static void ExpectSpawnerWithoutResolverWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("컨테이너 없이"));
        }

        /// <summary>
        /// 대역으로 세운 유닛 하나가 조립될 때 남기는 경고 둘이다. 정의에 어빌리티 집합이 없어 체력을 조립하지 않고,
        /// 프리팹에 행동 트리 에셋이 없어 트리를 시작하지 않는다. 조립은 이 순서로 경고한다.
        /// </summary>
        private static void ExpectUnitAssemblyWarnings()
        {
            LogAssert.Expect(LogType.Warning, new Regex("체력 어트리뷰트"));
            LogAssert.Expect(LogType.Warning, new Regex("행동 트리 에셋이 없어"));
        }
    }
}
