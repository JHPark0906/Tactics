using System.Reflection;
using HS.Framework.Settings;
using HS.Framework.Foundation.Input;
using HS.Framework.UI.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Framework.Tests.EditMode
{
    public sealed class SettingsWindowViewModelTests
    {
        [Test]
        public void GraphicsViewModelAppliesPendingValuesToGraphicsSettings()
        {
            var settings = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);
            var viewModel = new GraphicsSettingsViewModel(
                settings,
                new[] { new SettingsResolution(1280, 720), new SettingsResolution(1920, 1080) },
                new[] { 60, 144 },
                new[] { -1, 60, 144 });

            viewModel.SelectedResolutionIndex = 1;
            viewModel.SelectedRefreshRateIndex = 2;
            viewModel.SelectedFullScreenModeIndex = 0;
            viewModel.SelectedTargetFrameRateIndex = 2;
            viewModel.IsVSyncEnabled = true;

            Assert.That(viewModel.HasChanges, Is.True);

            viewModel.Apply(false, false);

            Assert.That(settings.ResolutionWidth, Is.EqualTo(1920));
            Assert.That(settings.ResolutionHeight, Is.EqualTo(1080));
            Assert.That(settings.PreferredRefreshRate, Is.EqualTo(144));
            Assert.That(settings.FullScreenMode, Is.EqualTo(FullScreenMode.FullScreenWindow));
            Assert.That(settings.TargetFrameRate, Is.EqualTo(144));
            Assert.That(settings.IsVSyncEnabled, Is.True);
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void GraphicsViewModelCancelRestoresAppliedValues()
        {
            var settings = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);
            var viewModel = new GraphicsSettingsViewModel(
                settings,
                new[] { new SettingsResolution(1280, 720), new SettingsResolution(1920, 1080) },
                new[] { 60, 144 },
                new[] { -1, 60 });

            viewModel.SelectedResolutionIndex = 1;
            Assert.That(viewModel.HasChanges, Is.True);

            viewModel.Cancel();

            Assert.That(viewModel.SelectedResolutionIndex, Is.EqualTo(0));
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void InputViewModelAppliesBindingOverrides()
        {
            var inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            var actionMap = inputActionAsset.AddActionMap("Gameplay");
            var action = actionMap.AddAction("Jump", binding: "<Keyboard>/space");

            try
            {
                var settings = InputSettingsService.Initialize(inputActionAsset, false, false);
                var viewModel = new InputSettingsViewModel(settings);

                viewModel.Bindings[0].PendingPath = "<Keyboard>/enter";
                Assert.That(viewModel.HasChanges, Is.True);

                viewModel.Apply(false);

                Assert.That(action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
                Assert.That(viewModel.HasChanges, Is.False);
            }
            finally
            {
                InputSettingsService.Initialize(null, false, false);
                Object.DestroyImmediate(inputActionAsset);
            }
        }

        [Test]
        public void InputBindingItemViewModelProvidesReadableDisplayNameAndResetsToDefault()
        {
            var inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            var actionMap = inputActionAsset.AddActionMap("Gameplay");
            actionMap.AddAction("Jump", binding: "<Keyboard>/space");

            try
            {
                var settings = InputSettingsService.Initialize(inputActionAsset, false, false);
                var viewModel = new InputSettingsViewModel(settings);
                var binding = viewModel.Bindings[0];

                binding.PendingPath = "<Keyboard>/enter";
                Assert.That(binding.DisplayName, Is.EqualTo("Enter"));

                binding.ResetToDefaults();
                Assert.That(binding.PendingPath, Is.EqualTo("<Keyboard>/space"));
                Assert.That(binding.DisplayName, Is.EqualTo("Space"));
            }
            finally
            {
                InputSettingsService.Initialize(null, false, false);
                Object.DestroyImmediate(inputActionAsset);
            }
        }

        [Test]
        public void DisposedInputBindingItemRejectsRebindingAndIgnoresMutations()
        {
            var inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            var actionMap = inputActionAsset.AddActionMap("Gameplay");
            var action = actionMap.AddAction("Jump", binding: "<Keyboard>/space");

            try
            {
                var binding = new InputBindingItemViewModel(action, 0, actionMap.name);
                binding.Dispose();

                binding.PendingPath = "<Keyboard>/enter";
                binding.Cancel();
                binding.ResetToDefaults();

                Assert.That(binding.PendingPath, Is.EqualTo("<Keyboard>/space"));
                Assert.That(binding.StartInteractiveRebindingAsync().GetAwaiter().GetResult(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(inputActionAsset);
            }
        }

        [Test]
        public void ApplyingBindingDoesNotReenableActionWhileInputGateIsClosed()
        {
            var inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            var actionMap = inputActionAsset.AddActionMap("Gameplay");
            var action = actionMap.AddAction("Jump", binding: "<Keyboard>/space");

            try
            {
                action.Enable();
                var binding = new InputBindingItemViewModel(action, 0, actionMap.name, new ClosedInputGate());
                binding.PendingPath = "<Keyboard>/enter";

                binding.Apply();

                Assert.That(action.enabled, Is.False);
                Assert.That(action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));
            }
            finally
            {
                Object.DestroyImmediate(inputActionAsset);
            }
        }

        [Test]
        public void SettingsWindowViewModelCombinesSectionChanges()
        {
            var graphicsSettings = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);
            var inputSettings = InputSettingsService.Initialize(null, false, false);
            var viewModel = SettingsWindowViewModel.CreateDefault(
                graphicsSettings,
                inputSettings,
                new[] { new SettingsAudioChannelDefinition("master", "Master", 0.8f) });

            viewModel.AudioChannels[0].PendingVolume = 0.25f;
            Assert.That(viewModel.HasChanges, Is.True);

            viewModel.Apply(false, false);

            Assert.That(viewModel.AudioChannels[0].AppliedVolume, Is.EqualTo(0.25f));
            Assert.That(viewModel.HasChanges, Is.False);
        }

        [Test]
        public void SettingsWindowViewDisposesOwnedViewModelWhenReplacedByInjectedViewModel()
        {
            var graphicsSettings = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);
            var inputSettings = InputSettingsService.Initialize(null, false, false);
            var injectedViewModel = SettingsWindowViewModel.CreateDefault(
                graphicsSettings,
                inputSettings,
                new[] { new SettingsAudioChannelDefinition("master", "Master", 0.8f) });
            var gameObject = new GameObject(nameof(SettingsWindowViewDisposesOwnedViewModelWhenReplacedByInjectedViewModel));

            try
            {
                var view = gameObject.AddComponent<SettingsWindowView>();
                InvokeLifecycleMethod(view, "Awake");
                var ownedViewModel = GetOwnedViewModel(view);

                Assert.That(ownedViewModel, Is.Not.Null);
                Assert.That(ownedViewModel.IsDisposed, Is.False);

                view.Initialize(injectedViewModel);

                Assert.That(ownedViewModel.IsDisposed, Is.True);
                Assert.That(injectedViewModel.IsDisposed, Is.False);
                Assert.That(GetOwnedViewModel(view), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                injectedViewModel.Dispose();
                InputSettingsService.Initialize(null, false, false);
            }
        }

        [Test]
        public void SettingsWindowViewDisposesOwnedViewModelOnDestroy()
        {
            DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);
            InputSettingsService.Initialize(null, false, false);
            var gameObject = new GameObject(nameof(SettingsWindowViewDisposesOwnedViewModelOnDestroy));

            try
            {
                var view = gameObject.AddComponent<SettingsWindowView>();
                InvokeLifecycleMethod(view, "Awake");
                var ownedViewModel = GetOwnedViewModel(view);

                Assert.That(ownedViewModel, Is.Not.Null);

                InvokeLifecycleMethod(view, "OnDestroy");

                Assert.That(ownedViewModel.IsDisposed, Is.True);
                Assert.That(GetOwnedViewModel(view), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                InputSettingsService.Initialize(null, false, false);
            }
        }

        [Test]
        public void SettingsWindowViewDoesNotDisposeInjectedViewModelOnDestroy()
        {
            var graphicsSettings = DisplaySettingsService.Initialize(
                DisplaySettingsPreset.CreateRuntime(1280, 720, FullScreenMode.Windowed, 60, false, 60),
                false,
                false);
            var inputSettings = InputSettingsService.Initialize(null, false, false);
            var injectedViewModel = SettingsWindowViewModel.CreateDefault(
                graphicsSettings,
                inputSettings,
                new[] { new SettingsAudioChannelDefinition("master", "Master", 0.8f) });
            var gameObject = new GameObject(nameof(SettingsWindowViewDoesNotDisposeInjectedViewModelOnDestroy));

            try
            {
                var view = gameObject.AddComponent<SettingsWindowView>();
                view.Initialize(injectedViewModel);

                InvokeLifecycleMethod(view, "OnDestroy");

                Assert.That(injectedViewModel.IsDisposed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                injectedViewModel.Dispose();
                InputSettingsService.Initialize(null, false, false);
            }
        }

        private static void InvokeLifecycleMethod(SettingsWindowView view, string methodName)
        {
            var method = typeof(SettingsWindowView).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"SettingsWindowView.{methodName} 메서드를 찾지 못했다.");
            method.Invoke(view, null);
        }

        private static SettingsWindowViewModel GetOwnedViewModel(SettingsWindowView view)
        {
            var field = typeof(SettingsWindowView).GetField(
                "_ownedViewModel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "SettingsWindowView._ownedViewModel 필드를 찾지 못했다.");
            return (SettingsWindowViewModel)field.GetValue(view);
        }

        private sealed class ClosedInputGate : IInputStateController
        {
            public bool IsInputEnabled => false;

            public bool IsScopeEnabled(InputBlockScope scope) => false;

            public void SetInputEnabled(bool isEnabled)
            {
            }

            public System.IDisposable AcquireInputBlock()
            {
                return new NoopToken();
            }

            public System.IDisposable AcquireInputBlock(InputBlockScope scope)
            {
                return new NoopToken();
            }

            private sealed class NoopToken : System.IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}
