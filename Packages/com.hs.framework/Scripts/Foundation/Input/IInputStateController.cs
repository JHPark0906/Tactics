using System;

namespace HS.Framework.Foundation.Input
{
    /// <summary>
    /// 런타임 흐름에서 입력 활성화 상태를 제어하는 최소 계약이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 입력 상태는 두 층으로 구성된다. <see cref="SetInputEnabled"/>가 변경하는 기본 상태와,
    /// <see cref="AcquireInputBlock(InputBlockScope)"/>가 반환하는 차단 토큰이 겹쳐지는 일시적 차단 층이다.
    /// 어떤 범위에 살아 있는 차단 토큰이 하나라도 있으면 기본 상태와 무관하게 그 범위의 입력은 비활성화되고,
    /// 그 범위의 마지막 토큰이 해제되면 기본 상태가 다시 적용된다. 따라서 여러 호출부가 각자 토큰을
    /// 획득·해제해도 서로의 상태를 덮어쓰지 않고 안전하게 합성된다.
    /// </para>
    /// <para>
    /// 차단은 <see cref="InputBlockScope"/> 단위로 나뉜다. 게임플레이만 막으면 UI는 살아 있으므로,
    /// 모달 창은 자기 클릭과 취소 입력을 유지한 채로 그 아래 게임플레이만 멈출 수 있다.
    /// 범위를 밝히지 않는 <see cref="AcquireInputBlock()"/>은 가장 안전한 기본값인
    /// <see cref="InputBlockScope.Gameplay"/>로 동작한다.
    /// </para>
    /// </remarks>
    public interface IInputStateController
    {
        /// <summary>
        /// 현재 게임플레이 입력이 실제로 활성화되어 있는지 여부다.
        /// 기본 상태가 활성이더라도 게임플레이를 막는 차단 토큰이 살아 있으면 false를 반환한다.
        /// UI 입력까지 함께 확인하려면 <see cref="IsScopeEnabled"/>를 사용한다.
        /// </summary>
        bool IsInputEnabled { get; }

        /// <summary>
        /// 지정한 범위의 입력이 실제로 활성화되어 있는지 확인한다.
        /// 여러 범위를 함께 넘기면 그 범위가 <b>모두</b> 활성일 때만 true를 반환한다.
        /// </summary>
        /// <param name="scope">확인할 입력 범위이다.</param>
        /// <returns>해당 범위가 모두 활성이면 true이다.</returns>
        bool IsScopeEnabled(InputBlockScope scope);

        /// <summary>
        /// 기본 입력 활성화 상태를 변경한다. 이 상태는 모든 범위에 함께 적용된다.
        /// 차단 토큰이 살아 있는 동안에는 그 범위의 실제 활성화가 지연되며, 토큰이 모두 해제될 때 반영된다.
        /// </summary>
        void SetInputEnabled(bool isEnabled);

        /// <summary>
        /// 게임플레이 입력을 막는 차단 토큰을 획득한다. UI 입력은 살아 있다.
        /// 범위를 명시하지 않는 호출은 되돌릴 길을 남기는 이 기본값을 따른다.
        /// </summary>
        /// <returns>해제 시 차단을 되돌리는 토큰을 반환한다.</returns>
        IDisposable AcquireInputBlock();

        /// <summary>
        /// 지정한 범위의 입력을 막는 차단 토큰을 획득한다.
        /// 반환된 토큰을 해제하기 전까지 그 범위의 입력은 비활성화 상태로 유지된다.
        /// 여러 호출부가 동시에 토큰을 보유할 수 있으며, 범위별로 마지막 토큰이 해제될 때 기본 상태가 복원된다.
        /// </summary>
        /// <param name="scope">막을 입력 범위이며 <see cref="InputBlockScope.None"/>이면 아무것도 막지 않는다.</param>
        /// <returns>해제 시 차단을 되돌리는 토큰을 반환한다.</returns>
        IDisposable AcquireInputBlock(InputBlockScope scope);
    }
}
