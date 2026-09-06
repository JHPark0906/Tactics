using UnityEngine;

namespace HS.Framework.AI.BehaviourTree
{
    /// <summary>
    /// 월드에서 발생한 소리의 위치와 청취 반경을 전달한다.
    /// </summary>
    public readonly struct SoundStimulus
    {
        /// <summary>
        /// 소리가 발생한 월드 위치를 가져온다.
        /// </summary>
        public Vector3 Position { get; }

        /// <summary>
        /// 소리를 들을 수 있는 최대 반경을 가져온다.
        /// </summary>
        public float Range { get; }

        /// <summary>
        /// 소리 자극 데이터를 생성한다.
        /// </summary>
        public SoundStimulus(Vector3 position, float range)
        {
            Position = position;
            Range = range;
        }
    }
}