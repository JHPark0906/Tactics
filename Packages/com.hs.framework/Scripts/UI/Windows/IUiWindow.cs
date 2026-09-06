namespace HS.Framework.UI.Windows
{
    /// <summary>
    /// 창 스택 규칙이 참조하는 창의 최소 계약이다.
    /// 순수 로직인 UiWindowStack을 MonoBehaviour와 분리해 테스트할 수 있도록 한다.
    /// </summary>
    public interface IUiWindow
    {
        /// <summary>
        /// 모달 창인지 여부를 가져온다. 모달 창이 열려 있으면 아래 창과 게임플레이 입력이 차단 대상이 된다.
        /// </summary>
        bool IsModal { get; }

        /// <summary>
        /// 취소(ESC) 입력으로 닫을 수 있는지 여부를 가져온다.
        /// </summary>
        bool CloseOnCancel { get; }
    }
}
