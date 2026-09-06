using System;
using UnityEngine;

namespace HS.Framework.UI.Windows
{
    /// <summary>
    /// 열기/닫기 수명주기를 가진 UI 창의 기반 컴포넌트이다.
    /// ViewBase&lt;T&gt; 상속을 강제하지 않으므로 MVVM View와 같은 GameObject에 조합해 사용할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiWindowBase : MonoBehaviour, IUiWindow
    {
        /// <summary>
        /// 모달 창인지 여부이다. 모달 창이 열려 있으면 아래 창 조작과 게임플레이 입력이 차단 대상이 된다.
        /// </summary>
        [Header("Window")]
        [SerializeField] private bool isModal;

        /// <summary>
        /// 취소(ESC) 입력으로 닫을 수 있는지 여부이다.
        /// </summary>
        [SerializeField] private bool closeOnCancel = true;

        /// <summary>
        /// 정렬 순서 적용에 사용할 캐시된 캔버스이다.
        /// </summary>
        private Canvas _canvas;

        /// <summary>
        /// 모달 창인지 여부를 가져온다. 기본값은 인스펙터 설정을 따르며, 파생 클래스가 코드로 고정할 수 있다.
        /// 관리자는 창이 열리거나 닫힐 때 이 값을 다시 확인하므로, 열려 있는 동안 값이 바뀌면 즉시 반영되지 않는다.
        /// </summary>
        public virtual bool IsModal => isModal;

        /// <summary>
        /// 취소(ESC) 입력으로 닫을 수 있는지 여부를 가져온다.
        /// 기본값은 인스펙터 설정을 따르며, 파생 클래스가 코드로 고정할 수 있다.
        /// </summary>
        public virtual bool CloseOnCancel => closeOnCancel;

        /// <summary>
        /// 창이 현재 열려 있는지 여부를 가져온다.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>열기 또는 표시 요청 교체마다 바뀌며, 관리자가 이전 요청과 재개방된 요청을 구분한다.</summary>
        internal long RequestVersion { get; private set; }

        /// <summary>
        /// 창이 열린 직후 발생한다.
        /// </summary>
        public event Action<UiWindowBase> Opened;

        /// <summary>
        /// 창이 닫힌 직후 발생한다. 열린 채 파괴될 때도 발생한다.
        /// </summary>
        public event Action<UiWindowBase> Closed;

        /// <summary>
        /// 창이 닫히기를 요청했을 때 발생한다. 구독자(관리자)가 실제 닫기를 수행한다.
        /// </summary>
        public event Action<UiWindowBase> CloseRequested;

        /// <summary>
        /// 창을 연다. 이미 열려 있으면 아무것도 하지 않는다.
        /// </summary>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            BeginRequest();
            gameObject.SetActive(true);
            OnOpened();
            Opened?.Invoke(this);
        }

        /// <summary>
        /// 창을 즉시 닫는다. 열려 있지 않으면 아무것도 하지 않는다.
        /// </summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            gameObject.SetActive(false);
            Closed?.Invoke(this);
            // 요청자 콜백은 닫힘 처리를 마친 뒤 실행한다. 그 안에서 다시 연 창을
            // 이전 Close 호출이 비활성화하거나 관리자 스택에서 제거하면 안 된다.
            OnClosed();
        }

        /// <summary>
        /// 창 닫기를 요청한다. 관리자가 구독 중이면 관리자에게 위임하고, 아니면 스스로 닫는다.
        /// </summary>
        public void RequestClose()
        {
            if (!IsOpen)
            {
                return;
            }

            if (CloseRequested != null)
            {
                CloseRequested.Invoke(this);
            }
            else
            {
                Close();
            }
        }

        /// <summary>
        /// 창의 캔버스 정렬 순서를 지정한다. 캔버스가 없으면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="sortingOrder">적용할 정렬 순서 값이다.</param>
        public void SetSortingOrder(int sortingOrder)
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }

            if (_canvas == null)
            {
                return;
            }

            _canvas.overrideSorting = true;
            _canvas.sortingOrder = sortingOrder;
        }

        /// <summary>
        /// 창이 열린 직후 파생 클래스가 추가 처리를 수행한다.
        /// </summary>
        protected virtual void OnOpened()
        {
        }

        /// <summary>
        /// 새 표시 요청을 시작한다. 이미 열린 창에서 요청자를 교체하는 Show 구현도 이를 호출해야
        /// 진행 중인 전체 닫기가 새 요청을 이전 요청으로 오인해 닫지 않는다.
        /// </summary>
        protected void BeginRequest()
        {
            RequestVersion++;
        }

        /// <summary>
        /// 창 비활성화와 닫힘 알림을 마친 뒤 파생 클래스가 추가 처리를 수행한다.
        /// </summary>
        protected virtual void OnClosed()
        {
        }

        /// <summary>
        /// 열린 채 파괴되면 닫힘 이벤트만 발생시켜 관리자 스택과 상태를 맞춘다.
        /// 파괴 중에는 OnClosed 훅과 GameObject 조작을 수행하지 않는다.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            Closed?.Invoke(this);
        }
    }
}
