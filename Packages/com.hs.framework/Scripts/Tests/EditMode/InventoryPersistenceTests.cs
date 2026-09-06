using System.Collections.Generic;
using HS.Framework.Item;
using HS.Framework.Persistence;
using HS.Framework.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>인벤토리 캡처·복원 라운드트립과 AcknowledgeSaved 저장 흐름을 검증한다.</summary>
    public sealed class InventoryPersistenceTests
    {
        [Test]
        public void CaptureAggregatesStacksById()
        {
            var potion = CreateDefinition("potion", 2);
            var inventory = new Inventory(capacity: 3);
            inventory.Add(new ItemStack(potion, 3));

            var data = InventorySaveData.Capture(inventory);

            Assert.That(data.entries.Count, Is.EqualTo(1));
            Assert.That(data.entries[0].id, Is.EqualTo("potion"));
            Assert.That(data.entries[0].amount, Is.EqualTo(3));
        }

        [Test]
        public void RestoreRebuildsInventoryFromCatalogAndSkipsUnknownIds()
        {
            var sword = CreateDefinition("sword", 1);
            var potion = CreateDefinition("potion", 10);
            var catalog = ItemCatalog.CreateRuntime(new ItemDefinition[] { sword, potion });
            var data = new InventorySaveData
            {
                entries = new List<InventorySaveEntry>
                {
                    new() { id = "sword", amount = 1 },
                    new() { id = "potion", amount = 5 },
                    new() { id = "deleted-item", amount = 3 }
                }
            };
            var inventory = new Inventory(capacity: 5);
            inventory.Add(new ItemStack(CreateDefinition("stale", 1), 1));

            var restoredCount = InventorySaveData.Restore(inventory, data, catalog);

            Assert.That(restoredCount, Is.EqualTo(2));
            Assert.That(inventory.Contains(sword, 1), Is.True);
            Assert.That(inventory.Contains(potion, 5), Is.True);
            Assert.That(inventory.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void ParticipantRoundTripsThroughOrchestrator()
        {
            var potion = CreateDefinition("potion", 10);
            var catalog = ItemCatalog.CreateRuntime(new ItemDefinition[] { potion });
            var storage = new InMemorySaveDataStorage();

            var sourceInventory = new Inventory(capacity: 3);
            sourceInventory.Add(new ItemStack(potion, 7));
            var saveOrchestrator = new SaveOrchestrator(storage, new TestPublisher<SaveAllCompletedEvent>(), new TestPublisher<LoadAllCompletedEvent>());
            saveOrchestrator.Register(new InventorySaveParticipant(sourceInventory, catalog, "inventory"));
            saveOrchestrator.SaveAll(SaveKind.Manual);

            var targetInventory = new Inventory(capacity: 3);
            var loadOrchestrator = new SaveOrchestrator(storage, new TestPublisher<SaveAllCompletedEvent>(), new TestPublisher<LoadAllCompletedEvent>());
            loadOrchestrator.Register(new InventorySaveParticipant(targetInventory, catalog, "inventory"));
            var restoredCount = loadOrchestrator.LoadAll();

            Assert.That(restoredCount, Is.EqualTo(1));
            Assert.That(targetInventory.Contains(potion, 7), Is.True);
            Assert.That(targetInventory.IsDirty, Is.False);
        }

        [Test]
        public void AutomaticSaveClearsDirtyAndSkipsNextTime()
        {
            var potion = CreateDefinition("potion", 10);
            var catalog = ItemCatalog.CreateRuntime(new ItemDefinition[] { potion });
            var inventory = new Inventory(capacity: 3);
            var orchestrator = new SaveOrchestrator(new InMemorySaveDataStorage(), new TestPublisher<SaveAllCompletedEvent>(), new TestPublisher<LoadAllCompletedEvent>());
            orchestrator.Register(new InventorySaveParticipant(inventory, catalog, "inventory"));

            inventory.Add(new ItemStack(potion, 2));
            Assert.That(inventory.IsDirty, Is.True);

            Assert.That(orchestrator.SaveAll(SaveKind.Automatic), Is.EqualTo(1));
            Assert.That(inventory.IsDirty, Is.False);
            Assert.That(orchestrator.SaveAll(SaveKind.Automatic), Is.EqualTo(0));
        }

        [Test]
        public void AcknowledgeKeepsDirtyWhenInventoryChangedAfterCapture()
        {
            var potion = CreateDefinition("potion", 10);
            var catalog = ItemCatalog.CreateRuntime(new ItemDefinition[] { potion });
            var inventory = new Inventory(capacity: 3);
            inventory.Add(new ItemStack(potion, 1));
            var participant = new InventorySaveParticipant(inventory, catalog, "inventory");

            participant.CaptureState();
            inventory.Add(new ItemStack(potion, 1));
            participant.AcknowledgeSaved();

            Assert.That(inventory.IsDirty, Is.True);
        }

        [Test]
        public void RestoreStateClearsDirty()
        {
            var potion = CreateDefinition("potion", 10);
            var catalog = ItemCatalog.CreateRuntime(new ItemDefinition[] { potion });
            var sourceInventory = new Inventory(capacity: 3);
            sourceInventory.Add(new ItemStack(potion, 4));
            var sourceParticipant = new InventorySaveParticipant(sourceInventory, catalog, "inventory");
            var serializedState = sourceParticipant.CaptureState();

            var targetInventory = new Inventory(capacity: 3);
            var targetParticipant = new InventorySaveParticipant(targetInventory, catalog, "inventory");
            targetParticipant.RestoreState(serializedState);

            Assert.That(targetInventory.Contains(potion, 4), Is.True);
            Assert.That(targetInventory.IsDirty, Is.False);
        }

        private static TestItemDefinition CreateDefinition(string id, int maxStackSize)
        {
            var definition = ScriptableObject.CreateInstance<TestItemDefinition>();
            definition.Initialize(id, maxStackSize);
            return definition;
        }

        /// <summary>Id와 최대 스택 수를 지정할 수 있는 테스트용 아이템 정의이다.</summary>
        private sealed class TestItemDefinition : ItemDefinition
        {
            private string _id;
            private int _maxStackSize;

            public override string Id => _id;
            public override string DisplayName => _id;
            public override int MaxStackSize => _maxStackSize;

            public void Initialize(string id, int maxStackSize)
            {
                _id = id;
                _maxStackSize = maxStackSize;
            }
        }

        /// <summary>메모리 사전에 기록하는 테스트용 저장소 백엔드이다.</summary>
        private sealed class InMemorySaveDataStorage : ISaveDataStorage
        {
            private readonly Dictionary<string, string> _values = new();

            public bool Exists(string key)
            {
                return _values.ContainsKey(key);
            }

            public bool TryRead(string key, out string value)
            {
                return _values.TryGetValue(key, out value);
            }

            public void Write(string key, string value)
            {
                _values[key] = value;
            }

            public void Delete(string key)
            {
                _values.Remove(key);
            }
        }
    }
}
