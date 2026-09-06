using HS.Framework.Character;

namespace HS.Tactics.Character
{
    /// <summary>이동 수단의 목적지 도착 판정에 쓰는 멈춤 거리를 제공한다.</summary>
    /// <remarks>
    /// Tactics의 엄폐 도착 판정은 이 값보다 좁은 기준을 쓰지 않아 이동기의 도착 판정과 일치시킨다.
    /// 멈춤 거리 개념이 없는 이동 수단도 있으므로 ICharacterMover와 별개의 선택적 계약으로 제공한다.
    /// </remarks>
    public interface IStoppingDistanceProvider
    {
        /// <summary>목적지에서 이 거리 안에 들면 도착으로 보는 거리(미터)이다.</summary>
        float StoppingDistance { get; }
    }
}
