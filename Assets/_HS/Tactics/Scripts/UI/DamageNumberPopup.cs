using System;
using TMPro;
using UnityEngine;

namespace HS.Tactics.UI
{
    /// <summary>
    /// 맞은 자리 위로 피해량 숫자를 띄웠다가 떠오르며 사라지게 하는 컴포넌트이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>순수한 연출이다.</b> 게임 판정에 관여하지 않으므로 고정 스텝이 아니라 <c>Update</c>에서 움직이고
    /// 흐려진다 — 프레임률이 달라도 결과가 갈리지 않아야 하는 것은 판정이지 이 숫자가 아니다.
    /// </para>
    /// <para>
    /// <b>자기 수명을 스스로 잰다.</b> <see cref="Show"/>로 받은 콜백을 다 떠오른 순간 스스로 부른다.
    /// 풀에 반환하는 쪽(<see cref="DamageNumberSpawner"/>)이 활성 목록을 따로 들고 매 프레임 돌 필요가 없다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class DamageNumberPopup : MonoBehaviour
    {
        [Tooltip("위로 떠오르는 속도(초당 미터)이다.")]
        [SerializeField]
        [Min(0f)]
        private float riseSpeed = 1.2f;

        [Tooltip("떴다가 사라지기까지 걸리는 시간(초)이다.")]
        [SerializeField]
        [Min(0.01f)]
        private float lifetime = 0.8f;

        private TextMeshPro _text;
        private Color _baseColor;
        private Action<DamageNumberPopup> _release;
        private float _elapsed;

        private void Awake()
        {
            _text = GetComponent<TextMeshPro>();
            _baseColor = _text.color;
        }

        /// <summary>
        /// 지정한 자리에서 피해량을 보여 주기 시작한다.
        /// </summary>
        /// <param name="amount">보여 줄 피해량이다.</param>
        /// <param name="worldPosition">숫자를 띄울 월드 좌표이다.</param>
        /// <param name="release">다 떠오른 뒤 자신을 풀에 돌려줄 콜백이다.</param>
        public void Show(int amount, Vector3 worldPosition, Action<DamageNumberPopup> release)
        {
            transform.position = worldPosition;
            _text.text = amount.ToString();
            _text.color = _baseColor;
            _release = release;
            _elapsed = 0f;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);
            FaceCamera();

            var fadeStart = lifetime * 0.5f;
            if (_elapsed > fadeStart)
            {
                var fadeT = Mathf.Clamp01((_elapsed - fadeStart) / (lifetime - fadeStart));
                var color = _baseColor;
                color.a = Mathf.Lerp(_baseColor.a, 0f, fadeT);
                _text.color = color;
            }

            if (_elapsed >= lifetime)
            {
                _release?.Invoke(this);
            }
        }

        /// <summary>주 카메라 쪽을 향해 돌아 글자가 항상 읽히게 한다. 주 카메라가 없으면 그대로 둔다.</summary>
        private void FaceCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            transform.rotation = mainCamera.transform.rotation;
        }
    }
}
