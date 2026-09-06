using HS.Framework.Item;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 인벤토리 변경 추적 동작을 검증한다.
    /// </summary>
    public sealed class InventoryTests
    {
        /// <summary>
        /// 실제 수량 변경이 dirty 상태, revision 및 변경 이벤트를 갱신하는지 검증한다.
        /// </summary>
        [Test]
        public void AddMarksInventoryDirtyAndPublishesChange()
        {
            var definition = CreateDefinition(maxStackSize: 10);
            var inventory = new Inventory(capacity: 1);
            InventoryChangedEvent? receivedEvent = null;
            using var subscription = inventory.Changed.Subscribe(change => receivedEvent = change);

            var addedAmount = inventory.Add(new ItemStack(definition, 5));

            Assert.That(addedAmount, Is.EqualTo(5));
            Assert.That(inventory.IsDirty, Is.True);
            Assert.That(inventory.Revision, Is.EqualTo(1));
            Assert.That(receivedEvent?.ChangeType, Is.EqualTo(InventoryChangeType.Added));
            Assert.That(receivedEvent?.Amount, Is.EqualTo(5));
            Object.DestroyImmediate(definition);
        }

        /// <summary>
        /// 저장 이후 새 변경이 발생하면 이전 revision으로 dirty 상태를 해제할 수 없는지 검증한다.
        /// </summary>
        [Test]
        public void AcknowledgeSavedRejectsOutdatedRevision()
        {
            var definition = CreateDefinition(maxStackSize: 10);
            var inventory = new Inventory(capacity: 1);
            inventory.Add(new ItemStack(definition, 5));
            var savedRevision = inventory.Revision;
            inventory.Add(new ItemStack(definition, 1));

            Assert.That(inventory.AcknowledgeSaved(savedRevision), Is.False);
            Assert.That(inventory.IsDirty, Is.True);
            Assert.That(inventory.AcknowledgeSaved(inventory.Revision), Is.True);
            Assert.That(inventory.IsDirty, Is.False);
            Object.DestroyImmediate(definition);
        }

        /// <summary>
        /// Changed가 내부 Subject를 그대로 노출하지 않아 다운캐스트가 차단되는지 검증한다.
        /// </summary>
        [Test]
        public void ChangedDoesNotExposeInternalSubject()
        {
            var inventory = new Inventory(capacity: 1);

            Assert.That(inventory.Changed, Is.Not.InstanceOf<Subject<InventoryChangedEvent>>());
            Assert.That(inventory.Changed, Is.SameAs(inventory.Changed));
        }

        private static TestItemDefinition CreateDefinition(int maxStackSize)
        {
            var definition = ScriptableObject.CreateInstance<TestItemDefinition>();
            definition.Initialize(maxStackSize);
            return definition;
        }

        private sealed class TestItemDefinition : ItemDefinition
        {
            private int _maxStackSize;

            public override string Id => "test-item";
            public override string DisplayName => "Test Item";
            public override int MaxStackSize => _maxStackSize;

            public void Initialize(int maxStackSize)
            {
                _maxStackSize = maxStackSize;
            }
        }
    }
}
