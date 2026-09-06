using System;
using UnityEngine;
using UnityEngine.Serialization;
using MessagePipe;
using VContainer;

namespace HS.Framework.Gameplay.Triggers
{
    /// <summary>
    /// 게임 진행을 변경하는 이벤트의 식별자와 발생 주체를 전달한다.
    /// </summary>
    public readonly struct GameTriggerEvent
    {
        /// <summary>
        /// 기획과 구독자가 공유하는 트리거 식별자를 가져온다.
        /// </summary>
        public string TriggerId { get; }

        /// <summary>
        /// 트리거를 발생시킨 GameObject를 가져온다.
        /// </summary>
        public GameObject Instigator { get; }

        /// <summary>
        /// 트리거 이벤트를 생성한다.
        /// </summary>
        public GameTriggerEvent(string triggerId, GameObject instigator)
        {
            TriggerId = triggerId;
            Instigator = instigator;
        }
    }

    /// <summary>
    /// Collider의 진입을 지정한 게임 트리거로 변환하는 공용 컴포넌트이다.
    /// Rigidbody와 Is Trigger Collider는 Unity 물리 시스템에서 설정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameTriggerVolume : MonoBehaviour
    {
        [FormerlySerializedAs("_triggerId")] [SerializeField] private string triggerId;

        private IPublisher<GameTriggerEvent> _triggerPublisher;

        /// <summary>트리거 이벤트 발행자를 주입받는다. 발행자가 없으면 진입해도 아무것도 발행하지 않는다.</summary>
        /// <param name="triggerPublisher">게임 트리거 이벤트의 발행자이다.</param>
        [Inject]
        public void InjectMessagePipePublisher(IPublisher<GameTriggerEvent> triggerPublisher)
        {
            _triggerPublisher = triggerPublisher ?? throw new ArgumentNullException(nameof(triggerPublisher));
        }

        /// <summary>
        /// 지정한 객체가 볼륨에 진입한 것처럼 트리거를 발행한다.
        /// </summary>
        public void Raise(GameObject instigator)
        {
            if (_triggerPublisher == null ||
                instigator == null || string.IsNullOrWhiteSpace(triggerId))
            {
                return;
            }

            var triggerEvent = new GameTriggerEvent(triggerId, instigator);
            _triggerPublisher.Publish(triggerEvent);
        }

        private void OnTriggerEnter(Collider other)
        {
            Raise(other.gameObject);
        }
    }
}
