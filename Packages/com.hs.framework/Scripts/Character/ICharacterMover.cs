using UnityEngine;

namespace HS.Framework.Character
{
    /// <summary>
    /// 캐릭터를 목적지로 이동시키는 구성요소를 정의한다.
    /// Unity NavMeshAgent 또는 CharacterController 구현체가 이 계약을 구현한다.
    /// </summary>
    public interface ICharacterMover
    {
        /// <summary>
        /// 마지막 이동 요청의 목적지에 도착했는지 여부이다.
        /// 진행 중인 이동 요청이 없으면 true를 반환한다.
        /// </summary>
        bool HasReachedDestination { get; }

        /// <summary>
        /// 지정한 월드 좌표로의 이동을 요청한다.
        /// 요청이 수락되면 true, 이동이 불가능한 상태이면 false를 반환한다.
        /// </summary>
        bool MoveTo(Vector3 destination);

        /// <summary>
        /// 진행 중인 이동을 중단한다.
        /// </summary>
        void Stop();
    }
}
