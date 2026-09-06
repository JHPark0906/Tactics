using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 소멸 어빌리티가 활성화를 일으킨 이벤트 안에서 끝나지 않고, 시간이 흐른 틱에서만 끝나며,
    /// 취소되면 물러나지 않는지 검증한다. 사망 어빌리티가 끝나면서 이 어빌리티를 트리거하는 이어짐은
    /// <c>DeathAbilityTests</c>가 검증하므로, 여기서는 소멸 어빌리티 자체만 격리해서 본다.
    /// </summary>
    public sealed class DisappearanceAbilityTests
    {
        private static readonly GameplayTag AbilityTag = GameplayTag.Parse(UnitAbilityTags.Disappearance);

        private GameObject _owner;
        private GameplayAbilitySystemComponent _abilitySystem;
        private DisappearanceAbilityDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Owner");
            _abilitySystem = _owner.AddComponent<GameplayAbilitySystemComponent>();
            _abilitySystem.ConfigureManualControl();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_owner);
            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
            }
        }

        [Test]
        public void ActivationDoesNotRetireTheOwnerImmediately()
        {
            Grant(disappearDelay: 0f);

            var result = _abilitySystem.System.TryActivate(AbilityTag);

            Assert.That(result, Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(_owner.activeSelf, Is.True, "활성화되는 순간에는 물러나지 않는다.");
        }

        [Test]
        public void ATickWithoutElapsedTimeDoesNotRetire()
        {
            Grant(disappearDelay: 0f);
            _abilitySystem.System.TryActivate(AbilityTag);

            _abilitySystem.System.Tick(0f);

            Assert.That(_owner.activeSelf, Is.True, "흐른 시간이 없는 틱은 활성화되는 순간의 판정이다.");
            Assert.That(_abilitySystem.System.IsActive(AbilityTag), Is.True);
        }

        [Test]
        public void RetiresOnceTheDelayHasPassed()
        {
            Grant(disappearDelay: 0.5f);
            _abilitySystem.System.TryActivate(AbilityTag);

            _abilitySystem.System.Tick(0.3f);
            Assert.That(_owner.activeSelf, Is.True, "기다리는 시간이 지나기 전에는 물러나지 않는다.");

            _abilitySystem.System.Tick(0.3f);
            Assert.That(_owner.activeSelf, Is.False);
            Assert.That(_abilitySystem.System.IsActive(AbilityTag), Is.False);
        }

        [Test]
        public void BeingCancelledDoesNotRetire()
        {
            Grant(disappearDelay: 0f);
            _abilitySystem.System.TryActivate(AbilityTag);

            _abilitySystem.System.Clear();

            Assert.That(_owner.activeSelf, Is.True, "파괴나 정리로 취소된 것은 물러남이 아니다.");
        }

        [Test]
        public void TheDisappearanceAssetCarriesTheRulesTheCodeReliesOn()
        {
            var asset = AssetDatabase.LoadAssetAtPath<DisappearanceAbilityDefinition>(
                "Assets/_HS/Tactics/Definitions/Abilities/DisappearanceAbility.asset");

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.TryValidate(out var errorMessage), Is.True, errorMessage);
            Assert.That(asset.AbilityTag, Is.EqualTo(AbilityTag));
            Assert.That(asset.TriggerEventTags, Does.Contain(GameplayTag.Parse(DeathAbility.DisappearanceEventTagName)));
            Assert.That(asset.CancelAbilitiesWithTags, Does.Contain(GameplayTag.Parse(UnitAbilityTags.Family)));
            Assert.That(asset.BlockAbilitiesWithTags, Does.Contain(GameplayTag.Parse(UnitAbilityTags.Family)));
        }

        private void Grant(float disappearDelay)
        {
            _definition = DisappearanceAbilityDefinition.CreateRuntime(disappearDelay);
            _abilitySystem.System.GrantAbility(_definition);
        }
    }
}
