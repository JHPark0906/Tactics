using HS.Framework.Settings;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 설정 서비스 4형제가 초기화 시 캐시 인스턴스를 교체하지 않고 제자리에서 갱신하는지 검증한다.
    /// 초기화 전에 <c>Current</c>를 캡처한 소비자가 스테일 인스턴스를 쥐지 않는 것이 이 계약의 핵심이다.
    /// </summary>
    public sealed class SettingsServiceLifecycleTests
    {
        /// <summary>테스트 시작 전 PlayerPrefs에 저장돼 있던 그래픽 설정 JSON이며, 없었으면 null이다.</summary>
        private string _savedGraphicsJson;

        /// <summary>테스트 시작 전 PlayerPrefs에 저장돼 있던 오디오 설정 JSON이며, 없었으면 null이다.</summary>
        private string _savedAudioJson;

        /// <summary>테스트 시작 전 PlayerPrefs에 저장돼 있던 입력 설정 JSON이며, 없었으면 null이다.</summary>
        private string _savedInputJson;

        [SetUp]
        public void SetUp()
        {
            _savedGraphicsJson = ReadSavedJson("GraphicsSettings");
            _savedAudioJson = ReadSavedJson("AudioSettings");
            _savedInputJson = ReadSavedJson("InputSettings");
            ResetAllServices();
        }

        [TearDown]
        public void TearDown()
        {
            ResetAllServices();
            RestoreSavedJson("GraphicsSettings", _savedGraphicsJson);
            RestoreSavedJson("AudioSettings", _savedAudioJson);
            RestoreSavedJson("InputSettings", _savedInputJson);
        }

        [Test]
        public void DisplayInitializeUpgradesEarlyCapturedInstanceInPlace()
        {
            var early = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);

            var initialized = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1920, 1080, FullScreenMode.FullScreenWindow, 144, true, 144),
                false,
                false);

            Assert.That(initialized, Is.SameAs(early), "초기화는 캐시된 인스턴스를 새 인스턴스로 교체하면 안 된다.");
            Assert.That(DisplaySettingsService.Current, Is.SameAs(early));
            Assert.That(early.ResolutionWidth, Is.EqualTo(1920), "캡처된 인스턴스가 재초기화 결과를 반영해야 한다.");
            Assert.That(early.ResolutionHeight, Is.EqualTo(1080));
            Assert.That(early.IsVSyncEnabled, Is.True);
        }

        [Test]
        public void DisplayLoadKeepsCachedInstanceAndAppliesSavedValues()
        {
            var early = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);
            PlayerPrefs.SetString(
                "GraphicsSettings",
                "{\"version\":2,\"resolutionWidth\":1600,\"resolutionHeight\":900,\"preferredRefreshRate\":75," +
                "\"targetFrameRate\":75,\"isVSyncEnabled\":false,\"fullScreenMode\":3,\"qualityLevel\":-1}");

            var loaded = DisplaySettingsService.Load(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60));

            Assert.That(loaded, Is.SameAs(early), "불러오기도 캐시된 인스턴스를 교체하면 안 된다.");
            Assert.That(early.ResolutionWidth, Is.EqualTo(1600));
            Assert.That(early.ResolutionHeight, Is.EqualTo(900));
        }

        [Test]
        public void DisplayEventSubscriptionSurvivesReinitialization()
        {
            var early = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);
            var notificationCount = 0;
            early.OnDisplaySettingsChanged += _ => notificationCount++;

            DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1920, 1080, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);

            Assert.That(notificationCount, Is.EqualTo(1), "초기화 전에 등록한 구독이 그대로 살아 있어야 한다.");
        }

        [Test]
        public void AudioInitializeUpgradesEarlyCapturedInstanceInPlace()
        {
            var early = AudioSettingsService.Current;
            Assert.That(early.DefaultMasterVolume, Is.EqualTo(0f));

            var preset = AudioSettingsPreset.CreateRuntime(0.75f, true);
            try
            {
                var initialized = AudioSettingsService.Initialize(preset, false, false);

                Assert.That(initialized, Is.SameAs(early), "초기화는 캐시된 인스턴스를 새 인스턴스로 교체하면 안 된다.");
                Assert.That(AudioSettingsService.Current, Is.SameAs(early));
                Assert.That(early.DefaultMasterVolume, Is.EqualTo(0.75f), "캡처된 인스턴스가 프리셋 주입 결과를 반영해야 한다.");
                Assert.That(early.MasterVolume, Is.EqualTo(0.75f));
                Assert.That(early.IsMasterMuted, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void AudioResetToDefaultsRestoresPresetValues()
        {
            var preset = AudioSettingsPreset.CreateRuntime(0.75f);
            try
            {
                var settings = AudioSettingsService.Initialize(preset, false, false);
                settings.SetMaster(0.1f, true, false, false);
                Assert.That(settings.MasterVolume, Is.EqualTo(0.1f));

                settings.ResetToDefaults(false, false);

                Assert.That(settings.MasterVolume, Is.EqualTo(0.75f));
                Assert.That(settings.IsMasterMuted, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void InputLoadKeepsCachedInstanceAndPreservesLiveBlockToken()
        {
            var early = InputSettingsService.Current;
            var block = early.AcquireInputBlock();
            try
            {
                Assert.That(early.IsInputEnabled, Is.False);

                var loaded = InputSettingsService.Load();

                Assert.That(loaded, Is.SameAs(early), "불러오기는 캐시된 인스턴스를 교체하면 안 된다.");
                Assert.That(InputSettingsService.Current, Is.SameAs(early));
                Assert.That(
                    early.IsInputEnabled,
                    Is.False,
                    "저장 대상이 아닌 차단 토큰 상태가 불러오기로 사라지면 안 된다.");
            }
            finally
            {
                block.Dispose();
            }

            Assert.That(early.IsInputEnabled, Is.True);
        }

        [Test]
        public void InputInitializeUpgradesEarlyCapturedInstanceInPlace()
        {
            var early = InputSettingsService.Current;

            var initialized = InputSettingsService.Initialize(null, false, false);

            Assert.That(initialized, Is.SameAs(early), "초기화는 캐시된 인스턴스를 새 인스턴스로 교체하면 안 된다.");
            Assert.That(InputSettingsService.Current, Is.SameAs(early));
        }

        /// <summary>지정한 PlayerPrefs 키의 값을 읽으며, 없으면 null을 반환한다.</summary>
        /// <param name="key">읽을 PlayerPrefs 키이다.</param>
        /// <returns>저장돼 있던 문자열이며, 없었으면 null이다.</returns>
        private static string ReadSavedJson(string key)
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        /// <summary>테스트 전에 저장돼 있던 값을 되돌리며, 없었으면 키를 지운다.</summary>
        /// <param name="key">되돌릴 PlayerPrefs 키이다.</param>
        /// <param name="savedJson">되돌릴 문자열이며, null이면 키를 지운다.</param>
        private static void RestoreSavedJson(string key, string savedJson)
        {
            if (savedJson == null)
            {
                PlayerPrefs.DeleteKey(key);
            }
            else
            {
                PlayerPrefs.SetString(key, savedJson);
            }
        }

        /// <summary>테스트 사이에 정적 캐시가 새지 않도록 설정 서비스 캐시를 모두 초기화한다.</summary>
        private static void ResetAllServices()
        {
            SettingsStorage.ResetForTests();
            DisplaySettingsService.ResetCurrentForTests();
            AudioSettingsService.ResetCurrentForTests();
            InputSettingsService.ResetCurrentForTests();
            LocaleSettingsService.ResetCurrentForTests();
            PlayerPrefs.DeleteKey("GraphicsSettings");
            PlayerPrefs.DeleteKey("AudioSettings");
            PlayerPrefs.DeleteKey("InputSettings");
        }
    }
}
