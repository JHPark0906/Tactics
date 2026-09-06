using HS.Framework.Settings;
using HS.Framework.UI.Settings;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>그래픽 설정의 품질 레벨 옵션 구성, 저장 버전 호환, 센티널 복원 흐름을 검증한다.</summary>
    public sealed class GraphicsQualityLevelTests
    {
        /// <summary>그래픽 설정 저장에 사용하는 PlayerPrefs 키이다.</summary>
        private const string PlayerPrefsKey = "GraphicsSettings";

        /// <summary>테스트에서 QualitySettings 전역 상태 대신 사용하는 가짜 품질 레벨 컨트롤러이다.</summary>
        private FakeQualityLevelController _qualityController;

        /// <summary>테스트 시작 전 PlayerPrefs에 저장돼 있던 그래픽 설정 JSON이며, 없었으면 null이다.</summary>
        private string _savedGraphicsJson;

        /// <summary>테스트 시작 전의 VSync 카운트이다.</summary>
        private int _savedVSyncCount;

        /// <summary>테스트 시작 전의 목표 프레임레이트이다.</summary>
        private int _savedTargetFrameRate;

        [SetUp]
        public void SetUp()
        {
            _qualityController = new FakeQualityLevelController(2, 1);
            DisplaySettingsService.QualityController = _qualityController;
            _savedGraphicsJson = PlayerPrefs.HasKey(PlayerPrefsKey) ? PlayerPrefs.GetString(PlayerPrefsKey) : null;
            _savedVSyncCount = QualitySettings.vSyncCount;
            _savedTargetFrameRate = Application.targetFrameRate;
        }

        [TearDown]
        public void TearDown()
        {
            DisplaySettingsService.QualityController = null;

            if (_savedGraphicsJson == null)
            {
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
            }
            else
            {
                PlayerPrefs.SetString(PlayerPrefsKey, _savedGraphicsJson);
            }

            QualitySettings.vSyncCount = _savedVSyncCount;
            Application.targetFrameRate = _savedTargetFrameRate;
        }

        private static DisplaySettingsService CreateSettings(int qualityLevel = -1)
        {
            return DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60, qualityLevel),
                false,
                false);
        }

        private static GraphicsSettingsViewModel CreateViewModel(DisplaySettingsService settings)
        {
            return new GraphicsSettingsViewModel(
                settings,
                new[] { new SettingsResolution(1280, 720), new SettingsResolution(1920, 1080) },
                new[] { 60, 144 },
                new[] { -1, 60, 144 },
                new[] { "Low", "High" });
        }

        [Test]
        public void QualityLevelOptionsStartWithProjectDefaultSentinel()
        {
            var viewModel = CreateViewModel(CreateSettings());

            Assert.That(viewModel.QualityLevels.Count, Is.EqualTo(3));
            Assert.That(viewModel.QualityLevels[0].Value, Is.EqualTo(-1));
            Assert.That(viewModel.QualityLevels[0].DisplayName, Is.EqualTo("Project Default"));
            Assert.That(viewModel.QualityLevels[1].Value, Is.EqualTo(0));
            Assert.That(viewModel.QualityLevels[1].DisplayName, Is.EqualTo("Low"));
            Assert.That(viewModel.QualityLevels[2].Value, Is.EqualTo(1));
            Assert.That(viewModel.QualityLevels[2].DisplayName, Is.EqualTo("High"));
        }

        [Test]
        public void SentinelPresetSelectsProjectDefaultWithoutChanges()
        {
            var viewModel = CreateViewModel(CreateSettings());

            Assert.That(viewModel.SelectedQualityLevelIndex, Is.EqualTo(0));
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void SelectingQualityLevelTracksChangesAndAppliesToService()
        {
            var settings = CreateSettings();
            var viewModel = CreateViewModel(settings);

            viewModel.SelectedQualityLevelIndex = 2;
            Assert.That(viewModel.HasChanges, Is.True);

            viewModel.Apply(false, false);

            Assert.That(settings.QualityLevel, Is.EqualTo(1));
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void CancelRestoresAppliedQualityLevel()
        {
            var viewModel = CreateViewModel(CreateSettings());

            viewModel.SelectedQualityLevelIndex = 1;
            Assert.That(viewModel.HasChanges, Is.True);

            viewModel.Cancel();

            Assert.That(viewModel.SelectedQualityLevelIndex, Is.EqualTo(0));
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void PresetQualityLevelSelectsMatchingOption()
        {
            var settings = CreateSettings(1);
            var viewModel = CreateViewModel(settings);

            Assert.That(settings.QualityLevel, Is.EqualTo(1));
            Assert.That(viewModel.SelectedQualityLevelIndex, Is.EqualTo(2));
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void SetQualityLevelSanitizesNegativeValueToSentinel()
        {
            var settings = CreateSettings();

            settings.SetQualityLevel(-5, false, false);

            Assert.That(settings.QualityLevel, Is.EqualTo(-1));
        }

        [Test]
        public void SetQualityLevelSanitizesOutOfRangeValueToSentinel()
        {
            var settings = CreateSettings();

            settings.SetQualityLevel(5, false, false);

            Assert.That(settings.QualityLevel, Is.EqualTo(-1));
        }

        [Test]
        public void ApplyingSentinelRestoresCapturedProjectDefaultQualityLevel()
        {
            var settings = CreateSettings();

            settings.SetQualityLevel(0, true, false);
            Assert.That(_qualityController.GetQualityLevel(), Is.EqualTo(0));

            settings.SetQualityLevel(-1, true, false);

            Assert.That(settings.QualityLevel, Is.EqualTo(-1));
            Assert.That(_qualityController.GetQualityLevel(), Is.EqualTo(1));
        }

        [Test]
        public void ApplySkipsQualityChangeWhenLevelAlreadyMatches()
        {
            var settings = CreateSettings(1);

            settings.Apply();

            Assert.That(_qualityController.SetCallCount, Is.EqualTo(0));
        }

        [Test]
        public void ApplyingSentinelWithoutExplicitLevelKeepsCurrentQuality()
        {
            var settings = CreateSettings();

            settings.Apply();

            Assert.That(_qualityController.SetCallCount, Is.EqualTo(0));
            Assert.That(_qualityController.GetQualityLevel(), Is.EqualTo(1));
        }

        [Test]
        public void LoadAcceptsOlderVersionSaveDataAndPreservesValues()
        {
            PlayerPrefs.SetString(
                PlayerPrefsKey,
                "{\"version\":1,\"resolutionWidth\":1920,\"resolutionHeight\":1080,\"preferredRefreshRate\":144,\"targetFrameRate\":120,\"isVSyncEnabled\":true,\"fullScreenMode\":3}");

            var settings = DisplaySettingsService.Load(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.FullScreenWindow, 60, false, 60));

            Assert.That(settings.ResolutionWidth, Is.EqualTo(1920));
            Assert.That(settings.ResolutionHeight, Is.EqualTo(1080));
            Assert.That(settings.PreferredRefreshRate, Is.EqualTo(144));
            Assert.That(settings.TargetFrameRate, Is.EqualTo(120));
            Assert.That(settings.IsVSyncEnabled, Is.True);
            Assert.That(settings.FullScreenMode, Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(settings.QualityLevel, Is.EqualTo(-1));
        }

        [Test]
        public void LoadRejectsNewerVersionSaveDataAndFallsBackToDefaults()
        {
            PlayerPrefs.SetString(
                PlayerPrefsKey,
                "{\"version\":3,\"resolutionWidth\":1920,\"resolutionHeight\":1080,\"preferredRefreshRate\":144,\"targetFrameRate\":120,\"isVSyncEnabled\":true,\"fullScreenMode\":3,\"qualityLevel\":0}");

            var settings = DisplaySettingsService.Load(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.FullScreenWindow, 60, false, 60));

            Assert.That(settings.ResolutionWidth, Is.EqualTo(1280));
            Assert.That(settings.ResolutionHeight, Is.EqualTo(720));
            Assert.That(settings.PreferredRefreshRate, Is.EqualTo(60));
            Assert.That(settings.IsVSyncEnabled, Is.False);
            Assert.That(settings.FullScreenMode, Is.EqualTo(FullScreenMode.FullScreenWindow));
            Assert.That(settings.QualityLevel, Is.EqualTo(-1));
        }

        [Test]
        public void LoadSanitizesOutOfRangeQualityLevelToSentinel()
        {
            PlayerPrefs.SetString(
                PlayerPrefsKey,
                "{\"version\":2,\"resolutionWidth\":1280,\"resolutionHeight\":720,\"preferredRefreshRate\":60,\"targetFrameRate\":60,\"isVSyncEnabled\":false,\"fullScreenMode\":3,\"qualityLevel\":5}");

            var settings = DisplaySettingsService.Load(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60));

            Assert.That(settings.QualityLevel, Is.EqualTo(-1));
        }

        /// <summary>QualitySettings 전역 상태 대신 사용하는 테스트용 품질 레벨 컨트롤러이다.</summary>
        private sealed class FakeQualityLevelController : IQualityLevelController
        {
            /// <summary>현재 적용된 품질 레벨 인덱스이다.</summary>
            private int _currentLevel;

            /// <summary>가짜 품질 레벨 컨트롤러를 생성한다.</summary>
            /// <param name="count">사용 가능한 품질 레벨 개수이다.</param>
            /// <param name="currentLevel">초기 품질 레벨 인덱스이다.</param>
            public FakeQualityLevelController(int count, int currentLevel)
            {
                Count = count;
                _currentLevel = currentLevel;
            }

            /// <inheritdoc />
            public int Count { get; }

            /// <summary>SetQualityLevel이 호출된 횟수를 가져온다.</summary>
            public int SetCallCount { get; private set; }

            /// <inheritdoc />
            public int GetQualityLevel()
            {
                return _currentLevel;
            }

            /// <inheritdoc />
            public void SetQualityLevel(int qualityLevel)
            {
                _currentLevel = qualityLevel;
                SetCallCount++;
            }
        }
    }
}
