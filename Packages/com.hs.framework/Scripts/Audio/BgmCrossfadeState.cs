using UnityEngine;

namespace HS.Framework.Audio
{
    /// <summary>
    /// 두 재생 슬롯 사이의 배경음 크로스페이드 진행 상태를 계산하는 순수 로직이다.
    /// 각 슬롯의 가중치는 [0, 1] 범위이며 실제 소스 볼륨에 곱해 사용한다.
    /// </summary>
    public sealed class BgmCrossfadeState
    {
        /// <summary>크로스페이드에 사용하는 재생 슬롯 수이다.</summary>
        public const int SlotCount = 2;

        private readonly float[] _slotWeights = new float[SlotCount];
        private float _fadeDuration;
        private bool _isStopping = true;
        private int _activeSlot = SlotCount - 1;

        /// <summary>현재 활성 슬롯 인덱스이다.</summary>
        public int ActiveSlot => _activeSlot;

        /// <summary>정지를 향해 페이드 중인지 여부이다.</summary>
        public bool IsStopping => _isStopping;

        /// <summary>모든 슬롯이 무음 상태인지 여부이다.</summary>
        public bool IsSilent => _slotWeights[0] <= 0f && _slotWeights[1] <= 0f;

        /// <summary>지정한 슬롯의 현재 가중치를 가져온다.</summary>
        public float GetWeight(int slot)
        {
            return _slotWeights[slot];
        }

        /// <summary>
        /// 새 배경음 재생을 시작하며 활성 슬롯을 교대한다. 새 배경음을 실어야 하는 슬롯 인덱스를 반환한다.
        /// 이전 활성 슬롯은 현재 가중치에서 이어서 페이드아웃한다.
        /// </summary>
        /// <param name="fadeDuration">크로스페이드 시간(초)이다. 0 이하이면 즉시 전환한다.</param>
        public int BeginPlay(float fadeDuration)
        {
            _activeSlot = (_activeSlot + 1) % SlotCount;
            _isStopping = false;
            _fadeDuration = Mathf.Max(0f, fadeDuration);

            if (_fadeDuration <= 0f)
            {
                _slotWeights[_activeSlot] = 1f;
                _slotWeights[(_activeSlot + 1) % SlotCount] = 0f;
            }
            else
            {
                _slotWeights[_activeSlot] = 0f;
            }

            return _activeSlot;
        }

        /// <summary>
        /// 재생 중인 배경음의 정지를 시작한다. 모든 슬롯이 페이드아웃 대상이 된다.
        /// </summary>
        /// <param name="fadeDuration">페이드아웃 시간(초)이다. 0 이하이면 즉시 무음이 된다.</param>
        public void BeginStop(float fadeDuration)
        {
            _isStopping = true;
            _fadeDuration = Mathf.Max(0f, fadeDuration);

            if (_fadeDuration <= 0f)
            {
                _slotWeights[0] = 0f;
                _slotWeights[1] = 0f;
            }
        }

        /// <summary>경과 시간만큼 페이드 진행을 갱신한다.</summary>
        /// <param name="deltaTime">경과 시간(초)이다. 0 이하이면 무시한다.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            var step = _fadeDuration <= 0f ? 1f : deltaTime / _fadeDuration;
            var activeTarget = _isStopping ? 0f : 1f;
            var inactiveSlot = (_activeSlot + 1) % SlotCount;
            _slotWeights[_activeSlot] = Mathf.MoveTowards(_slotWeights[_activeSlot], activeTarget, step);
            _slotWeights[inactiveSlot] = Mathf.MoveTowards(_slotWeights[inactiveSlot], 0f, step);
        }
    }
}
