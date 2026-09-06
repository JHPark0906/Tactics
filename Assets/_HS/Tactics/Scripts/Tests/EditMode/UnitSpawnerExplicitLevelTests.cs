using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Placement;
using HS.Tactics.Progress;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// <see cref="UnitSpawner.Spawn"/>의 선택 인자 <c>explicitLevel</c>이 스폰한 유닛의 레벨로 투영되는지,
    /// 생략하고 육성 진행도 없으면 시작 레벨인지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 스포너는 조립을 마친 뒤 유닛을 돌려주므로 호출자는 조립 전에 끼어들 수 없다. 그래서 명시 레벨은 스폰
    /// 인자로 받아 주입 뒤·활성화 전이라는 창에서 스포너가 넣는다. 이 파일은 그 창이 실제로 레벨을 살리는지 본다.
    /// </para>
    /// <para>
    /// 에디터는 보통의 스크립트를 깨우지 않으므로 활성화만으로는 조립이 돌지 않는다. 검사가
    /// <see cref="TacticalUnit.InitializeUnit"/>을 직접 불러 조립하고, 컨테이너 없이 세운 경고와 조립 중의
    /// 경고(곡선 없음·행동 트리 없음)를 나는 순서대로 기대한다.
    /// </para>
    /// </remarks>
    public sealed class UnitSpawnerExplicitLevelTests
    {
        private readonly List<Object> _createdObjects = new();
        private TestUnitAttributes _attributes;
        private UnitSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _attributes = new TestUnitAttributes();
        }

        [TearDown]
        public void TearDown()
        {
            _spawner?.Dispose();
            _spawner = null;

            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
            _attributes.Dispose();
        }

        [Test]
        public void SpawningWithAnExplicitLevelProjectsThatLevel()
        {
            var definition = CreateDefinition();
            _spawner = new UnitSpawner();
            LogAssert.Expect(LogType.Warning, new Regex("컨테이너 없이"));

            var unit = _spawner.Spawn(definition, new Vector3(2f, 0.1f, -40f), Quaternion.identity, new TeamId(2), null, 3);
            Track(unit);

            // 곡선은 주입에서만 오는데 컨테이너 없이 세웠으므로 곡선 없음 경고가 나고, 행동 트리 에셋도 없다.
            LogAssert.Expect(LogType.Warning, new Regex("레벨 곡선"));
            LogAssert.Expect(LogType.Warning, new Regex("행동 트리 에셋이 없어"));
            unit.InitializeUnit();

            Assert.That(unit.AbilitySystem.System.Attributes.GetBaseValue(_attributes.Level), Is.EqualTo(3f), "스폰 인자로 넘긴 명시 레벨이 어트리뷰트에 적혀야 한다.");
            Assert.That(unit.Team.TeamId.Value, Is.EqualTo(2), "새 선택 인자가 기존 위치 인자의 뜻을 바꾸면 안 된다.");
        }

        [Test]
        public void SpawningWithoutAnExplicitLevelKeepsTheStartingLevel()
        {
            var definition = CreateDefinition();
            _spawner = new UnitSpawner();
            LogAssert.Expect(LogType.Warning, new Regex("컨테이너 없이"));

            var unit = _spawner.Spawn(definition, new Vector3(2f, 0.1f, -40f), Quaternion.identity, new TeamId(2));
            Track(unit);

            // 명시 레벨도 육성 진행도 없으므로 곡선 없음 경고는 나지 않고, 행동 트리 에셋만 없다.
            LogAssert.Expect(LogType.Warning, new Regex("행동 트리 에셋이 없어"));
            unit.InitializeUnit();

            Assert.That(unit.AbilitySystem.System.Attributes.GetBaseValue(_attributes.Level), Is.EqualTo(UnitLevelProgress.StartingLevel), "명시 레벨을 생략하면 지금처럼 시작 레벨이어야 한다.");
        }

        /// <summary>
        /// 어트리뷰트 묶음을 갖춘 정의에 활성 루트로 저장된 프리팹처럼 만든 유닛 오브젝트를 붙인다.
        /// 묶음이 있어야 조립이 체력 어트리뷰트를 찾지 못했다는 경고 없이 레벨을 투영한다.
        /// </summary>
        private UnitDefinition CreateDefinition()
        {
            var prefab = Track(new GameObject("UnitPrefab"));
            prefab.AddComponent<TacticalUnit>();
            return Track(UnitDefinition.CreateRuntime(
                "Grunt",
                100,
                unitPrefab: prefab,
                abilitySet: _attributes.CreateAbilitySet(),
                id: "Grunt"));
        }

        /// <summary>스폰된 유닛이 실제로 세워졌는지 확인하고 정리 목록에 등록한다.</summary>
        private void Track(TacticalUnit unit)
        {
            Assert.That(unit, Is.Not.Null, "유닛이 세워지지 않았다.");
            _createdObjects.Add(unit.gameObject);
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
