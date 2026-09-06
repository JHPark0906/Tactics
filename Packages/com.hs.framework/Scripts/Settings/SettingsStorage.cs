using HS.Framework.Persistence;
using UnityEngine;

namespace HS.Framework.Settings
{
    /// <summary>
    /// 설정 계층이 값을 읽고 쓸 때 사용하는 저장소 백엔드를 제공한다.
    /// 설정 서비스가 PlayerPrefs를 직접 호출하지 않고 이 진입점을 거치므로,
    /// 게임 프로젝트나 테스트가 파일·메모리·클라우드 백엔드로 대체할 수 있다.
    /// 기본 백엔드는 <see cref="PlayerPrefsSaveDataStorage"/>이며 기존 PlayerPrefs 키를 그대로 사용하므로,
    /// 대체하지 않으면 이전 버전이 저장한 설정이 계속 로드된다.
    /// </summary>
    public static class SettingsStorage
    {
        /// <summary>
        /// 현재 설정 저장에 사용 중인 저장소 백엔드이며, 지정되지 않았으면 null이다.
        /// </summary>
        private static ISaveDataStorage _current;

        /// <summary>
        /// 설정 저장에 사용할 저장소 백엔드를 가져오거나 설정한다.
        /// 지정하지 않았으면 PlayerPrefs 기반 기본 백엔드를 만들어 사용하며,
        /// null을 설정하면 다시 기본 백엔드로 되돌린다.
        /// 대체 백엔드는 설정 서비스를 초기화하기 전에 지정해야 초기 로드부터 반영된다.
        /// </summary>
        public static ISaveDataStorage Current
        {
            get => _current ??= new PlayerPrefsSaveDataStorage();
            set => _current = value;
        }

        /// <summary>
        /// 도메인 리로드를 끄고 플레이 모드에 진입해도 이전 세션의 정적 상태가 남지 않도록 초기화한다.
        /// 정적 상태를 보유한 프레임워크 클래스는 모두 이 규약(SubsystemRegistration 시점 리셋)을 따르므로,
        /// 새로 정적 필드를 추가하는 작성자는 이 메서드에도 해당 필드를 반드시 추가해야 한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _current = null;
        }

        /// <summary>
        /// 테스트에서 대체 백엔드가 다음 테스트로 새지 않도록 기본 백엔드로 되돌린다.
        /// 플레이 모드 진입 시 수행하는 리셋과 같은 동작이다.
        /// </summary>
        internal static void ResetForTests()
        {
            ResetStaticState();
        }
    }
}
