namespace HS.Framework.Audio
{
    /// <summary>
    /// 사운드 큐가 후보 클립 가운데 하나를 고르는 방식이다.
    /// </summary>
    public enum AudioCueClipSelectionMode
    {
        /// <summary>무작위로 고르되 직전 클립의 즉시 반복은 피한다.</summary>
        Random = 0,

        /// <summary>등록 순서대로 순환하며 고른다.</summary>
        Sequential = 1
    }
}
