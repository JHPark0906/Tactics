using HS.Framework.Foundation.Input;
using HS.Framework.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>입력 차단이 범위별로 나뉘어 액션 맵에 적용되는지 검증한다.</summary>
    public sealed class InputBlockScopeTests
    {
        private InputActionAsset _asset;

        [TearDown]
        public void TearDown()
        {
            if (_asset != null)
            {
                _asset.Disable();
                Object.DestroyImmediate(_asset);
                _asset = null;
            }
        }

        [Test]
        public void GameplayBlockLeavesUiInputAlive()
        {
            var service = new InputSettingsService();

            var token = service.AcquireInputBlock(InputBlockScope.Gameplay);

            Assert.That(service.IsScopeEnabled(InputBlockScope.Gameplay), Is.False);
            Assert.That(
                service.IsScopeEnabled(InputBlockScope.Ui),
                Is.True,
                "게임플레이만 막을 때 UI까지 꺼지면 되돌릴 수단이 사라진다.");

            token.Dispose();
            Assert.That(service.IsScopeEnabled(InputBlockScope.All), Is.True);
        }

        [Test]
        public void UiBlockLeavesGameplayInputAlive()
        {
            var service = new InputSettingsService();

            using var token = service.AcquireInputBlock(InputBlockScope.Ui);

            Assert.That(service.IsScopeEnabled(InputBlockScope.Ui), Is.False);
            Assert.That(service.IsScopeEnabled(InputBlockScope.Gameplay), Is.True);
        }

        [Test]
        public void AllScopeBlocksBothRanges()
        {
            var service = new InputSettingsService();

            using var token = service.AcquireInputBlock(InputBlockScope.All);

            Assert.That(service.IsScopeEnabled(InputBlockScope.Gameplay), Is.False);
            Assert.That(service.IsScopeEnabled(InputBlockScope.Ui), Is.False);
        }

        [Test]
        public void ScopesAreCountedIndependently()
        {
            var service = new InputSettingsService();
            var gameplayToken = service.AcquireInputBlock(InputBlockScope.Gameplay);
            var allToken = service.AcquireInputBlock(InputBlockScope.All);

            allToken.Dispose();

            Assert.That(
                service.IsScopeEnabled(InputBlockScope.Gameplay),
                Is.False,
                "게임플레이를 막는 다른 토큰이 남아 있으면 차단이 유지되어야 한다.");
            Assert.That(
                service.IsScopeEnabled(InputBlockScope.Ui),
                Is.True,
                "UI를 막던 토큰이 사라졌으면 UI는 곧바로 살아나야 한다.");

            gameplayToken.Dispose();
            Assert.That(service.IsScopeEnabled(InputBlockScope.All), Is.True);
        }

        [Test]
        public void ParameterlessAcquireBlocksGameplayOnly()
        {
            var service = new InputSettingsService();

            using var token = service.AcquireInputBlock();

            Assert.That(service.IsScopeEnabled(InputBlockScope.Gameplay), Is.False);
            Assert.That(
                service.IsScopeEnabled(InputBlockScope.Ui),
                Is.True,
                "범위를 밝히지 않은 차단은 되돌릴 길을 남기는 기본값을 따라야 한다.");
        }

        [Test]
        public void NoneScopeBlocksNothing()
        {
            var service = new InputSettingsService();

            using var token = service.AcquireInputBlock(InputBlockScope.None);

            Assert.That(service.IsScopeEnabled(InputBlockScope.All), Is.True);
        }

        [Test]
        public void GameplayBlockDisablesOnlyTheGameplayActionMap()
        {
            var service = new InputSettingsService();
            service.SetInputActionAsset(CreateAsset(), enable: true, loadBindingOverrides: false);

            using var token = service.AcquireInputBlock(InputBlockScope.Gameplay);

            Assert.That(FindMap("Player").enabled, Is.False, "게임플레이 맵은 꺼져야 한다.");
            Assert.That(
                FindMap("UI").enabled,
                Is.True,
                "모달 창이 자기 클릭과 취소 입력을 잃지 않으려면 UI 맵은 켜져 있어야 한다.");
        }

        [Test]
        public void ReleasingTheBlockRestoresTheGameplayActionMap()
        {
            var service = new InputSettingsService();
            service.SetInputActionAsset(CreateAsset(), enable: true, loadBindingOverrides: false);

            var token = service.AcquireInputBlock(InputBlockScope.Gameplay);
            token.Dispose();

            Assert.That(FindMap("Player").enabled, Is.True);
            Assert.That(FindMap("UI").enabled, Is.True);
        }

        [Test]
        public void AllScopeDisablesEveryActionMap()
        {
            var service = new InputSettingsService();
            service.SetInputActionAsset(CreateAsset(), enable: true, loadBindingOverrides: false);

            using var token = service.AcquireInputBlock(InputBlockScope.All);

            Assert.That(FindMap("Player").enabled, Is.False);
            Assert.That(FindMap("UI").enabled, Is.False);
        }

        [Test]
        public void MapsOtherThanTheUiMapAreTreatedAsGameplay()
        {
            var service = new InputSettingsService();
            var asset = CreateAsset();
            var extraMap = asset.AddActionMap("Vehicle");
            extraMap.AddAction("Steer", binding: "<Keyboard>/a");
            service.SetInputActionAsset(asset, enable: true, loadBindingOverrides: false);

            using var token = service.AcquireInputBlock(InputBlockScope.Gameplay);

            Assert.That(
                FindMap("Vehicle").enabled,
                Is.False,
                "용도를 모르는 맵은 게임플레이로 보아 함께 막는 것이 안전하다.");
            Assert.That(FindMap("UI").enabled, Is.True);
        }

        [Test]
        public void BaseDisabledStateTurnsOffEveryScope()
        {
            var service = new InputSettingsService();
            service.SetInputActionAsset(CreateAsset(), enable: false, loadBindingOverrides: false);

            Assert.That(service.IsScopeEnabled(InputBlockScope.Gameplay), Is.False);
            Assert.That(
                service.IsScopeEnabled(InputBlockScope.Ui),
                Is.False,
                "사용자가 입력을 통째로 끈 설정은 범위와 무관하게 적용되어야 한다.");
            Assert.That(FindMap("UI").enabled, Is.False);
        }

        [Test]
        public void CustomUiActionMapNameIsHonored()
        {
            var service = new InputSettingsService();
            var asset = CreateAsset();
            var menuMap = asset.AddActionMap("Menu");
            menuMap.AddAction("Submit", binding: "<Keyboard>/enter");
            service.SetUiActionMapName("Menu");
            service.SetInputActionAsset(asset, enable: true, loadBindingOverrides: false);

            using var token = service.AcquireInputBlock(InputBlockScope.Gameplay);

            Assert.That(FindMap("Menu").enabled, Is.True, "UI 맵으로 지정한 이름이 적용되어야 한다.");
            Assert.That(FindMap("UI").enabled, Is.False, "더 이상 UI 맵이 아니면 게임플레이로 취급된다.");
        }

        /// <summary>게임플레이 맵과 UI 맵을 하나씩 가진 입력 에셋을 만든다.</summary>
        private InputActionAsset CreateAsset()
        {
            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _asset.name = "TestInputActions";
            var playerMap = _asset.AddActionMap("Player");
            playerMap.AddAction("Move", binding: "<Keyboard>/w");
            var uiMap = _asset.AddActionMap("UI");
            uiMap.AddAction("Cancel", binding: "<Keyboard>/escape");
            return _asset;
        }

        /// <summary>이름으로 액션 맵을 찾으며, 없으면 테스트를 실패시킨다.</summary>
        private InputActionMap FindMap(string mapName)
        {
            var map = _asset.FindActionMap(mapName);
            Assert.That(map, Is.Not.Null, $"{mapName} 액션 맵을 찾지 못했다.");
            return map;
        }
    }
}
