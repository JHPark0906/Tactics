using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HS.Framework.Scene;
using HS.Framework.Tests.Support;
using NUnit.Framework;
using R3;
using UnityEngine.SceneManagement;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 목적지 씬이 초기화 진행률을 한 스택 안에서 0에서 1까지 다 보고해 버려도 전환이 곧바로 완료되는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 실제 씬 오브젝트가 보고할 수 있는 자리는 <c>[Inject]</c> 메서드 본문뿐이며, 동기 초기화라면 그 한 스택 안에서
    /// 0과 1이 연달아 나간다. 게이트는 그 뒤에야 도는데, 그 시점에 새로 구독해 1을 기다리면 이미 흘러가 버린
    /// 값은 다시 오지 않는다. 그래서 게이트는 마지막 보고 값을 함께 보고, 이미 1에 닿았으면 기다리지 않는다.
    /// </para>
    /// <para>
    /// <see cref="SceneTransitionInitializationGateTests"/>는 게이트 전 한 번, 게이트 뒤 여러 번 보고하는 경우를
    /// 다룬다. 여기서는 게이트 전에 끝까지 보고하는 경우만 다루며, 게이트가 마지막 값을 보지 않으면
    /// 이 검사의 전환은 Pending으로 남는다.
    /// </para>
    /// </remarks>
    public sealed class SceneTransitionInitializationGateSynchronousReportTests
    {
        [Test]
        public void AReportThatReachesOneBeforeTheGateCompletesWithoutWaiting()
        {
            var loader = new RecordingSceneLoader();
            var startedPublisher = new TestPublisher<SceneLoadStartedEvent>();
            var completedPublisher = new TestPublisher<SceneLoadCompletedEvent>();
            var failedPublisher = new TestPublisher<SceneLoadFailedEvent>();
            using var coordinator = new SceneTransitionCoordinator(
                loader, new ImmediateDisplayPolicy(), startedPublisher, completedPublisher, failedPublisher);
            var observedInitializingValues = new List<float>();
            using var subscription = coordinator.State
                .Where(state => state.Phase == SceneLoadPhase.Initializing)
                .Subscribe(state => observedInitializingValues.Add(state.Value));
            // 로더의 콜백은 목적지 씬의 [Inject]가 도는 시점을 대신한다. 동기 초기화는 그 안에서 시작과 끝을 다 보고한다.
            loader.OnDestinationLoaded = () =>
            {
                coordinator.ReportInitializationProgress(0f);
                coordinator.ReportInitializationProgress(1f);
            };
            var destination = SceneFromBuildSettings("Bootstrap.unity");

            var task = coordinator.LoadSceneAsync(destination);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "게이트 전에 이미 1에 닿았으면 기다릴 것이 없다.");
            Assert.That(coordinator.IsLoading, Is.False);
            Assert.That(completedPublisher.Published, Has.Count.EqualTo(1));
            Assert.That(observedInitializingValues, Is.EqualTo(new[] { 0f, 1f }));
        }

        [Test]
        public void TheLoadersTrailingProgressAfterAReportDoesNotStepThePhaseBack()
        {
            // 실제 로더는 씬이 활성화된 뒤에야 마지막 진행률 1을 준다. 그 사이에 씬이 보고를 시작했으면
            // 그 로더 값은 버려야 단계가 Initializing에서 LoadingTarget으로 되돌아가지 않는다.
            var loader = new RecordingSceneLoader { InvokeCallbackBeforeFinalProgress = true };
            var startedPublisher = new TestPublisher<SceneLoadStartedEvent>();
            var completedPublisher = new TestPublisher<SceneLoadCompletedEvent>();
            var failedPublisher = new TestPublisher<SceneLoadFailedEvent>();
            using var coordinator = new SceneTransitionCoordinator(
                loader, new ImmediateDisplayPolicy(), startedPublisher, completedPublisher, failedPublisher);
            var observedPhases = new List<SceneLoadPhase>();
            using var subscription = coordinator.State.Subscribe(state => observedPhases.Add(state.Phase));
            loader.OnDestinationLoaded = () =>
            {
                coordinator.ReportInitializationProgress(0f);
                coordinator.ReportInitializationProgress(1f);
            };
            var destination = SceneFromBuildSettings("Bootstrap.unity");

            var task = coordinator.LoadSceneAsync(destination);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            var firstInitializing = observedPhases.IndexOf(SceneLoadPhase.Initializing);
            Assert.That(firstInitializing, Is.GreaterThanOrEqualTo(0), "보고가 있었으므로 Initializing 단계가 한 번은 나타난다.");
            Assert.That(
                observedPhases.GetRange(firstInitializing, observedPhases.Count - firstInitializing),
                Has.No.Member(SceneLoadPhase.LoadingTarget),
                "초기화 보고가 시작된 뒤에는 로더의 진행률이 단계를 되돌리지 않는다.");
            Assert.That(observedPhases[observedPhases.Count - 1], Is.EqualTo(SceneLoadPhase.Completed));
        }

        /// <summary>빌드 세팅에 등록된 씬 가운데 파일 이름이 맞는 것을 찾아 참조를 만든다.</summary>
        /// <remarks>
        /// 코디네이터가 실제 빌드 세팅을 확인하므로, 경로를 코드에 적어 두면 씬이 옮겨질 때마다 낡는다.
        /// 등록된 목록에서 찾으면 어느 폴더에 있든 같은 씬을 가리킨다.
        /// </remarks>
        /// <param name="fileName">찾을 씬 파일 이름이다.</param>
        /// <returns>빌드 세팅에 등록된 그 씬의 참조이다.</returns>
        private static SceneReference SceneFromBuildSettings(string fileName)
        {
            for (var index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(index);
                if (path.EndsWith(fileName, StringComparison.Ordinal))
                {
                    return SceneReference.Create(path);
                }
            }

            throw new InvalidOperationException($"빌드 세팅에 {fileName} 이 없다.");
        }

        /// <summary>목적지 씬이 로드된 직후를 알려 주는 것 말고는 아무것도 하지 않는 테스트용 씬 로더이다.</summary>
        private sealed class RecordingSceneLoader : ISceneLoader
        {
            public bool IsLoading { get; private set; }

            /// <summary>로드가 끝난 뒤 불릴 것이며, 목적지 씬의 오브젝트가 막 주입되는 시점을 대신한다.</summary>
            public Action OnDestinationLoaded { get; set; }

            /// <summary>
            /// 참이면 마지막 진행률 1보다 콜백을 먼저 부른다 — 실제 로더가 씬 활성화 뒤에야 1을 주는 순서다.
            /// </summary>
            public bool InvokeCallbackBeforeFinalProgress { get; set; }

            public UniTask LoadSceneAsync(SceneReference scene, Action<float> progress = null)
            {
                IsLoading = true;
                if (InvokeCallbackBeforeFinalProgress)
                {
                    OnDestinationLoaded?.Invoke();
                    progress?.Invoke(1f);
                }
                else
                {
                    progress?.Invoke(1f);
                    OnDestinationLoaded?.Invoke();
                }

                IsLoading = false;
                return UniTask.CompletedTask;
            }
        }

        private sealed class ImmediateDisplayPolicy : ILoadingSceneDisplayPolicy
        {
            public UniTask WaitForMinimumDisplayAsync() => UniTask.CompletedTask;
        }
    }
}
