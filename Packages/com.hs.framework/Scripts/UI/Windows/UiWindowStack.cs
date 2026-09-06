using System;
using System.Collections.Generic;

namespace HS.Framework.UI.Windows
{
    /// <summary>
    /// 열린 창의 스택 규칙을 구현하는 순수 C# 클래스이다.
    /// 인덱스 0이 가장 아래 창이고 마지막 인덱스가 최상단 창이다.
    /// </summary>
    public sealed class UiWindowStack
    {
        private readonly List<IUiWindow> _windows = new();

        /// <summary>
        /// 현재 열린 창의 수를 가져온다.
        /// </summary>
        public int Count => _windows.Count;

        /// <summary>
        /// 아래에서 위 순서로 나열된 열린 창 목록을 가져온다.
        /// </summary>
        public IReadOnlyList<IUiWindow> Windows => _windows;

        /// <summary>
        /// 최상단 창을 가져온다. 열린 창이 없으면 null을 반환한다.
        /// </summary>
        public IUiWindow Top => _windows.Count > 0 ? _windows[^1] : null;

        /// <summary>
        /// 스택에 모달 창이 하나라도 있는지 여부를 가져온다.
        /// </summary>
        public bool HasModal
        {
            get
            {
                foreach (var window in _windows)
                {
                    if (window.IsModal)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 가장 위에 있는 모달 창을 가져온다. 모달 창이 없으면 null을 반환한다.
        /// </summary>
        public IUiWindow TopModal
        {
            get
            {
                for (var index = _windows.Count - 1; index >= 0; index--)
                {
                    if (_windows[index].IsModal)
                    {
                        return _windows[index];
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// 창이 스택에 포함되어 있는지 여부를 반환한다.
        /// </summary>
        /// <param name="window">확인할 창이다.</param>
        public bool Contains(IUiWindow window)
        {
            return window != null && _windows.Contains(window);
        }

        /// <summary>
        /// 창을 최상단에 푸시한다. 이미 스택에 있으면 최상단으로 끌어올린다.
        /// </summary>
        /// <param name="window">푸시할 창이다.</param>
        public void Push(IUiWindow window)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            _windows.Remove(window);
            _windows.Add(window);
        }

        /// <summary>
        /// 최상단 창을 스택에서 제거하고 반환한다. 스택이 비어 있으면 null을 반환한다.
        /// </summary>
        public IUiWindow Pop()
        {
            if (_windows.Count == 0)
            {
                return null;
            }

            var top = _windows[^1];
            _windows.RemoveAt(_windows.Count - 1);
            return top;
        }

        /// <summary>
        /// 창을 스택의 위치와 무관하게 제거한다.
        /// </summary>
        /// <param name="window">제거할 창이다.</param>
        /// <returns>창이 스택에 있어 제거됐으면 true를 반환한다.</returns>
        public bool Remove(IUiWindow window)
        {
            return window != null && _windows.Remove(window);
        }

        /// <summary>
        /// 모든 창을 스택에서 제거한다.
        /// </summary>
        public void Clear()
        {
            _windows.Clear();
        }

        /// <summary>
        /// 창이 자기보다 위에 있는 모달 창에 의해 조작이 차단되는지 여부를 반환한다.
        /// 스택에 없는 창은 열린 모달 창이 하나라도 있으면 차단된 것으로 본다.
        /// </summary>
        /// <param name="window">확인할 창이다.</param>
        public bool IsBlockedByModal(IUiWindow window)
        {
            var windowIndex = window != null ? _windows.IndexOf(window) : -1;
            if (windowIndex < 0)
            {
                return HasModal;
            }

            for (var index = windowIndex + 1; index < _windows.Count; index++)
            {
                if (_windows[index].IsModal)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 취소(ESC) 입력이 닫아야 할 창을 반환한다.
        /// 최상단 창이 취소로 닫기를 허용할 때만 그 창을 반환하고, 허용하지 않거나 스택이 비었으면 null을 반환한다.
        /// 최상단 아래의 창은 순서를 건너뛰어 닫지 않는다.
        /// </summary>
        public IUiWindow PeekCancelTarget()
        {
            var top = Top;
            return top != null && top.CloseOnCancel ? top : null;
        }
    }
}
