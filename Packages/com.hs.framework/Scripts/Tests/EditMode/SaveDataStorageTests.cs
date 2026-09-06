using System;
using System.IO;
using HS.Framework.Persistence;
using HS.Framework.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>파일 저장소 백엔드의 라운드트립과 SaveGameSlot 백엔드 주입을 검증한다.</summary>
    public sealed class SaveDataStorageTests
    {
        /// <summary>테스트마다 새로 만드는 임시 루트 디렉터리이다.</summary>
        private string _rootDirectory;

        [SetUp]
        public void SetUp()
        {
            _rootDirectory = Path.Combine(Path.GetTempPath(), "HSFrameworkTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, true);
            }
        }

        [Test]
        public void WriteThenReadRoundTripsValue()
        {
            var storage = new FileSaveDataStorage(_rootDirectory);

            storage.Write("slot", "{\"score\":1}");

            Assert.That(storage.Exists("slot"), Is.True);
            Assert.That(storage.TryRead("slot", out var value), Is.True);
            Assert.That(value, Is.EqualTo("{\"score\":1}"));
        }

        [Test]
        public void WriteOverwritesExistingValueWithoutTempResidue()
        {
            var storage = new FileSaveDataStorage(_rootDirectory);

            storage.Write("slot", "first");
            storage.Write("slot", "second");

            Assert.That(storage.TryRead("slot", out var value), Is.True);
            Assert.That(value, Is.EqualTo("second"));
            Assert.That(Directory.GetFiles(_rootDirectory, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void KeyWithFileNameUnsafeCharactersRoundTrips()
        {
            var storage = new FileSaveDataStorage(_rootDirectory);
            const string key = "save/slot:with*unsafe?chars";

            storage.Write(key, "value");

            Assert.That(storage.TryRead(key, out var value), Is.True);
            Assert.That(value, Is.EqualTo("value"));
        }

        [Test]
        public void CaseDistinctKeysRoundTripAndDeleteIndependently()
        {
            var storage = new FileSaveDataStorage(_rootDirectory);
            storage.Write("Player", "first");
            storage.Write("player", "second");

            Assert.That(storage.TryRead("Player", out var upperValue), Is.True);
            Assert.That(upperValue, Is.EqualTo("first"));
            Assert.That(storage.TryRead("player", out var lowerValue), Is.True);
            Assert.That(lowerValue, Is.EqualTo("second"));

            storage.Delete("Player");

            Assert.That(storage.Exists("Player"), Is.False);
            Assert.That(storage.TryRead("player", out lowerValue), Is.True);
            Assert.That(lowerValue, Is.EqualTo("second"));
        }

        [TestCase("CON")]
        [TestCase("con")]
        [TestCase("../저장/Player")]
        [TestCase("%0050layer")]
        public void EncodedKeysAvoidReservedNamesAndPathTraversal(string key)
        {
            var storage = new FileSaveDataStorage(_rootDirectory);
            storage.Write(key, "saved");

            Assert.That(storage.TryRead(key, out var value), Is.True);
            Assert.That(value, Is.EqualTo("saved"));
            Assert.That(Directory.GetFiles(_rootDirectory, "*.json", SearchOption.AllDirectories), Has.Length.EqualTo(1));
        }

        [Test]
        public void ExistingLegacySaveRemainsReadableAndIsNotClaimedByAnotherCase()
        {
            Directory.CreateDirectory(_rootDirectory);
            var legacyPath = Path.Combine(_rootDirectory, "Player.json");
            File.WriteAllText(legacyPath, "legacy");
            var storage = new FileSaveDataStorage(_rootDirectory);

            Assert.That(storage.Exists("Player"), Is.True);
            Assert.That(storage.TryRead("Player", out var value), Is.True);
            Assert.That(value, Is.EqualTo("legacy"));
            Assert.That(storage.Exists("player"), Is.False);
            storage.Write("player", "another key");

            Assert.That(storage.TryRead("Player", out value), Is.True);
            Assert.That(value, Is.EqualTo("legacy"));
            Assert.That(File.ReadAllText(legacyPath), Is.EqualTo("legacy"));
        }

        [Test]
        public void NewWriteTakesPrecedenceOverLegacyAndDeleteRemovesBothFormats()
        {
            Directory.CreateDirectory(_rootDirectory);
            var legacyPath = Path.Combine(_rootDirectory, "Player.json");
            File.WriteAllText(legacyPath, "legacy");
            var storage = new FileSaveDataStorage(_rootDirectory);

            storage.Write("Player", "updated");

            Assert.That(storage.TryRead("Player", out var value), Is.True);
            Assert.That(value, Is.EqualTo("updated"));
            Assert.That(File.ReadAllText(legacyPath), Is.EqualTo("legacy"), "이전 저장본은 새 쓰기로 손상되지 않는다.");

            storage.Delete("Player");

            Assert.That(storage.TryRead("Player", out _), Is.False, "삭제 뒤 이전 형식의 값이 되살아나면 안 된다.");
            Assert.That(File.Exists(legacyPath), Is.False);
        }

        [Test]
        public void FailedMigrationWritePreservesTheLegacySave()
        {
            Directory.CreateDirectory(_rootDirectory);
            File.WriteAllText(Path.Combine(_rootDirectory, "Player.json"), "legacy");
            File.WriteAllText(Path.Combine(_rootDirectory, "v2"), "blocks the new directory");
            var storage = new FileSaveDataStorage(_rootDirectory);

            Assert.That(() => storage.Write("Player", "updated"), Throws.Exception);

            Assert.That(storage.TryRead("Player", out var value), Is.True);
            Assert.That(value, Is.EqualTo("legacy"));
        }

        [Test]
        public void TryReadReturnsFalseWhenMissing()
        {
            var storage = new FileSaveDataStorage(_rootDirectory);

            Assert.That(storage.Exists("missing"), Is.False);
            Assert.That(storage.TryRead("missing", out var value), Is.False);
            Assert.That(value, Is.Null);
        }

        [Test]
        public void DeleteRemovesEntry()
        {
            var storage = new FileSaveDataStorage(_rootDirectory);
            storage.Write("slot", "value");

            storage.Delete("slot");

            Assert.That(storage.Exists("slot"), Is.False);
        }

        [Test]
        public void SaveGameSlotRoundTripsThroughFileStorage()
        {
            var writer = new FileTestSaveGameSlot(_rootDirectory);
            writer.GetOrCreate(2).SetScore(7);
            writer.Save();

            var reader = new FileTestSaveGameSlot(_rootDirectory);

            Assert.That(reader.Load(), Is.True);
            Assert.That(reader.TryGet(2, out var saveGame), Is.True);
            Assert.That(saveGame.Score, Is.EqualTo(7));
        }

        /// <summary>파일 백엔드를 주입해 사용하는 테스트용 저장 슬롯이다.</summary>
        private sealed class FileTestSaveGameSlot : SaveGameSlot<FileTestSaveGame>
        {
            public FileTestSaveGameSlot(string rootDirectory)
                : base(
                    "FileSlotTest",
                    new TestPublisher<SaveGameLoadedEvent>(),
                    new TestPublisher<SaveGameSavedEvent>(),
                    1,
                    new FileSaveDataStorage(rootDirectory))
            {
            }
        }

        /// <summary>점수 하나를 보관하는 테스트용 저장 데이터이다.</summary>
        private sealed class FileTestSaveGame : SaveGame
        {
            [SerializeField] private int score;

            public int Score => score;

            public void SetScore(int value)
            {
                score = value;
            }
        }

    }
}
