using System.Collections.Generic;
using HS.Framework.Persistence;
using HS.Framework.Settings;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 설정 계층이 PlayerPrefs를 직접 호출하지 않고 <see cref="ISaveDataStorage"/> 백엔드를 거치는지,
    /// 그리고 기본 백엔드가 기존 PlayerPrefs 키와 호환되는지 검증한다.
    /// </summary>
    public sealed class SettingsStorageTests
    {
        /// <summary>그래픽 설정 저장에 사용하는 저장소 키이다.</summary>
        private const string GraphicsKey = "GraphicsSettings";

        /// <summary>테스트 시작 전 PlayerPrefs에 저장돼 있던 그래픽 설정 JSON이며, 없었으면 null이다.</summary>
        private string _savedGraphicsJson;

        [SetUp]
        public void SetUp()
        {
            _savedGraphicsJson = PlayerPrefs.HasKey(GraphicsKey) ? PlayerPrefs.GetString(GraphicsKey) : null;
            ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            ResetAll();
            if (_savedGraphicsJson == null)
            {
                PlayerPrefs.DeleteKey(GraphicsKey);
            }
            else
            {
                PlayerPrefs.SetString(GraphicsKey, _savedGraphicsJson);
            }
        }

        [Test]
        public void DefaultStorageIsPlayerPrefsBackend()
        {
            Assert.That(SettingsStorage.Current, Is.TypeOf<PlayerPrefsSaveDataStorage>());
        }

        [Test]
        public void SettingsAreSavedThroughInjectedStorageInsteadOfPlayerPrefs()
        {
            var storage = new InMemorySaveDataStorage();
            SettingsStorage.Current = storage;

            var settings = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1920, 1080, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);
            settings.Save();

            Assert.That(storage.Exists(GraphicsKey), Is.True, "설정은 주입된 저장소 백엔드에 기록되어야 한다.");
            Assert.That(
                PlayerPrefs.HasKey(GraphicsKey),
                Is.False,
                "백엔드를 대체하면 설정이 PlayerPrefs에 직접 기록되지 않아야 한다.");
        }

        [Test]
        public void SettingsAreLoadedThroughInjectedStorage()
        {
            var storage = new InMemorySaveDataStorage();
            storage.Write(
                GraphicsKey,
                "{\"version\":2,\"resolutionWidth\":1600,\"resolutionHeight\":900,\"preferredRefreshRate\":75," +
                "\"targetFrameRate\":75,\"isVSyncEnabled\":false,\"fullScreenMode\":3,\"qualityLevel\":-1}");
            SettingsStorage.Current = storage;

            var settings = DisplaySettingsService.Load(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60));

            Assert.That(settings.ResolutionWidth, Is.EqualTo(1600));
            Assert.That(settings.ResolutionHeight, Is.EqualTo(900));
        }

        [Test]
        public void DefaultStorageKeepsExistingPlayerPrefsKeyCompatible()
        {
            PlayerPrefs.SetString(
                GraphicsKey,
                "{\"version\":2,\"resolutionWidth\":1440,\"resolutionHeight\":810,\"preferredRefreshRate\":60," +
                "\"targetFrameRate\":60,\"isVSyncEnabled\":false,\"fullScreenMode\":3,\"qualityLevel\":-1}");

            var settings = DisplaySettingsService.Load(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60));

            Assert.That(
                settings.ResolutionWidth,
                Is.EqualTo(1440),
                "기본 백엔드는 이전 버전이 PlayerPrefs에 저장한 설정을 그대로 읽어야 한다.");
            Assert.That(settings.ResolutionHeight, Is.EqualTo(810));
        }

        [Test]
        public void ResetRestoresDefaultStorageBackend()
        {
            SettingsStorage.Current = new InMemorySaveDataStorage();
            Assert.That(SettingsStorage.Current, Is.TypeOf<InMemorySaveDataStorage>());

            SettingsStorage.ResetForTests();

            Assert.That(SettingsStorage.Current, Is.TypeOf<PlayerPrefsSaveDataStorage>());
        }

        /// <summary>테스트 사이에 저장소 백엔드와 설정 서비스 캐시가 새지 않도록 초기화한다.</summary>
        private static void ResetAll()
        {
            SettingsStorage.ResetForTests();
            DisplaySettingsService.ResetCurrentForTests();
            AudioSettingsService.ResetCurrentForTests();
            InputSettingsService.ResetCurrentForTests();
            LocaleSettingsService.ResetCurrentForTests();
            PlayerPrefs.DeleteKey(GraphicsKey);
        }

        /// <summary>테스트에서 파일이나 PlayerPrefs를 건드리지 않고 사용하는 메모리 저장소 백엔드이다.</summary>
        private sealed class InMemorySaveDataStorage : ISaveDataStorage
        {
            /// <summary>키별로 보관 중인 저장 데이터이다.</summary>
            private readonly Dictionary<string, string> _values = new();

            /// <inheritdoc />
            public bool Exists(string key)
            {
                return _values.ContainsKey(key);
            }

            /// <inheritdoc />
            public bool TryRead(string key, out string value)
            {
                return _values.TryGetValue(key, out value);
            }

            /// <inheritdoc />
            public void Write(string key, string value)
            {
                _values[key] = value;
            }

            /// <inheritdoc />
            public void Delete(string key)
            {
                _values.Remove(key);
            }
        }
    }
}
