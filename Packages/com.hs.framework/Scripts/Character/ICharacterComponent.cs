namespace HS.Framework.Character
{
    /// <summary>
    /// 캐릭터에 부착되어 생명주기와 상태를 함께 구성하는 요소를 정의한다.
    /// </summary>
    public interface ICharacterComponent
    {
        /// <summary>
        /// 소유 캐릭터가 준비된 뒤 의존성을 연결한다.
        /// </summary>
        void Initialize(CharacterBase characterBase);
    }
}
