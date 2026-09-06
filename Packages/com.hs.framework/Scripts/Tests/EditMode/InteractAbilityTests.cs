using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Interaction;
using HS.Framework.Interaction.Abilities;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 상호작용 시도 자체가 어빌리티(<see cref="InteractAbilityDefinition"/>)를 거쳐 실행되는지,
    /// 그리고 어빌리티 시스템이 없는 인터랙터에서는 직접 실행되는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 모듈은 GAS 없이도 쓸 수 있어야 한다.</b> 그래서 「있으면 그 문을 거친다」와 「없으면 그대로
    /// 된다」 둘 다를 검사한다. 어느 한쪽만 재면 다른 쪽이 조용히 깨져도 아무 신호가 없다.
    /// </para>
    /// <para>
    /// <b>레이캐스트를 대역으로 바꾸지 않는다.</b> <see cref="Targeting.InteractionTargetSensor"/>는 봉인된
    /// 클래스라 대역으로 바꿔 끼울 수 없다. 그 대신 실제 콜라이더와 카메라를 두어 진짜 Raycast로 찾게
    /// 한다 — 이 프로젝트의 다른 EditMode 검사들도 같은 방식을 쓴다.
    /// </para>
    /// </remarks>
    public sealed class InteractAbilityTests
    {
        private readonly List<GameObject> _createdObjects = new();

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
        }

        [Test]
        public void WithoutAnAbilitySystemInteractionRunsDirectly()
        {
            var (controller, target) = CreateControllerWithTarget(withAbilitySystem: false);

            Assert.That(controller.TryInteract(), Is.True);

            Assert.That(target.InteractCount, Is.EqualTo(1));
        }

        [Test]
        public void WithAnAbilitySystemInteractionRunsThroughTheAbility()
        {
            var (controller, target) = CreateControllerWithTarget(withAbilitySystem: true);
            var abilitySystem = controller.GetComponent<GameplayAbilitySystemComponent>();

            Assert.That(controller.TryInteract(), Is.True);

            Assert.That(target.InteractCount, Is.EqualTo(1));
            Assert.That(
                abilitySystem.System.IsGranted(InteractAbilityDefinition.InteractTag),
                Is.True,
                "어빌리티를 거쳐 실행됐다면 그 어빌리티가 부여되어 있어야 한다.");
        }

        [Test]
        public void RepeatedInteractionsDoNotGrantTheAbilityAgain()
        {
            var (controller, _) = CreateControllerWithTarget(withAbilitySystem: true);
            var abilitySystem = controller.GetComponent<GameplayAbilitySystemComponent>();

            controller.TryInteract();
            controller.TryInteract();

            Assert.That(
                abilitySystem.System.GrantedAbilityCount,
                Is.EqualTo(1),
                "매 상호작용마다 새로 부여하면 부여 목록이 계속 자란다.");
        }

        [Test]
        public void UnavailableTargetsReportNoInteractionEvenThoughTheAbilityActivates()
        {
            var (controller, target) = CreateControllerWithTarget(withAbilitySystem: true, isAvailable: false);
            var abilitySystem = controller.GetComponent<GameplayAbilitySystemComponent>();

            var interacted = controller.TryInteract();

            Assert.That(
                interacted,
                Is.False,
                "어빌리티 발동 자체는 성공해도 대상이 불가능이면 반환값은 실패여야 한다.");
            Assert.That(target.InteractCount, Is.Zero);
            Assert.That(
                abilitySystem.System.GrantedAbilityCount,
                Is.EqualTo(1),
                "실패한 시도도 문을 여는 어빌리티는 부여해 둔다 — 다음 시도에서 다시 만들지 않기 위해서다.");
        }

        /// <summary>시선 앞 1미터에 상호작용 대상을 둔 컨트롤러를 만든다.</summary>
        /// <param name="withAbilitySystem">인터랙터에 어빌리티 시스템 컴포넌트를 붙일지 여부이다.</param>
        /// <param name="isAvailable">대상이 지금 상호작용 가능으로 평가할지 여부이다.</param>
        /// <returns>컨트롤러와 대상 대역이다.</returns>
        private (InteractionController Controller, FakeInteractable Target) CreateControllerWithTarget(
            bool withAbilitySystem, bool isAvailable = true)
        {
            var cameraObject = new GameObject("Camera");
            _createdObjects.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var controllerObject = new GameObject("Interactor");
            _createdObjects.Add(controllerObject);
            if (withAbilitySystem)
            {
                controllerObject.AddComponent<GameplayAbilitySystemComponent>();
            }

            var controller = controllerObject.AddComponent<InteractionController>();
            controller.Initialize(camera);

            var targetObject = new GameObject("Target");
            _createdObjects.Add(targetObject);
            targetObject.transform.position = new Vector3(0f, 0f, 1f);
            targetObject.AddComponent<BoxCollider>();
            var target = targetObject.AddComponent<FakeInteractable>();
            target.IsAvailable = isAvailable;

            return (controller, target);
        }

        private sealed class FakeInteractable : MonoBehaviour, IInteractable
        {
            public bool IsAvailable { get; set; } = true;

            public int InteractCount { get; private set; }

            public InteractionPrompt Prompt => default;

            public InteractionAvailability Evaluate(IInteractor interactor) => new(IsAvailable);

            public void Interact(IInteractor interactor) => InteractCount++;
        }
    }
}
