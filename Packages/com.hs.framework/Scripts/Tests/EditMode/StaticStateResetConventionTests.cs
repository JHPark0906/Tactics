using System;
using System.Linq;
using System.Reflection;
using HS.Framework.Settings;
using HS.Framework.UI.Settings;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 정적 상태를 보유한 클래스가 플레이 모드 진입 시점 리셋 규약을 따르는지 검증한다.
    /// 도메인 리로드를 끄고 플레이 모드에 들어가면 이전 세션의 정적 상태가 살아남으므로,
    /// 프레임워크는 <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> 시점에
    /// 정적 상태를 되돌리는 비공개 메서드를 두는 것을 표준 규약으로 삼는다.
    /// 새로 정적 상태를 도입하는 클래스는 이 목록에도 함께 추가해야 한다.
    /// </summary>
    public sealed class StaticStateResetConventionTests
    {
        /// <summary>
        /// 정적 상태를 보유해 리셋 규약을 지켜야 하는 형식 목록이다.
        /// 제네릭 형식에는 <see cref="RuntimeInitializeOnLoadMethodAttribute"/>를 적용할 수 없어
        /// <c>SingletonBehaviour&lt;T&gt;</c>는 접근 시점 정리로 같은 규약을 만족시키므로 목록에서 제외한다.
        /// </summary>
        private static readonly Type[] StaticStateHolders =
        {
            typeof(DisplaySettingsService),
            typeof(AudioSettingsService),
            typeof(InputSettingsService),
            typeof(LocaleSettingsService),
            typeof(SettingsStorage),
            typeof(InputActionResolver),
            typeof(InputBindingItemViewModel)
        };

        [TestCaseSource(nameof(StaticStateHolders))]
        public void StaticStateHolderDeclaresSubsystemRegistrationReset(Type type)
        {
            var resetMethods = type
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method
                    .GetCustomAttributes<RuntimeInitializeOnLoadMethodAttribute>()
                    .Any(attribute => attribute.loadType == RuntimeInitializeLoadType.SubsystemRegistration))
                .ToArray();

            Assert.That(
                resetMethods,
                Is.Not.Empty,
                $"{type.Name}은 SubsystemRegistration 시점에 정적 상태를 되돌리는 메서드를 선언해야 한다.");
            Assert.That(
                resetMethods.All(method => method.GetParameters().Length == 0),
                Is.True,
                $"{type.Name}의 정적 상태 리셋 메서드는 매개변수를 받지 않아야 Unity가 호출할 수 있다.");
        }

        [Test]
        public void SettingsServiceResetHooksClearCachedInstances()
        {
            var display = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);
            var audio = AudioSettingsService.Current;
            var input = InputSettingsService.Current;
            var locale = LocaleSettingsService.Current;

            DisplaySettingsService.ResetCurrentForTests();
            AudioSettingsService.ResetCurrentForTests();
            InputSettingsService.ResetCurrentForTests();
            LocaleSettingsService.ResetCurrentForTests();

            Assert.That(AudioSettingsService.Current, Is.Not.SameAs(audio));
            Assert.That(InputSettingsService.Current, Is.Not.SameAs(input));
            Assert.That(LocaleSettingsService.Current, Is.Not.SameAs(locale));
            Assert.That(
                DisplaySettingsService.Initialize(
                    DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                    false,
                    false),
                Is.Not.SameAs(display));

            DisplaySettingsService.ResetCurrentForTests();
            AudioSettingsService.ResetCurrentForTests();
            InputSettingsService.ResetCurrentForTests();
            LocaleSettingsService.ResetCurrentForTests();
        }
    }
}
