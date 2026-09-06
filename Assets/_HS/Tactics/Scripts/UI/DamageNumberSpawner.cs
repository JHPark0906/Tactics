using System;
using HS.Framework.Foundation.Collections;
using HS.Framework.Gameplay.Health;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace HS.Tactics.UI
{
    /// <summary>
    /// 피해가 실제로 적용될 때마다 맞은 자리 위에 피해량 숫자를 띄우는 서비스이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>표시용 팝업은 오브젝트 풀에서 빌린다.</b> 전투 중에는 피해가 잦게 일어나므로, 그때마다
    /// 오브젝트를 만들고 없애면 매 타격마다 GC 압력이 쌓인다. <see cref="ObjectPool{T}"/>가 이미
    /// 그 문제를 풀어 두었으므로(<c>SoundService</c>의 효과음 소스 풀과 같은 자리) 새 장치를 짓지 않고
    /// 그대로 쓴다. 팝업 프리팹을 따로 두지 않는 것은 이 컴포넌트가 스스로 만들 만큼 단순해서이다.
    /// </para>
    /// <para>
    /// <b>구독 방식은 <c>BattleOutcomeService</c>와 같다.</b> 주입과 활성 상태에 따라 구독을 걸고 놓는
    /// 자리가 그 컴포넌트를 그대로 따른다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DamageNumberSpawner : MonoBehaviour
    {
        [Tooltip("맞은 대상의 위치에서 얼마나 위에 숫자를 띄울지(미터)이다. 임시값 — 유닛 프리팹의 실제 키에 맞춰 나중에 조정한다.")]
        [SerializeField]
        [Min(0f)]
        private float spawnHeight = 2f;

        [Tooltip("팝업 글자 크기이다. 월드 공간 TextMeshPro 기준 임시값 — 실제 프리팹 스케일에 맞춰 나중에 조정한다.")]
        [SerializeField]
        [Min(0.1f)]
        private float fontSize = 4f;

        [Tooltip("씬 시작 시 미리 만들어 둘 팝업 수이다.")]
        [SerializeField]
        [Min(0)]
        private int prewarmCount = 4;

        private ObjectPool<DamageNumberPopup> _pool;
        private ISubscriber<DamageAppliedEvent> _damageSubscriber;
        private IDisposable _damageSubscription;

        /// <summary>피해 이벤트 구독자를 주입받고, 활성 상태면 곧바로 구독한다.</summary>
        /// <param name="damageSubscriber">프레임워크 피해 적용 이벤트의 구독자이다.</param>
        [Inject]
        public void InjectMessagePipeDependencies(ISubscriber<DamageAppliedEvent> damageSubscriber)
        {
            _damageSubscriber = damageSubscriber ?? throw new ArgumentNullException(nameof(damageSubscriber));
            RefreshSubscription();
        }

        private void Awake()
        {
            _pool = new ObjectPool<DamageNumberPopup>(CreatePopup, OnAcquire, OnRelease);
            _pool.Prewarm(prewarmCount);
        }

        private void OnEnable()
        {
            RefreshSubscription();
        }

        private void OnDisable()
        {
            DetachSubscription();
        }

        private void OnDestroy()
        {
            DetachSubscription();
            _damageSubscriber = null;
        }

        /// <summary>
        /// 구독자가 준비되고 활성 상태일 때만 피해 이벤트를 구독한다.
        /// 주입이 여러 번 도착해도 구독이 쌓이지 않도록 기존 구독을 먼저 놓는다.
        /// </summary>
        private void RefreshSubscription()
        {
            DetachSubscription();
            if (!isActiveAndEnabled)
            {
                return;
            }

            _damageSubscription = _damageSubscriber?.Subscribe(OnDamageApplied);
        }

        private void DetachSubscription()
        {
            _damageSubscription?.Dispose();
            _damageSubscription = null;
        }

        /// <summary>피해가 실제로 적용되면 그 자리 위에 숫자를 띄운다.</summary>
        /// <param name="damageEvent">프레임워크가 발행한 피해 적용 이벤트이다.</param>
        private void OnDamageApplied(DamageAppliedEvent damageEvent)
        {
            if (damageEvent.Amount <= 0 || damageEvent.Target == null)
            {
                return;
            }

            var spawnPosition = damageEvent.Target.transform.position + Vector3.up * spawnHeight;
            var popup = _pool.Acquire();
            popup.Show(damageEvent.Amount, spawnPosition, ReleasePopup);
        }

        private void ReleasePopup(DamageNumberPopup popup)
        {
            _pool.Release(popup);
        }

        /// <summary>
        /// 새 팝업 GameObject를 만든다. 프리팹을 따로 두지 않고 필요한 컴포넌트를 코드로 붙인다.
        /// </summary>
        /// <remarks>
        /// 만들자마자 비활성화해 둔다. <see cref="ObjectPool{T}.Prewarm"/>은 대여 콜백을 부르지 않으므로,
        /// 여기서 꺼 두지 않으면 미리 만들어 둔 팝업이 빈 채로 화면에 활성 상태로 남는다.
        /// </remarks>
        private DamageNumberPopup CreatePopup()
        {
            var popupObject = new GameObject("DamageNumberPopup", typeof(TextMeshPro), typeof(DamageNumberPopup));
            popupObject.transform.SetParent(transform);

            var text = popupObject.GetComponent<TextMeshPro>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;

            popupObject.SetActive(false);
            return popupObject.GetComponent<DamageNumberPopup>();
        }

        private static void OnAcquire(DamageNumberPopup popup)
        {
            popup.gameObject.SetActive(true);
        }

        private static void OnRelease(DamageNumberPopup popup)
        {
            popup.gameObject.SetActive(false);
        }
    }
}
