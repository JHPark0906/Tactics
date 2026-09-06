using System;

namespace HS.Framework.Foundation.Input
{
    /// <summary>
    /// 입력 차단이 미치는 범위이다. 차단 주체가 무엇을 막아야 하는지 스스로 밝히게 한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 입력을 끄는 단위가 액션 에셋 전체 하나뿐이면, 게임플레이를 막으려는 주체가 UI까지 함께 꺼 버린다.
    /// 그러면 모달 창이 스스로 자기 클릭과 취소 입력을 죽이고, 전투 중에는 일시정지 경로가 사라진다.
    /// 그래서 차단을 범위로 나누어, 막아야 할 것만 막고 나머지는 살려 둔다.
    /// </para>
    /// <para>
    /// <b>범위를 고르는 기준.</b> 기본은 <see cref="Gameplay"/>이다. 플레이어가 세계에 개입하는 것을 막고 싶을 뿐이라면
    /// 거의 항상 이쪽이며, UI가 살아 있어야 사용자가 상황을 되돌릴 수 있다.
    /// <see cref="All"/>은 UI로도 할 수 있는 일이 없는 순간에만 쓴다. 씬 전환처럼 화면에 보이는 UI가
    /// 곧 사라질 예정이라 어떤 조작도 유효하지 않은 경우가 그렇다.
    /// </para>
    /// </remarks>
    [Flags]
    public enum InputBlockScope
    {
        /// <summary>아무것도 막지 않는다.</summary>
        None = 0,

        /// <summary>플레이어가 세계를 조작하는 입력을 막는다. UI 조작은 그대로 살아 있다.</summary>
        Gameplay = 1 << 0,

        /// <summary>메뉴와 창을 조작하는 UI 입력을 막는다.</summary>
        Ui = 1 << 1,

        /// <summary>게임플레이와 UI를 모두 막는다. 되돌릴 조작조차 필요 없는 순간에만 사용한다.</summary>
        All = Gameplay | Ui
    }
}
