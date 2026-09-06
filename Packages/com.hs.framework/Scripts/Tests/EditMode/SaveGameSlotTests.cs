using HS.Framework.Persistence;
using HS.Framework.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>저장 슬롯의 PlayerPrefs 직렬화와 복원을 검증한다.</summary>
    public sealed class SaveGameSlotTests
    {
        private const string PlayerPrefsKey = "HS.Framework.Tests.EditMode.SaveGameSlot";

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void SaveAndLoadRoundTripsSlotData()
        {
            var writer = new TestSaveGameSlot();
            writer.GetOrCreate(3).SetScore(42);
            writer.Save();

            var reader = new TestSaveGameSlot();

            Assert.That(reader.Load(), Is.True);
            Assert.That(reader.TryGet(3, out var saveGame), Is.True);
            Assert.That(saveGame.Score, Is.EqualTo(42));
            Assert.That(saveGame.Version, Is.EqualTo(1));
        }

        private sealed class TestSaveGameSlot : SaveGameSlot<TestSaveGame>
        {
            public TestSaveGameSlot()
                : base(
                    PlayerPrefsKey,
                    new TestPublisher<SaveGameLoadedEvent>(),
                    new TestPublisher<SaveGameSavedEvent>())
            {
            }
        }

        private sealed class TestSaveGame : SaveGame
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
