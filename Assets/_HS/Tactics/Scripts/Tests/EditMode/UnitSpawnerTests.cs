using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>유닛 스폰이 조립 전에 정의와 진영을 적용하는지 검증한다.</summary>
    public sealed class UnitSpawnerTests
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
        public void SpawnWithoutDefinitionOrPrefabCreatesNothing()
        {
            _spawner = new UnitSpawner();
            var definitionWithoutPrefab = CreateDefinition(null);

            Assert.That(_spawner.Spawn(null, Vector3.zero, Quaternion.identity, default), Is.Null);
            Assert.That(
                _spawner.Spawn(definitionWithoutPrefab, Vector3.zero, Quaternion.identity, default),
                Is.Null);
        }


        private UnitDefinition CreateDefinition(GameObject prefab)
        {
            var definition = UnitDefinition.CreateRuntime("TestUnit", 100, unitPrefab: prefab);
            _createdObjects.Add(definition);
            return definition;
        }

    }
}
