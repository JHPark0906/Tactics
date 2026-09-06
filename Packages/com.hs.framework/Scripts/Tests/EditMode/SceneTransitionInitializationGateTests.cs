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
    /// 목적지 씬이 스스로 초기화 진행률을 보고할 때만 완료가 그만큼 미뤄지는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ISceneTransitionService.ReportInitializationProgress"/>를 호출하지 않는 씬에서는
    /// 추가 대기가 없고 <see cref="SceneLoadPhase.Initializing"/> 단계도 나타나지 않아야 한다.
    /// </para>
    /// <para>
    /// <b>보고가 있는 씬의 순서.</b> 목적지 씬의 오브젝트 그래프는 로더가 끝난 시점에 이미 다 만들어져 있고,
    /// 그 안의 무언가가 스스로 보고를 시작하는 자리는 그 오브젝트의 주입 본문이다. 여기서는
    /// 그 시점을 로더의 씬 로드 콜백 안에서 <see cref="ISceneTransitionService.ReportInitializationProgress"/>를
    /// 직접 부르는 것으로 흉내 낸다.
    /// </para>
    /// </remarks>
    public sealed class SceneTransitionInitializationGateTests
    {
        [Test]
        public void WithoutAnyReportTheTransitionCompletesImmediatelyAndNeverReportsInitializing()
        {
            var loader = new RecordingSceneLoader();
            var startedPublisher = new TestPublisher<SceneLoadStartedEvent>();
            var completedPublisher = new TestPublisher<SceneLoadCompletedEvent>();
            var failedPublisher = new TestPublisher<SceneLoadFailedEvent>();
            using var coordinator = new SceneTransitionCoordinator(
                loader, new ImmediateDisplayPolicy(), startedPublisher, completedPublisher, failedPublisher);
            var observedPhases = new List<SceneLoadPhase>();
            using var subscription = coordinator.State.Subscribe(state => observedPhases.Add(state.Phase));
            var destination = SceneFromBuildSettings("Bootstrap.unity");

            var task = coordinator.LoadSceneAsync(destination);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "아무도 보고하지 않으면 대기 없이 그대로 끝나야 한다.");
            Assert.That(completedPublisher.Published, Has.Count.EqualTo(1));
            Assert.That(observedPhases, Has.No.Member(SceneLoadPhase.Initializing), "보고가 없으면 그 단계 자체가 나타나면 안 된다.");
        }

        [Test]
        public void AReportedProgressDelaysCompletionUntilItReachesOne()
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
            // 로더의 콜백은 목적지 씬이 다 만들어진 시점을 대신한다. 그 자리에서 첫 보고를 하는 것이
            // 씬 오브젝트의 Awake가 실제로 하는 일과 같은 순서다.
            loader.OnDestinationLoaded = () => coordinator.ReportInitializationProgress(0f);
            var destination = SceneFromBuildSettings("Bootstrap.unity");

            var task = coordinator.LoadSceneAsync(destination);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending), "보고가 있었으면 1에 닿기 전에는 끝나면 안 된다.");
            Assert.That(coordinator.IsLoading, Is.True);
            Assert.That(completedPublisher.Published, Is.Empty);

            coordinator.ReportInitializationProgress(0.5f);
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending), "0.5는 아직 1이 아니므로 끝나면 안 된다.");

            coordinator.ReportInitializationProgress(1f);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "1에 닿으면 그 자리에서 완료되어야 한다.");
            Assert.That(coordinator.IsLoading, Is.False);
            Assert.That(completedPublisher.Published, Has.Count.EqualTo(1));
            Assert.That(observedInitializingValues, Is.EqualTo(new[] { 0f, 0.5f, 1f }));
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

            /// <summary>로드가 끝난 뒤 불릴 것이며, 목적지 씬의 오브젝트가 막 활성화된 시점을 대신한다.</summary>
            public Action OnDestinationLoaded { get; set; }

            public UniTask LoadSceneAsync(SceneReference scene, Action<float> progress = null)
            {
                IsLoading = true;
                progress?.Invoke(1f);
                IsLoading = false;
                OnDestinationLoaded?.Invoke();
                return UniTask.CompletedTask;
            }
        }

        private sealed class ImmediateDisplayPolicy : ILoadingSceneDisplayPolicy
        {
            public UniTask WaitForMinimumDisplayAsync() => UniTask.CompletedTask;
        }
    }
}
