using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>컨테이너 없이 스폰할 때 스포너가 그 사실을 스포너마다 한 번만 경고하는지 검증한다.</summary>
    public sealed class UnitSpawnerResolverWarningTests
    {
        private readonly List<Object> _createdObjects = new();
        private UnitSpawner _spawner;

        [TearDown]
        public void TearDown()
        {
            _spawner?.Dispose();
            _spawner = null;

            foreach (var created in _createdObjects)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void SpawningWithoutResolverWarnsOnlyOncePerSpawner()
        {
            var definition = CreateDefinition(CreateUnitPrefab());
            _spawner = new UnitSpawner();
            LogAssert.Expect(LogType.Warning, new Regex("UnitSpawner.*컨테이너 없이"));

            var first = _spawner.Spawn(definition, Vector3.zero, Quaternion.identity, default);
            var second = _spawner.Spawn(definition, Vector3.zero, Quaternion.identity, default);
            Track(first);
            Track(second);

            Assert.That(first, Is.Not.Null, "컨테이너가 없어도 유닛은 세워져야 한다.");
            Assert.That(second, Is.Not.Null, "두 번째 스폰도 세워져야 한다.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EachSpawnerWarnsOnItsOwn()
        {
            var definition = CreateDefinition(CreateUnitPrefab());
            _spawner = new UnitSpawner();
            var otherSpawner = new UnitSpawner();
            LogAssert.Expect(LogType.Warning, new Regex("UnitSpawner.*컨테이너 없이"));
            LogAssert.Expect(LogType.Warning, new Regex("UnitSpawner.*컨테이너 없이"));

            try
            {
                Track(_spawner.Spawn(definition, Vector3.zero, Quaternion.identity, default));
                Track(otherSpawner.Spawn(definition, Vector3.zero, Quaternion.identity, default));
            }
            finally
            {
                otherSpawner.Dispose();
            }

            LogAssert.NoUnexpectedReceived();
        }

        private GameObject CreateUnitPrefab()
        {
            var prefab = new GameObject("UnitPrefab");
            prefab.AddComponent<TacticalUnit>();
            _createdObjects.Add(prefab);
            return prefab;
        }

        private UnitDefinition CreateDefinition(GameObject prefab)
        {
            var definition = UnitDefinition.CreateRuntime("TestUnit", 100, unitPrefab: prefab);
            _createdObjects.Add(definition);
            return definition;
        }

        private void Track(TacticalUnit unit)
        {
            if (unit != null)
            {
                _createdObjects.Add(unit.gameObject);
            }
        }
    }
}
