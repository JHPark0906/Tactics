using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using R3;
using UnityEngine;

namespace HS.Framework.Scene
{
    /// <summary>단일 씬 로더와 로딩 화면 정책을 조합해 클라이언트 전환 수명주기를 관리한다.</summary>
    public sealed class SceneTransitionCoordinator : ISceneTransitionService, IDisposable
    {
        private ISceneLoader _sceneLoader;
        private ILoadingSceneDisplayPolicy _loadingSceneDisplayPolicy;
        private Action<SceneLoadStartedEvent> _publishStarted;
        private Action<SceneLoadCompletedEvent> _publishCompleted;
        private Action<SceneLoadFailedEvent> _publishFailed;
        private SceneReference _recoveryScene;
        private readonly ReactiveProperty<SceneTransitionState> _state = new(SceneTransitionState.Idle);
        private readonly Subject<float> _initializationProgress = new();
        private long _nextOperationId;
        private bool _isLoading;
        private bool _hasInitializationReport;
        private float _lastInitializationProgress;
        private long _currentOperationId;
        private DateTime _currentStartedAtUtc;
        private SceneReference _currentDestination;

        /// <inheritdoc />
        public bool IsLoading => _isLoading;

        /// <inheritdoc />
        public Observable<SceneTransitionState> State => _state;

        /// <summary>MessagePipe publisher를 사용하는 씬 전환 조정자를 생성한다.</summary>
        public SceneTransitionCoordinator(
            ISceneLoader sceneLoader,
            ILoadingSceneDisplayPolicy loadingSceneDisplayPolicy,
            IPublisher<SceneLoadStartedEvent> startedPublisher,
            IPublisher<SceneLoadCompletedEvent> completedPublisher,
            IPublisher<SceneLoadFailedEvent> failedPublisher,
            SceneReference recoveryScene = null)
        {
            Initialize(sceneLoader, loadingSceneDisplayPolicy, recoveryScene);
            startedPublisher = startedPublisher ?? throw new ArgumentNullException(nameof(startedPublisher));
            completedPublisher = completedPublisher ?? throw new ArgumentNullException(nameof(completedPublisher));
            failedPublisher = failedPublisher ?? throw new ArgumentNullException(nameof(failedPublisher));
            _publishStarted = message => startedPublisher.Publish(message);
            _publishCompleted = message => completedPublisher.Publish(message);
            _publishFailed = message => failedPublisher.Publish(message);
        }

        private void Initialize(
            ISceneLoader sceneLoader,
            ILoadingSceneDisplayPolicy loadingSceneDisplayPolicy,
            SceneReference recoveryScene)
        {
            _sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            _loadingSceneDisplayPolicy = loadingSceneDisplayPolicy ??
                                         throw new ArgumentNullException(nameof(loadingSceneDisplayPolicy));
            _recoveryScene = recoveryScene;
        }

        /// <inheritdoc />
        public UniTask LoadSceneAsync(SceneReference scene)
        {
            SceneLoader.ThrowIfSceneCannotBeLoaded(scene);
            return ExecuteAsync(null, scene);
        }

        /// <inheritdoc />
        public UniTask LoadSceneAfterLoadingSceneAsync(SceneReference loadingScene, SceneReference destination)
        {
            SceneLoader.ThrowIfSceneCannotBeLoaded(loadingScene);
            SceneLoader.ThrowIfSceneCannotBeLoaded(destination);
            if (loadingScene.Equals(destination))
            {
                throw new ArgumentException("Loading scene and destination must be different.", nameof(loadingScene));
            }

            return ExecuteAsync(loadingScene, destination);
        }

        private async UniTask ExecuteAsync(SceneReference loadingScene, SceneReference destination)
        {
            if (_isLoading)
            {
                throw new InvalidOperationException("A scene transition is already in progress.");
            }

            _isLoading = true;
            var operationId = ++_nextOperationId;
            var startedAtUtc = DateTime.UtcNow;
            var failedScene = destination;
            _currentOperationId = operationId;
            _currentStartedAtUtc = startedAtUtc;
            _currentDestination = destination;
            try
            {
                _publishStarted(new SceneLoadStartedEvent(destination, loadingScene != null));
                if (loadingScene != null)
                {
                    Report(operationId, startedAtUtc, SceneLoadPhase.LoadingScreen, 0f, destination);
                    failedScene = loadingScene;
                    await _sceneLoader.LoadSceneAsync(loadingScene);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    await _loadingSceneDisplayPolicy.WaitForMinimumDisplayAsync();
                }

                Report(operationId, startedAtUtc, SceneLoadPhase.LoadingTarget, 0f, destination);
                failedScene = destination;
                _hasInitializationReport = false;
                _lastInitializationProgress = 0f;
                // 로더의 마지막 진행률(1)은 씬이 활성화된 뒤에 오므로, 씬이 그 사이에 초기화를 보고하기 시작했으면
                // 그 뒤의 로더 값은 버린다 — 받으면 단계가 Initializing에서 LoadingTarget으로 되돌아가 보인다.
                await _sceneLoader.LoadSceneAsync(destination, value =>
                {
                    if (!_hasInitializationReport)
                    {
                        Report(operationId, startedAtUtc, SceneLoadPhase.LoadingTarget, value, destination);
                    }
                });

                // 목적지 씬의 오브젝트 그래프는 이미 다 만들어졌다. 그 안의 무언가가 방금 ReportInitializationProgress를
                // 스스로 불렀다면(씬 오브젝트의 Awake와 주입은 이 await가 재개되기 전에 이미 끝나 있고, 서비스 참조는
                // 주입에서만 오므로 보고는 주입 본문에서 시작된다) 1에 닿을 때까지 기다린다. 아무도 부르지 않았으면
                // 이 대기는 완전히 건너뛴다.
                // 마지막 보고 값도 함께 본다 — 보고가 한 스택 안에서 0에서 1까지 다 지나갔으면 그 값들은 구독자 없이
                // 흘러가 버렸으므로, 여기서 새로 구독해 1을 기다리면 영원히 오지 않는다.
                if (_hasInitializationReport && _lastInitializationProgress < 1f)
                {
                    await _initializationProgress.FirstAsync(value => value >= 1f);
                }

                Report(operationId, startedAtUtc, SceneLoadPhase.Completed, 1f, destination);
                _publishCompleted(new SceneLoadCompletedEvent(destination));
            }
            catch (Exception exception)
            {
                var wasRecovered = false;
                Exception recoveryException = null;
                if (CanRecoverFrom(failedScene))
                {
                    try
                    {
                        Report(
                            operationId,
                            startedAtUtc,
                            SceneLoadPhase.Recovering,
                            0f,
                            destination,
                            exception,
                            _recoveryScene);
                        await _sceneLoader.LoadSceneAsync(_recoveryScene, value =>
                            Report(
                                operationId,
                                startedAtUtc,
                                SceneLoadPhase.Recovering,
                                value,
                                destination,
                                exception,
                                _recoveryScene));
                        wasRecovered = true;
                        Report(
                            operationId,
                            startedAtUtc,
                            SceneLoadPhase.Recovered,
                            1f,
                            destination,
                            exception,
                            _recoveryScene);
                    }
                    catch (Exception recoveryFailure)
                    {
                        recoveryException = recoveryFailure;
                    }
                }

                if (!wasRecovered)
                {
                    Report(
                        operationId,
                        startedAtUtc,
                        SceneLoadPhase.Failed,
                        0f,
                        destination,
                        recoveryException == null
                            ? exception
                            : new AggregateException(exception, recoveryException),
                        _recoveryScene);
                }

                _publishFailed(new SceneLoadFailedEvent(
                    destination,
                    exception,
                    _recoveryScene,
                    wasRecovered,
                    recoveryException));
                if (recoveryException != null)
                {
                    throw new AggregateException(
                        "씬 전환과 MainMenu 안전 복귀가 모두 실패했습니다.",
                        exception,
                        recoveryException);
                }

                throw;
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void Report(
            long operationId,
            DateTime startedAtUtc,
            SceneLoadPhase phase,
            float value,
            SceneReference destination,
            Exception exception = null,
            SceneReference recoveryScene = null)
        {
            _state.Value = new SceneTransitionState(
                operationId,
                startedAtUtc,
                phase,
                Mathf.Clamp01(value),
                destination,
                exception,
                recoveryScene);
        }

        private bool CanRecoverFrom(SceneReference failedScene)
        {
            return _recoveryScene is { IsAssigned: true } &&
                   SceneLoader.CanLoadScene(_recoveryScene) &&
                   !Equals(failedScene, _recoveryScene);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 전환이 진행 중이 아닐 때의 호출은 조용히 무시한다 — 보고할 전환 자체가 없다. 첫 호출이 그 씬을
        /// "초기화를 보고하는 씬"으로 등록하는 것을 겸한다. 카운터나 이벤트 버스를 따로 두지 않은 것은
        /// 등록과 첫 보고가 같은 사건이기 때문이다.
        /// </remarks>
        public void ReportInitializationProgress(float progress)
        {
            if (!_isLoading)
            {
                return;
            }

            var clamped = Mathf.Clamp01(progress);
            _hasInitializationReport = true;
            _lastInitializationProgress = clamped;
            Report(_currentOperationId, _currentStartedAtUtc, SceneLoadPhase.Initializing, clamped, _currentDestination);
            _initializationProgress.OnNext(clamped);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _state.Dispose();
            _initializationProgress.Dispose();
        }
    }
}
