using System;
using HS.Framework.Persistence;
using HS.Framework.Scene;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace HS.Framework.Bootstrap
{
    /// <summary>
    /// 씬 로딩이 완료될 때마다 자동 저장이 실행되도록 <see cref="SaveOrchestrator"/>를 이벤트 버스에 연결한다.
    /// </summary>
    /// <remarks>
    /// 저장 위치와 백엔드는 프로젝트 정책이므로 프레임워크가 결정하지 않는다.
    /// 게임 프로젝트가 자신이 고른 <see cref="ISaveDataStorage"/>로 <see cref="SaveOrchestrator"/>를 만들어
    /// 프로젝트가 구성한 저장 조정자를 주입받아 자동 저장 훅을 부착하고 수명주기에 맞춰 해제한다.
    /// 프로젝트가 담당하는 부분은 백엔드 선택과 참여자 등록뿐이고,
    /// 훅을 언제 붙이고 언제 떼는지는 프레임워크가 책임진다.
    /// 이벤트 broker와 저장 서비스는 <see cref="Runtime.FrameworkLifetimeScope"/>가 구성하므로 프로젝트가 따로 준비할 필요가 없다.
    /// 다른 시점에 자동 저장을 걸고 싶으면 <see cref="SaveOrchestrator.AttachAutoSaveTrigger{TEvent}"/>를
    /// 원하는 이벤트로 직접 호출한다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SceneLoadAutoSaveBinder : MonoBehaviour
    {
        private SaveOrchestrator _saveOrchestrator;
        private ISubscriber<SceneLoadCompletedEvent> _sceneLoadCompletedSubscriber;
        private IDisposable _autoSaveSubscription;

        /// <summary>
        /// 자동 저장 훅이 현재 부착되어 있는지 여부를 가져온다.
        /// </summary>
        public bool IsAttached => _autoSaveSubscription != null;

        /// <summary>
        /// 저장 조정자와 씬 로드 완료 구독자를 주입받고, 둘 다 준비되고 활성 상태면 자동 저장 훅을 건다.
        /// </summary>
        /// <param name="saveOrchestrator">프로젝트가 구성한 저장 조정자이다.</param>
        /// <param name="sceneLoadCompletedSubscriber">씬 로드 완료 메시지의 구독자이다.</param>
        [Inject]
        public void InjectMessagePipeDependencies(
            SaveOrchestrator saveOrchestrator,
            ISubscriber<SceneLoadCompletedEvent> sceneLoadCompletedSubscriber)
        {
            _saveOrchestrator = saveOrchestrator;
            _sceneLoadCompletedSubscriber = sceneLoadCompletedSubscriber;
            RefreshSubscription();
        }

        private void OnEnable()
        {
            RefreshSubscription();
        }

        private void OnDisable()
        {
            DetachAutoSaveTrigger();
        }

        private void OnDestroy()
        {
            DetachAutoSaveTrigger();
            _saveOrchestrator = null;
            _sceneLoadCompletedSubscriber = null;
        }

        /// <summary>
        /// 저장 조정자와 이벤트 구독자가 모두 준비되고 컴포넌트가 활성 상태일 때만 자동 저장 훅을 부착한다.
        /// 의존성은 등록 순서에 따라 따로 도착하므로 주입될 때마다 다시 판단하며,
        /// 이미 부착된 훅은 먼저 해제해 중복 구독이 쌓이지 않게 한다.
        /// </summary>
        private void RefreshSubscription()
        {
            DetachAutoSaveTrigger();
            if (!isActiveAndEnabled || _saveOrchestrator == null)
            {
                return;
            }

            _autoSaveSubscription = _sceneLoadCompletedSubscriber == null
                ? null
                : _saveOrchestrator.AttachAutoSaveTrigger(_sceneLoadCompletedSubscriber);
        }

        /// <summary>
        /// 부착된 자동 저장 훅을 해제한다. 부착되어 있지 않으면 아무것도 하지 않는다.
        /// </summary>
        private void DetachAutoSaveTrigger()
        {
            _autoSaveSubscription?.Dispose();
            _autoSaveSubscription = null;
        }
    }
}
