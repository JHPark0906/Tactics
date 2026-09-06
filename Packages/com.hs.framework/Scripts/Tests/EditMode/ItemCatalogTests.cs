using HS.Framework.Item;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>아이템 카탈로그의 Id 룩업과 등록 목록 검증을 확인한다.</summary>
    public sealed class ItemCatalogTests
    {
        [Test]
        public void TryGetByIdFindsRegisteredDefinition()
        {
            var sword = CreateDefinition("sword", 1);
            var potion = CreateDefinition("potion", 10);
            var catalog = ItemCatalog.CreateRuntime(new ItemDefinition[] { sword, potion });

            Assert.That(catalog.TryGetById("potion", out var definition), Is.True);
            Assert.That(definition, Is.SameAs(potion));
        }

        [Test]
        public void TryGetByIdReturnsFalseForUnknownOrEmptyId()
        {
            var catalog = ItemCatalog.CreateRuntime(new ItemDefinition[] { CreateDefinition("sword", 1) });

            Assert.That(catalog.TryGetById("unknown", out _), Is.False);
            Assert.That(catalog.TryGetById(null, out _), Is.False);
            Assert.That(catalog.TryGetById("  ", out _), Is.False);
        }

        [Test]
        public void TryValidatePassesForUniqueIds()
        {
            var catalog = ItemCatalog.CreateRuntime(new ItemDefinition[]
            {
                CreateDefinition("sword", 1),
                CreateDefinition("potion", 10)
            });

            Assert.That(catalog.TryValidate(out var errorMessage), Is.True);
            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public void TryValidateDetectsDuplicateId()
        {
            var catalog = ItemCatalog.CreateRuntime(new ItemDefinition[]
            {
                CreateDefinition("sword", 1),
                CreateDefinition("sword", 1)
            });

            Assert.That(catalog.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Does.Contain("sword"));
        }

        [Test]
        public void TryValidateDetectsMissingEntry()
        {
            var catalog = ItemCatalog.CreateRuntime(new ItemDefinition[] { null });

            Assert.That(catalog.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Is.Not.Null);
        }

        [Test]
        public void TryValidateDetectsEmptyId()
        {
            var catalog = ItemCatalog.CreateRuntime(new ItemDefinition[] { CreateDefinition("", 1) });

            Assert.That(catalog.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Is.Not.Null);
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
    }
}
