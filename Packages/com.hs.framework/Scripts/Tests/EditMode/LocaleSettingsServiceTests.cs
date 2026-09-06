using Cysharp.Threading.Tasks;
using HS.Framework.Settings;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>로케일 설정 서비스의 정적 캐시 동작과 적용 거부 계약을 검증한다.</summary>
    public sealed class LocaleSettingsServiceTests
    {
        [SetUp]
        public void SetUp()
        {
            LocaleSettingsService.ResetCurrentForTests();
        }

        [TearDown]
        public void TearDown()
        {
            LocaleSettingsService.ResetCurrentForTests();
        }

        [Test]
        public void CurrentCachesLazilyCreatedInstance()
        {
            var first = LocaleSettingsService.Current;

            Assert.That(first, Is.Not.Null);
            Assert.That(LocaleSettingsService.Current, Is.SameAs(first));
        }

        [Test]
        public void CurrentWithoutInitializationIsNotUserConfigurable()
        {
            Assert.That(LocaleSettingsService.Current.IsUserConfigurable, Is.False);
        }

        [Test]
        public void SetSelectedLocaleCodeWithoutPresetReturnsFalse()
        {
            Assert.That(LocaleSettingsService.Current.SetSelectedLocaleCode("en", false), Is.False);
        }

        [Test]
        public void GetAvailableLocalesWithoutPresetReturnsEmptyList()
        {
            Assert.That(LocaleSettingsService.Current.GetAvailableLocales(), Is.Empty);
        }

        [Test]
        public void InitializeWithNullPresetUpgradesCachedInstanceInPlace()
        {
            var early = LocaleSettingsService.Current;

            var initialized = LocaleSettingsService.InitializeAsync(null).GetAwaiter().GetResult();

            Assert.That(initialized, Is.SameAs(early), "초기화는 캐시된 인스턴스를 새 인스턴스로 교체하면 안 된다.");
            Assert.That(LocaleSettingsService.Current, Is.SameAs(early));
            Assert.That(early.IsUserConfigurable, Is.False);
        }

        [Test]
        public void InitializeUpgradesEarlyCapturedInstanceInPlaceWithPreset()
        {
            var early = LocaleSettingsService.Current;
            Assert.That(early.IsUserConfigurable, Is.False);

            var preset = ScriptableObject.CreateInstance<LocaleSettingsPreset>();
            try
            {
                // 프리셋 주입은 첫 await 이전에 동기적으로 일어나므로 완료를 기다리지 않고 검증할 수 있다.
                LocaleSettingsService.InitializeAsync(preset).Forget();

                Assert.That(LocaleSettingsService.Current, Is.SameAs(early), "초기화 전에 캡처한 인스턴스가 초기화 후에도 유효해야 한다.");
                Assert.That(early.IsUserConfigurable, Is.True, "캡처된 인스턴스도 프리셋 주입 결과를 반영해야 한다.");
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }
    }
}
