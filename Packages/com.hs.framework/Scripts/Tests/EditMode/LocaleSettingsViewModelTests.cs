using HS.Framework.Settings;
using HS.Framework.UI.Settings;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>로케일 설정 ViewModel의 편집 흐름과 빈 목록 안전성을 검증한다.</summary>
    public sealed class LocaleSettingsViewModelTests
    {
        private static LocaleInfo[] CreateLocales()
        {
            return new[]
            {
                new LocaleInfo("en", "English"),
                new LocaleInfo("ko", "한국어")
            };
        }

        [Test]
        public void EmptyLocaleListIsSafeToEdit()
        {
            var viewModel = new LocaleSettingsViewModel(null, new LocaleInfo[0]);

            Assert.That(viewModel.Locales, Is.Empty);
            Assert.That(viewModel.SelectedLocaleIndex, Is.EqualTo(0));
            Assert.That(viewModel.HasChanges, Is.False);

            viewModel.SelectedLocaleIndex = 3;

            Assert.That(viewModel.SelectedLocaleIndex, Is.EqualTo(0));
            Assert.That(viewModel.HasChanges, Is.False);
            Assert.DoesNotThrow(() =>
            {
                viewModel.Apply(false);
                viewModel.Cancel();
                viewModel.ResetToDefaults();
            });
        }

        [Test]
        public void InitialSelectionMatchesProvidedLocaleCode()
        {
            var viewModel = new LocaleSettingsViewModel(null, CreateLocales(), "ko");

            Assert.That(viewModel.SelectedLocaleIndex, Is.EqualTo(1));
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void UnknownInitialLocaleCodeIsNotRecordedAsAppliedAndFirstOptionIsApplicable()
        {
            var viewModel = new LocaleSettingsViewModel(null, CreateLocales(), "fr");

            Assert.That(viewModel.SelectedLocaleIndex, Is.EqualTo(0));
            Assert.That(viewModel.HasChanges, Is.True, "적용된 로케일이 없으므로 표시 중인 첫 옵션은 적용 대기 변경이어야 한다.");

            viewModel.Apply(false);

            Assert.That(viewModel.SelectedLocaleIndex, Is.EqualTo(0));
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void MissingInitialLocaleCodeIsNotRecordedAsApplied()
        {
            var viewModel = new LocaleSettingsViewModel(null, CreateLocales());

            Assert.That(viewModel.SelectedLocaleIndex, Is.EqualTo(0));
            Assert.That(viewModel.HasChanges, Is.True, "현재 로케일을 확인하지 못했으면 첫 옵션을 적용된 것으로 기록하면 안 된다.");
        }

        [Test]
        public void ApplyKeepsPendingChangesWhenServiceRejectsLocale()
        {
            var rejectingService = new LocaleSettingsService();
            var viewModel = new LocaleSettingsViewModel(rejectingService, CreateLocales(), "en");

            viewModel.SelectedLocaleIndex = 1;
            Assert.That(viewModel.HasChanges, Is.True);

            viewModel.Apply(false);

            Assert.That(viewModel.SelectedLocaleIndex, Is.EqualTo(1), "적용 실패 시 편집 중인 선택을 유지해 재시도할 수 있어야 한다.");
            Assert.That(viewModel.HasChanges, Is.True, "서비스가 적용을 거부하면 변경 대기 상태를 유지해야 한다.");

            viewModel.Cancel();

            Assert.That(viewModel.SelectedLocaleIndex, Is.EqualTo(0));
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void ChangingSelectionTracksChangesAndApplyCommits()
        {
            var viewModel = new LocaleSettingsViewModel(null, CreateLocales(), "en");

            viewModel.SelectedLocaleIndex = 1;
            Assert.That(viewModel.HasChanges, Is.True);

            viewModel.Apply(false);

            Assert.That(viewModel.SelectedLocaleIndex, Is.EqualTo(1));
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void CancelRestoresLastAppliedSelection()
        {
            var viewModel = new LocaleSettingsViewModel(null, CreateLocales(), "en");

            viewModel.SelectedLocaleIndex = 1;
            viewModel.Cancel();

            Assert.That(viewModel.SelectedLocaleIndex, Is.EqualTo(0));
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void ResetToDefaultsRestoresInitialSelection()
        {
            var viewModel = new LocaleSettingsViewModel(null, CreateLocales(), "ko");

            viewModel.SelectedLocaleIndex = 0;
            viewModel.Apply(false);
            Assert.That(viewModel.HasChanges, Is.False);

            viewModel.ResetToDefaults();

            Assert.That(viewModel.SelectedLocaleIndex, Is.EqualTo(1));
            Assert.That(viewModel.HasChanges, Is.True);
        }

        [Test]
        public void SettingsWindowViewModelAggregatesLocaleChangesAndDisposesChild()
        {
            var graphicsSettings = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);
            var inputSettings = InputSettingsService.Initialize(null, false, false);
            var localeViewModel = new LocaleSettingsViewModel(null, CreateLocales(), "en");
            var windowViewModel = new SettingsWindowViewModel(
                new GraphicsSettingsViewModel(
                    graphicsSettings,
                    new[] { new SettingsResolution(1280, 720) },
                    new[] { 60 },
                    new[] { -1, 60 }),
                new InputSettingsViewModel(inputSettings),
                null,
                null,
                localeViewModel);

            try
            {
                Assert.That(windowViewModel.Locale, Is.SameAs(localeViewModel));
                Assert.That(windowViewModel.HasChanges, Is.False);

                localeViewModel.SelectedLocaleIndex = 1;
                Assert.That(windowViewModel.HasChanges, Is.True);

                windowViewModel.Apply(false, false);
                Assert.That(windowViewModel.HasChanges, Is.False);
            }
            finally
            {
                windowViewModel.Dispose();
                InputSettingsService.Initialize(null, false, false);
            }

            Assert.That(localeViewModel.IsDisposed, Is.True);
        }
    }
}
