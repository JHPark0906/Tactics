using UnityEngine;

namespace HS.Framework.Character
{
    /// <summary>
    /// 캐릭터가 바라보는 방향을 도맡는 계약이다. 이동과는 별개의 값이며, 각속도 한계 안에서 돈다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>바라보는 방향의 주인은 하나다.</b> 이 계약을 구현한 구성요소만 회전을 쓴다. 이동기는 이번 스텝의
    /// 이동 방향을 여기에 알릴 뿐 스스로 돌리지 않고, 행동 트리의 조준 자리는 바라볼 점을 두고 지울 뿐이다.
    /// 둘이 같은 스텝에 회전을 쓰면 구성요소 순서에 따라 마지막에 쓴 쪽이 이겨 결과가 흔들린다.
    /// </para>
    /// <para>
    /// <b>무엇을 볼지의 차례.</b> 바라볼 점이 있으면 그 점, 없으면 이동기가 알린 이동 방향, 그것도 없으면
    /// 지금 방향을 유지한다. 바라볼 점은 두는 쪽이 <see cref="ClearLookTarget"/>로 명시적으로 지운다.
    /// 시간이 지나면 저절로 지워지는 창은 두지 않는다. 그런 창은 스텝 수가 바뀌거나 자리가 한 틱 건너뛰면
    /// 조용히 뜻이 달라진다.
    /// </para>
    /// <para>
    /// <b>이동에 관여하지 않는다.</b> 도는 동안에도 이동기는 제 속도로 걷고, 걷는 동안에도 여기는 제 각속도로 돈다.
    /// 어느 쪽도 다른 쪽이 끝나기를 기다리지 않는다.
    /// </para>
    /// </remarks>
    public interface ICharacterFacing
    {
        /// <summary>바라보는 방향이 도는 속도(초당 도)이다.</summary>
        float TurnSpeed { get; }

        /// <summary>바라볼 점이 있는지 여부이다.</summary>
        bool HasLookTarget { get; }

        /// <summary>바라볼 점을 둔다. 지워질 때까지 이동 방향 대신 그 점을 향해 돈다.</summary>
        /// <param name="worldPoint">바라볼 세계 좌표이다.</param>
        void LookAt(Vector3 worldPoint);

        /// <summary>바라볼 점을 지운다. 그 뒤로는 이동 방향을 본다.</summary>
        void ClearLookTarget();

        /// <summary>지금 정면과 그 점 사이의 수평 각(도)이다. 점이 자기 자리와 같으면 0이다.</summary>
        /// <param name="worldPoint">잴 세계 좌표이다.</param>
        /// <returns>0에서 180 사이의 각(도)이다.</returns>
        float AngleTo(Vector3 worldPoint);

        /// <summary>이동기가 이번 스텝에 움직인 방향을 알린다. 바라볼 점이 없을 때 이쪽을 본다.</summary>
        /// <param name="worldDirection">이번 스텝의 이동 방향이며, 길이가 0이면 무시한다.</param>
        void ReportMovementDirection(Vector3 worldDirection);
    }
}
