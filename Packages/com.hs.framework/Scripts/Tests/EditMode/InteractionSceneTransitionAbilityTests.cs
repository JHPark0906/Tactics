using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HS.Framework.Ability.Abilities;
using HS.Framework.Interaction;
using HS.Framework.Interaction.Abilities;
using HS.Framework.Interaction.Actions;
using HS.Framework.Scene;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 씬 전환 상호작용이 재사용 가능한 어빌리티를 거쳐 실행되고,
    /// 로드 중 후속 요청이 거부되는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <b>재진입 검사가 이 파일의 중심이다.</b> 씬 전환 서비스 대역이 곧바로 끝나 버리면 어빌리티도
    /// 같은 틱에 곧바로 끝나므로, 「불러오는 동안 다시 온 요청을 막는가」를 절대 재지 못한다.
    /// <see cref="UniTaskCompletionSource"/>로 불러오기를 붙들어 둔 채로 그 사이를 들여다본다.
    /// </remarks>
    public sealed class InteractionSceneTransitionAbilityTests
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
        public void WithoutAnAbilitySystemNothingHappensAndItWarnsOnce()
        {
            var interactorObject = CreateObject("Interactor");
            var action = CreateAction();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("InteractionSceneTransitionAction"));

            action.Execute(new FakeInteractor(interactorObject));
            action.Execute(new FakeInteractor(interactorObject));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ExecutingSendsTheDestinationAsPayloadAndTheAbilityLoadsIt()
        {
            var (interactor, abilitySystem) = CreateInteractor();
            var service = new RecordingTransitionService();
            var action = CreateAction(service, useLoadingScene: false);

            action.Execute(interactor);

            Assert.That(service.Transitions, Has.Count.EqualTo(1));
            Assert.That(service.Transitions[0].LoadingScene, Is.Null);
            Assert.That(service.Transitions[0].Destination.ScenePath, Is.EqualTo("Assets/Scenes/Destination.unity"));
            Assert.That(
                abilitySystem.System.IsActive(InteractionSceneTransitionAbilityDefinition.SceneTransitionAbilityTag),
                Is.False,
                "곧바로 끝나는 대역이면 같은 틱에 어빌리티도 끝나 있어야 한다.");
        }

        [Test]
        public void ARequestWhileLoadingIsIgnoredUntilTheFirstOneFinishes()
        {
            var (interactor, abilitySystem) = CreateInteractor();
            var service = new RecordingTransitionService { PendingLoad = new UniTaskCompletionSource() };
            var action = CreateAction(service, useLoadingScene: false);

            action.Execute(interactor);
            Assert.That(service.Transitions, Has.Count.EqualTo(1), "첫 요청은 그대로 접수되어야 한다.");

            action.Execute(interactor);
            Assert.That(
                service.Transitions,
                Has.Count.EqualTo(1),
                "불러오는 동안 온 두 번째 요청은 어빌리티가 이미 활성 중이라 걸러져야 한다.");

            service.PendingLoad.TrySetResult();

            // 완료는 로딩 플래그를 내릴 뿐이다. 그 플래그를 보고 어빌리티를 실제로 끝내는 것은 정기 틱이며,
            // 그 틱은 시스템이 저절로 돌리지 않으므로 여기서 한 번 직접 돌려야 한다. deltaTime은 0보다
            // 커야 Tick이 실제로 처리한다(0 이하면 아무 일도 하지 않는다).
            abilitySystem.System.Tick(1f);

            action.Execute(interactor);
            Assert.That(service.Transitions, Has.Count.EqualTo(2), "끝난 뒤의 요청은 다시 접수되어야 한다.");
            Assert.That(
                abilitySystem.System.GrantedAbilityCount,
                Is.EqualTo(1),
                "매 실행마다 새로 부여하면 부여 목록이 계속 자란다.");
        }

        [Test]
        public void UsingALoadingSceneRoutesThroughTheLoadingScenePath()
        {
            var (interactor, _) = CreateInteractor();
            var service = new RecordingTransitionService();
            var action = CreateAction(service, useLoadingScene: true);

            action.Execute(interactor);

            Assert.That(service.Transitions, Has.Count.EqualTo(1));
            Assert.That(service.Transitions[0].LoadingScene?.ScenePath, Is.EqualTo("Assets/Scenes/Loading.unity"));
        }

        /// <summary>어빌리티 시스템을 붙인 인터랙터를 만든다.</summary>
        /// <returns>인터랙터 대역과 그 어빌리티 시스템 컴포넌트이다.</returns>
        private (FakeInteractor Interactor, GameplayAbilitySystemComponent AbilitySystem) CreateInteractor()
        {
            var interactorObject = CreateObject("Interactor");
            var abilitySystem = interactorObject.AddComponent<GameplayAbilitySystemComponent>();
            return (new FakeInteractor(interactorObject), abilitySystem);
        }

        /// <summary>도착 씬만 지정한 씬 전환 액션 컴포넌트를 만든다.</summary>
        /// <returns>만든 액션이다.</returns>
        private InteractionSceneTransitionAction CreateAction()
        {
            return CreateAction(new RecordingTransitionService(), useLoadingScene: false);
        }

        /// <summary>지정한 서비스를 주입한 씬 전환 액션 컴포넌트를 만든다.</summary>
        /// <param name="service">주입할 씬 전환 서비스이다.</param>
        /// <param name="useLoadingScene">로딩 씬을 경유하도록 설정할지 여부이다.</param>
        /// <returns>만든 액션이다.</returns>
        private InteractionSceneTransitionAction CreateAction(RecordingTransitionService service, bool useLoadingScene)
        {
            var actionObject = CreateObject("SceneTransitionAction");
            var action = actionObject.AddComponent<InteractionSceneTransitionAction>();
            action.InjectSceneTransitionService(service);

            var serialized = new UnityEditor.SerializedObject(action);
            serialized.FindProperty("useLoadingScene").boolValue = useLoadingScene;
            SetSceneReference(serialized.FindProperty("destinationScene"), "Assets/Scenes/Destination.unity");
            if (useLoadingScene)
            {
                SetSceneReference(serialized.FindProperty("loadingScene"), "Assets/Scenes/Loading.unity");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return action;
        }

        /// <summary>직렬화된 씬 참조 필드에 경로를 채운다. 검사에서는 GUID를 몰라도 되므로 경로만 채운다.</summary>
        /// <param name="property">채울 씬 참조 프로퍼티이다.</param>
        /// <param name="scenePath">채울 씬 경로이다.</param>
        private static void SetSceneReference(UnityEditor.SerializedProperty property, string scenePath)
        {
            property.FindPropertyRelative("scenePath").stringValue = scenePath;
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

        private sealed class FakeInteractor : IInteractor
        {
            internal FakeInteractor(GameObject gameObject)
            {
                GameObject = gameObject;
            }

            public GameObject GameObject { get; }
        }

        /// <summary>요청받은 전환을 기록하고, 붙들어 둘 수 있는 검사용 씬 전환 서비스이다.</summary>
        private sealed class RecordingTransitionService : ISceneTransitionService
        {
            public List<(SceneReference LoadingScene, SceneReference Destination)> Transitions { get; } = new();

            /// <summary>지정하면 전환 요청이 이것이 끝날 때까지 기다린다.</summary>
            public UniTaskCompletionSource PendingLoad { get; set; }

            public bool IsLoading => false;

            public Observable<SceneTransitionState> State => Observable.Empty<SceneTransitionState>();

            public UniTask LoadSceneAsync(SceneReference scene)
            {
                Transitions.Add((null, scene));
                return PendingLoad?.Task ?? UniTask.CompletedTask;
            }

            public UniTask LoadSceneAfterLoadingSceneAsync(SceneReference loadingScene, SceneReference destination)
            {
                Transitions.Add((loadingScene, destination));
                return PendingLoad?.Task ?? UniTask.CompletedTask;
            }

            public void ReportInitializationProgress(float progress)
            {
            }
        }
    }
}
