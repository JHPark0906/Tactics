using System.Collections.Generic;
using HS.Framework.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>InputActionResolver의 서비스 우선 해석과 폴백 에셋 활성화 게이팅을 검증한다.</summary>
    public sealed class InputActionResolverTests
    {
        private readonly List<InputActionAsset> _createdAssets = new();
        private readonly List<InputActionResolver> _createdResolvers = new();

        [SetUp]
        public void SetUp()
        {
            ResetInputSettings();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var resolver in _createdResolvers)
            {
                resolver.Dispose();
            }

            _createdResolvers.Clear();
            ResetInputSettings();

            foreach (var asset in _createdAssets)
            {
                Object.DestroyImmediate(asset);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void FindActionWithoutServiceAssetResolvesFromFallbackAndEnablesIt()
        {
            var fallback = CreateAsset();
            var resolver = CreateResolver(fallback);

            var action = resolver.FindAction("Player", "Interact");

            Assert.That(action, Is.Not.Null);
            Assert.That(action.actionMap.asset, Is.SameAs(fallback));
            Assert.That(fallback.enabled, Is.True);
        }

        [Test]
        public void FindActionWithServiceAssetResolvesFromServiceAssetAndIgnoresFallback()
        {
            var serviceAsset = CreateAsset();
            var fallback = CreateAsset();
            InputSettingsService.Current.SetInputActionAsset(serviceAsset, true, false);

            var resolver = CreateResolver(fallback);
            var action = resolver.FindAction("Player", "Interact");

            Assert.That(resolver.CurrentAsset, Is.SameAs(serviceAsset));
            Assert.That(action.actionMap.asset, Is.SameAs(serviceAsset));
            Assert.That(fallback.enabled, Is.False);
        }

        [Test]
        public void ConstructorWithInputDisabledKeepsFallbackDisabled()
        {
            InputSettingsService.Current.SetInputEnabled(false);
            var fallback = CreateAsset();

            CreateResolver(fallback);

            Assert.That(fallback.enabled, Is.False);
        }

        [Test]
        public void SetInputEnabledTogglesFallbackEnabledState()
        {
            var fallback = CreateAsset();
            CreateResolver(fallback);
            Assert.That(fallback.enabled, Is.True);

            InputSettingsService.Current.SetInputEnabled(false);
            Assert.That(fallback.enabled, Is.False);

            InputSettingsService.Current.SetInputEnabled(true);
            Assert.That(fallback.enabled, Is.True);
        }

        [Test]
        public void RegisteringServiceAssetDisablesFallbackAndInvokesCallback()
        {
            var fallback = CreateAsset();
            var callbackCount = 0;
            var resolver = CreateResolver(fallback, () => callbackCount++);

            var serviceAsset = CreateAsset();
            InputSettingsService.Current.SetInputActionAsset(serviceAsset, true, false);

            Assert.That(resolver.CurrentAsset, Is.SameAs(serviceAsset));
            Assert.That(fallback.enabled, Is.False);
            Assert.That(callbackCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void DisposeDisablesFallbackAndStopsCallbacks()
        {
            var fallback = CreateAsset();
            var callbackCount = 0;
            var resolver = CreateResolver(fallback, () => callbackCount++);
            Assert.That(fallback.enabled, Is.True);

            resolver.Dispose();
            var countAfterDispose = callbackCount;

            Assert.That(fallback.enabled, Is.False);

            InputSettingsService.Current.SetInputEnabled(false);
            InputSettingsService.Current.SetInputEnabled(true);
            Assert.That(fallback.enabled, Is.False);
            Assert.That(callbackCount, Is.EqualTo(countAfterDispose));
        }

        [Test]
        public void DisposeWithAnotherResolverSharingFallbackKeepsFallbackEnabled()
        {
            var fallback = CreateAsset();
            var first = CreateResolver(fallback);
            var second = CreateResolver(fallback);

            first.Dispose();
            Assert.That(fallback.enabled, Is.True);

            second.Dispose();
            Assert.That(fallback.enabled, Is.False);
        }

        private static void ResetInputSettings()
        {
            var settings = InputSettingsService.Current;
            settings.SetInputActionAsset(null, true, false);
            settings.SetInputEnabled(true);
        }

        private InputActionAsset CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var actionMap = asset.AddActionMap("Player");
            actionMap.AddAction("Interact", InputActionType.Button);
            _createdAssets.Add(asset);
            return asset;
        }

        private InputActionResolver CreateResolver(
            InputActionAsset fallback,
            System.Action onResolvedAssetChanged = null)
        {
            var resolver = new InputActionResolver(fallback, onResolvedAssetChanged);
            _createdResolvers.Add(resolver);
            return resolver;
        }
    }
}
