using System.Collections.Generic;
using HS.Tactics.Character.Movement;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 스폰된 유닛이 깨어나는 순간 이미 제자리에 있는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 활성 루트로 저장된 프리팹은 비활성 임시 부모에서 활성 부모로 옮기는 순간 계층에서 활성이 되어
    /// 그 자리에서 깨어난다. 붙인 뒤에 좌표를 맞추면 깨어날 때 읽는 자리가 임시 부모의 자리가 된다.
    /// 에디터는 보통의 스크립트를 깨우지 않으므로, 항상 실행되는 기록자를 붙여 깨어난 자리를 잡는다.
    /// </para>
    /// <para>
    /// 기록자가 실제로 깨어났는지도 함께 단언한다. 깨어나지 않았는데 자리만 맞으면 검사가 아무것도
    /// 보지 못한 채 통과하기 때문이다.
    /// </para>
    /// </remarks>
    public sealed class UnitSpawnerActivationOrderTests
    {
        private const float Tolerance = 1e-4f;

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
        public void ASpawnedUnitWakesUpAtItsSpawnPosition()
        {
            var definition = CreateDefinition(CreateUnitPrefab());
            _spawner = new UnitSpawner();
            var position = new Vector3(3f, 0f, 7f);
            var rotation = Quaternion.Euler(0f, 90f, 0f);

            var unit = _spawner.Spawn(definition, position, rotation, default);
            Track(unit);

            var recorder = unit.GetComponent<WakePositionRecorder>();
            Assert.That(recorder.HasWoken, Is.True, "기록자가 깨어나지 않았다. 깨어난 자리를 검사할 수 없다.");
            Assert.That(
                Vector3.Distance(recorder.PositionAtWake, position),
                Is.LessThan(Tolerance),
                $"깨어난 자리는 {recorder.PositionAtWake}이고 스폰 자리는 {position}이다.");
            Assert.That(Quaternion.Angle(recorder.RotationAtWake, rotation), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(unit.transform.position, position), Is.LessThan(Tolerance));
            AssertLogicPosition(unit, position);
        }

        [Test]
        public void ASpawnedUnitKeepsItsWorldPositionUnderAnOffsetParent()
        {
            var parent = new GameObject("Units").transform;
            _createdObjects.Add(parent.gameObject);
            parent.position = new Vector3(10f, 0f, -4f);
            parent.rotation = Quaternion.Euler(0f, 45f, 0f);
            var definition = CreateDefinition(CreateUnitPrefab());
            _spawner = new UnitSpawner(parent);
            var position = new Vector3(3f, 0f, 7f);

            var unit = _spawner.Spawn(definition, position, Quaternion.identity, default);
            Track(unit);

            Assert.That(unit.transform.parent, Is.SameAs(parent));
            Assert.That(Vector3.Distance(unit.transform.position, position), Is.LessThan(Tolerance));
            var recorder = unit.GetComponent<WakePositionRecorder>();
            Assert.That(recorder.HasWoken, Is.True, "기록자가 깨어나지 않았다.");
            Assert.That(Vector3.Distance(recorder.PositionAtWake, position), Is.LessThan(Tolerance));
            AssertLogicPosition(unit, position);
        }

        /// <summary>활성 루트로 저장된 프리팹처럼, 활성 상태의 유닛 오브젝트를 만든다.</summary>
        private GameObject CreateUnitPrefab()
        {
            var prefab = new GameObject("UnitPrefab");
            prefab.AddComponent<PlanarCharacterMover>();
            prefab.AddComponent<TacticalUnit>();
            prefab.AddComponent<WakePositionRecorder>();
            _createdObjects.Add(prefab);
            return prefab;
        }

        private static void AssertLogicPosition(TacticalUnit unit, Vector3 position)
        {
            var mover = unit.GetComponent<PlanarCharacterMover>();
            Assert.That(Vector3.Distance(mover.CurrentLogicPosition, position), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(mover.PreviousLogicPosition, position), Is.LessThan(Tolerance));
        }

        private UnitDefinition CreateDefinition(GameObject prefab)
        {
            var definition = UnitDefinition.CreateRuntime("TestUnit", 100, unitPrefab: prefab);
            _createdObjects.Add(definition);
            return definition;
        }

        private void Track(TacticalUnit unit)
        {
            Assert.That(unit, Is.Not.Null, "유닛이 세워지지 않았다.");
            _createdObjects.Add(unit.gameObject);
        }

        /// <summary>
        /// 깨어나는 순간의 자리를 기록한다. 에디터에서도 깨어나도록 항상 실행되며,
        /// 처음 깨어난 자리만 남기고 그 뒤의 활성화는 무시한다.
        /// </summary>
        [ExecuteAlways]
        private sealed class WakePositionRecorder : MonoBehaviour
        {
            [System.NonSerialized]
            private bool _hasWoken;

            public bool HasWoken => _hasWoken;

            public Vector3 PositionAtWake { get; private set; }

            public Quaternion RotationAtWake { get; private set; }

            private void Awake() => Record();

            private void OnEnable() => Record();

            private void Record()
            {
                if (_hasWoken)
                {
                    return;
                }

                _hasWoken = true;
                PositionAtWake = transform.position;
                RotationAtWake = transform.rotation;
            }
        }
    }
}
