namespace HS.Framework.Persistence
{
    /// <summary>
    /// 키 기반 문자열 저장소 백엔드 계약이다.
    /// 저장 시스템이 PlayerPrefs, 파일 등 구체 백엔드에 직접 의존하지 않도록 분리한다.
    /// </summary>
    public interface ISaveDataStorage
    {
        /// <summary>
        /// 지정한 키의 데이터가 저장소에 존재하는지 확인한다.
        /// </summary>
        /// <param name="key">확인할 저장소 키이다.</param>
        /// <returns>데이터가 존재하면 true를 반환한다.</returns>
        bool Exists(string key);

        /// <summary>
        /// 지정한 키의 문자열 데이터를 읽는다.
        /// </summary>
        /// <param name="key">읽을 저장소 키이다.</param>
        /// <param name="value">읽은 문자열이며, 데이터가 없으면 null이다.</param>
        /// <returns>데이터를 읽었으면 true를 반환한다.</returns>
        bool TryRead(string key, out string value);

        /// <summary>
        /// 지정한 키에 문자열 데이터를 기록하고 즉시 영속화한다.
        /// </summary>
        /// <param name="key">기록할 저장소 키이다.</param>
        /// <param name="value">기록할 문자열이다.</param>
        void Write(string key, string value);

        /// <summary>
        /// 지정한 키의 데이터를 삭제한다. 데이터가 없으면 아무 일도 하지 않는다.
        /// </summary>
        /// <param name="key">삭제할 저장소 키이다.</param>
        void Delete(string key);
    }
}
