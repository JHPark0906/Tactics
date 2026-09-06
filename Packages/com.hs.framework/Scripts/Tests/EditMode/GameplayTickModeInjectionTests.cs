using System.Collections.Generic;
using System.Reflection;
using HS.Framework.Ability;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Effects;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.ProjectManagement;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 어빌리티 시스템·효과 실행기·행동 트리 실행기가 시간을 흘릴 자리를 프로젝트 설정(GameMode) 하나에서만
    /// 받는지, 그리고 씬 오브젝트와 스폰된 오브젝트 어느 쪽이든 첫 틱 전에 그 값을 받는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>두 주입 순서를 모두 재는 것이 이 검사의 중심이다.</b> 씬에 놓인 오브젝트는 Awake가 먼저 돌고
    /// 그 뒤에 주입을 받는다(<c>FrameworkInitializer</c>가 씬 로드 후 주입한다). 실행 중 스폰된 오브젝트는
    /// 비활성 상태에서 먼저 주입을 받고 그 뒤에 활성화되어 Awake가 돈다(<c>UnitSpawner</c>의 방식과 같다).
    /// 한쪽 순서만 확인하면 다른 쪽에서 낡은 기본값이 첫 틱까지 남아도 아무 신호가 없다.
    /// </para>
    /// <para>
    /// <b>인스펙터 칸이 없다는 것도 함께 고정한다.</b> 리플렉션으로 필드를 찾되 <c>SerializeField</c> 속성이
    /// 없어야 한다. 필드 자체가 없어졌는지, 아니면 그저 감춰졌을 뿐인지는 이 검사가 갈라 준다.
    /// </para>
    /// </remarks>
    public sealed class GameplayTickModeInjectionTests
    {
        private readonly List<GameObject> _createdObjects = new();
        private readonly List<Object> _createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();

            foreach (var createdAsset in _createdAssets)
            {
                if (createdAsset != null)
                {
                    Object.DestroyImmediate(createdAsset);
                }
            }

            _createdAssets.Clear();
        }

        [Test]
        public void AFreshProjectConfigurationDefaultsToFixedUpdate()
        {
            var configuration = FrameworkProjectConfiguration.CreateRuntime(null, ProjectUserSetting.None, 1);
            _createdAssets.Add(configuration);

            Assert.That(configuration.GameplayTickMode, Is.EqualTo(GameplayTickMode.OnFixedUpdate));
        }

        [Test]
        public void WithoutInjectionTheThreeComponentsDefaultToFixedUpdate()
        {
            var abilitySystem = CreateObject("Ability").AddComponent<GameplayAbilitySystemComponent>();
            var effect = CreateObject("Effect").AddComponent<GameplayEffectComponent>();
            var runner = CreateObject("Runner").AddComponent<BehaviourTreeRunner>();

            Assert.That(abilitySystem.TickMode, Is.EqualTo(GameplayTickMode.OnFixedUpdate));
            Assert.That(effect.TickMode, Is.EqualTo(GameplayTickMode.OnFixedUpdate));
            Assert.That(runner.TickMode, Is.EqualTo(GameplayTickMode.OnFixedUpdate));
        }

        [Test]
        public void TheThreeComponentsFollowTheInjectedGameMode()
        {
            var source = new FakeTickModeSource(GameplayTickMode.OnUpdate);
            var abilitySystem = CreateObject("Ability").AddComponent<GameplayAbilitySystemComponent>();
            var effect = CreateObject("Effect").AddComponent<GameplayEffectComponent>();
            var runner = CreateObject("Runner").AddComponent<BehaviourTreeRunner>();

            abilitySystem.InjectGameplayTickMode(source);
            effect.InjectGameplayTickMode(source);
            runner.InjectGameplayTickMode(source);

            Assert.That(abilitySystem.TickMode, Is.EqualTo(GameplayTickMode.OnUpdate));
            Assert.That(effect.TickMode, Is.EqualTo(GameplayTickMode.OnUpdate));
            Assert.That(runner.TickMode, Is.EqualTo(GameplayTickMode.OnUpdate));
        }

        [Test]
        public void AnEffectOnlyActorWithoutAnAbilitySystemStillFollowsGameMode()
        {
            // 효과 실행기는 어트리뷰트만 요구하고 어빌리티 시스템을 요구하지 않으므로,
            // 체력만 있고 어빌리티가 없는 액터에서는 이 컴포넌트가 맞춰 줄 상대가 없다.
            var effect = CreateObject("EffectOnly").AddComponent<GameplayEffectComponent>();
            var source = new FakeTickModeSource(GameplayTickMode.OnUpdate);

            effect.InjectGameplayTickMode(source);

            Assert.That(effect.TickMode, Is.EqualTo(GameplayTickMode.OnUpdate));
        }

        [Test]
        public void InjectingTheAbilitySystemAlsoAlignsItsEffectComponent()
        {
            var host = CreateObject("Host");
            var abilitySystem = host.AddComponent<GameplayAbilitySystemComponent>();
            var effect = host.GetComponent<GameplayEffectComponent>();
            var source = new FakeTickModeSource(GameplayTickMode.OnUpdate);

            abilitySystem.InjectGameplayTickMode(source);

            Assert.That(
                effect.TickMode,
                Is.EqualTo(GameplayTickMode.OnUpdate),
                "어빌리티와 효과가 다른 시계를 보면 사격 간격처럼 효과가 세는 시간과 어빌리티가 판단하는 시간이 어긋난다.");
        }

        [Test]
        public void ASceneObjectKeepsTheInjectedValueEvenThoughAwakeRanFirst()
        {
            // 씬에 놓인 오브젝트는 Awake가 먼저 돌고(기본값 그대로), FrameworkInitializer가 씬 로드 뒤에 주입한다.
            var host = CreateObject("SceneUnit");
            var abilitySystem = host.AddComponent<GameplayAbilitySystemComponent>();
            Assert.That(
                abilitySystem.TickMode,
                Is.EqualTo(GameplayTickMode.OnFixedUpdate),
                "Awake 직후에는 아직 주입 전이므로 기본값이어야 검사의 전제가 성립한다.");

            abilitySystem.InjectGameplayTickMode(new FakeTickModeSource(GameplayTickMode.OnUpdate));

            Assert.That(
                abilitySystem.TickMode,
                Is.EqualTo(GameplayTickMode.OnUpdate),
                "Awake가 먼저 돌아도 그 뒤의 주입이 첫 틱보다 앞서면 값이 정확히 반영되어야 한다.");
        }

        [Test]
        public void ASpawnedObjectReceivesTheValueBeforeAwakeEverRuns()
        {
            // 실행 중 스폰된 오브젝트는 UnitSpawner처럼 비활성 상태에서 먼저 주입을 받고, 그 뒤에 활성화되어 Awake가 돈다.
            var host = new GameObject("SpawnedUnit");
            _createdObjects.Add(host);
            host.SetActive(false);

            var abilitySystem = host.AddComponent<GameplayAbilitySystemComponent>();
            abilitySystem.InjectGameplayTickMode(new FakeTickModeSource(GameplayTickMode.OnUpdate));

            host.SetActive(true);

            Assert.That(
                abilitySystem.TickMode,
                Is.EqualTo(GameplayTickMode.OnUpdate),
                "Awake는 주입된 값을 덮어쓰지 않아야 한다 — 그렇지 않으면 스폰된 유닛만 기본값으로 되돌아간다.");
        }

        [Test]
        public void NoneOfTheThreeComponentsExposeATickModeInspectorSlot()
        {
            AssertNoSerializedTickModeField(typeof(GameplayAbilitySystemComponent));
            AssertNoSerializedTickModeField(typeof(GameplayEffectComponent));
            AssertNoSerializedTickModeField(typeof(BehaviourTreeRunner));
        }

        /// <summary>그 형식의 tickMode 필드가 있지만 SerializeField 속성은 없는지 확인한다.</summary>
        /// <param name="componentType">확인할 컴포넌트 형식이다.</param>
        private static void AssertNoSerializedTickModeField(System.Type componentType)
        {
            var field = componentType.GetField(
                "tickMode", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, $"{componentType.Name}에 tickMode 필드 자체가 없어졌다.");
            Assert.That(
                field.GetCustomAttribute<SerializeField>(),
                Is.Null,
                $"{componentType.Name}의 tickMode가 여전히 인스펙터 칸으로 남아 있다 — 값은 GameMode에서만 와야 한다.");
        }

        /// <summary>정리 목록에 등록된 빈 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        /// <returns>만든 오브젝트이다.</returns>
        private GameObject CreateObject(string objectName)
        {
            var createdObject = new GameObject(objectName);
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>검사에서 정해 둔 틱 방식을 그대로 돌려주는 프로젝트 설정 대역이다.</summary>
        private sealed class FakeTickModeSource : IGameplayTickModeSource
        {
            internal FakeTickModeSource(GameplayTickMode mode)
            {
                GameplayTickMode = mode;
            }

            /// <inheritdoc />
            public GameplayTickMode GameplayTickMode { get; }
        }
    }
}
