using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace HS.Framework.Settings
{
    /// <summary>
    /// 선택한 로케일을 저장하고 Unity Localization에 적용한다.
    /// </summary>
    public sealed class LocaleSettingsService
    {
        /// <summary>
        /// 선택한 로케일 코드를 저장할 때 사용하는 저장소 키이다.
        /// 기본 백엔드에서는 같은 이름의 PlayerPrefs 키로 기록된다.
        /// </summary>
        private const string StorageKey = "LocaleSettings";

        /// <summary>
        /// 현재 애플리케이션에서 사용 중인 로케일 설정 인스턴스이다.
        /// </summary>
        private static LocaleSettingsService _current;

        private LocaleSettingsPreset _preset;

        /// <summary>
        /// 현재 로케일 설정을 가져오며, 아직 초기화되지 않았으면 프리셋 없는 인스턴스를 생성해 캐시한다.
        /// </summary>
        /// <remarks>
        /// 이 정적 접근자는 주입 경로가 아직 없는 지점(FrameworkInitializer의 설정 초기화, 에디터 도구, EditMode 테스트)에서만 쓴다.
        /// 씬에 배치되는 런타임 컴포넌트는 VContainer로 주입받거나 직접 초기화할 수 있다.
        /// </remarks>
        public static LocaleSettingsService Current => EnsureCurrent();

        /// <summary>
        /// 캐시된 로케일 설정 인스턴스를 가져오며, 없으면 만들어 캐시한다.
        /// 모든 정적 진입점이 이 인스턴스를 재사용해야 초기화가 인스턴스를 교체하지 않는다.
        /// </summary>
        /// <returns>캐시된 로케일 설정 인스턴스를 반환한다.</returns>
        private static LocaleSettingsService EnsureCurrent()
        {
            return _current ??= new LocaleSettingsService();
        }

        /// <summary>
        /// 현재 선택된 로케일을 가져오며, Localization이 구성되지 않았으면 null을 반환한다.
        /// </summary>
        public Locale SelectedLocale => LocalizationSettings.HasSettings ? LocalizationSettings.SelectedLocale : null;

        /// <summary>
        /// 현재 선택된 로케일의 식별 코드를 가져오며, 선택된 로케일이 없으면 null을 반환한다.
        /// </summary>
        public string SelectedLocaleCode
        {
            get
            {
                var locale = SelectedLocale;
                return locale != null ? locale.Identifier.Code : null;
            }
        }

        /// <summary>
        /// 사용자가 로케일을 변경할 수 있는지 여부를 가져온다.
        /// </summary>
        public bool IsUserConfigurable => _preset != null && _preset.IsUserConfigurable;

        /// <summary>
        /// 로케일 설정을 초기화하고 저장된 로케일 또는 프리셋 기본 로케일을 적용한다.
        /// 프리셋이 없거나 Unity Localization이 구성되지 않았으면 로케일 적용만 건너뛴다.
        /// 새 인스턴스로 교체하는 대신 캐시된 인스턴스에 프리셋을 제자리 주입하므로,
        /// 초기화 전에 <see cref="Current"/>를 캡처한 소비자도 초기화 결과를 그대로 반영받는다.
        /// </summary>
        /// <param name="preset">기본 로케일과 사용자 변경 허용 여부를 정의한 프리셋이며, null이면 로케일을 적용하지 않는다.</param>
        /// <returns>초기화된 로케일 설정 인스턴스를 반환하며, <see cref="Current"/>와 항상 같은 인스턴스다.</returns>
        public static async UniTask<LocaleSettingsService> InitializeAsync(LocaleSettingsPreset preset)
        {
            var settings = EnsureCurrent();
            settings._preset = preset;

            if (preset == null || !LocalizationSettings.HasSettings)
            {
                Debug.Log("로케일 프리셋이 없거나 Unity Localization이 구성되지 않아 로케일 초기화를 건너뜁니다.");
                return settings;
            }

            await UniTask.WaitUntil(() => LocalizationSettings.InitializationOperation.IsDone);
            var locale = LoadSavedLocale() ?? preset.DefaultLocale ?? LocalizationSettings.SelectedLocale;
            if (locale != null)
            {
                LocalizationSettings.SelectedLocale = locale;
            }

            return settings;
        }

        /// <summary>
        /// 선택 로케일을 변경하고 필요에 따라 저장한다.
        /// Localization이 구성되지 않았거나 사용자 변경이 허용되지 않으면 아무것도 바꾸지 않고 false를 반환한다.
        /// </summary>
        /// <param name="locale">적용할 로케일이다.</param>
        /// <param name="save">변경한 로케일을 저장할지 여부이다.</param>
        /// <returns>로케일이 실제로 적용됐으면 true를 반환한다.</returns>
        public bool SetSelectedLocale(Locale locale, bool save = true)
        {
            if (locale == null || !IsUserConfigurable || !LocalizationSettings.HasSettings)
            {
                return false;
            }

            LocalizationSettings.SelectedLocale = locale;
            if (save)
            {
                SettingsStorage.Current.Write(StorageKey, locale.Identifier.Code);
            }

            return true;
        }

        /// <summary>
        /// 식별 코드로 선택 로케일을 변경하고 필요에 따라 저장한다.
        /// Localization이 구성되지 않았거나 사용자 변경이 허용되지 않거나 코드에 해당하는 로케일이 없으면
        /// 아무것도 바꾸지 않고 false를 반환한다.
        /// Locale 오버로드와 이름을 분리해 Unity.Localization을 참조하지 않는 어셈블리에서도 호출할 수 있다.
        /// </summary>
        /// <param name="localeCode">적용할 로케일의 식별 코드이다.</param>
        /// <param name="save">변경한 로케일을 저장할지 여부이다.</param>
        /// <returns>로케일이 실제로 적용됐으면 true를 반환한다.</returns>
        public bool SetSelectedLocaleCode(string localeCode, bool save = true)
        {
            if (string.IsNullOrWhiteSpace(localeCode) || !IsUserConfigurable || !LocalizationSettings.HasSettings)
            {
                return false;
            }

            return SetSelectedLocale(LocalizationSettings.AvailableLocales?.GetLocale(localeCode), save);
        }

        /// <summary>
        /// 사용 가능한 로케일 목록을 식별 코드와 표시 이름으로 가져온다.
        /// 사용자 변경이 허용되지 않거나 Localization이 구성되지 않았으면 빈 목록을 반환해,
        /// UI에 표시되는 선택지는 항상 <see cref="SetSelectedLocaleCode"/>로 적용 가능함을 보장한다.
        /// </summary>
        /// <returns>사용 가능한 로케일 정보 목록이다.</returns>
        public IReadOnlyList<LocaleInfo> GetAvailableLocales()
        {
            if (!IsUserConfigurable || !LocalizationSettings.HasSettings)
            {
                return Array.Empty<LocaleInfo>();
            }

            var locales = LocalizationSettings.AvailableLocales?.Locales;
            if (locales == null)
            {
                return Array.Empty<LocaleInfo>();
            }

            var results = new List<LocaleInfo>(locales.Count);
            foreach (var locale in locales)
            {
                if (locale != null)
                {
                    results.Add(new LocaleInfo(locale.Identifier.Code, locale.LocaleName));
                }
            }

            return results;
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
        /// 테스트에서 정적 캐시 상태가 누출되지 않도록 캐시를 초기 상태로 되돌린다.
        /// 플레이 모드 진입 시 수행하는 리셋과 같은 동작이다.
        /// </summary>
        internal static void ResetCurrentForTests()
        {
            ResetStaticState();
        }

        private static Locale LoadSavedLocale()
        {
            if (!SettingsStorage.Current.TryRead(StorageKey, out var savedLocaleCode))
            {
                return null;
            }

            return LocalizationSettings.AvailableLocales.GetLocale(savedLocaleCode);
        }
    }
}
