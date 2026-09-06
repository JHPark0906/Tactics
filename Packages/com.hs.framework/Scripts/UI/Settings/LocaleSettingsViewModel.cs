using System;
using System.Collections.Generic;
using HS.Framework.Foundation.MVVM;
using HS.Framework.Settings;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 로케일 설정 화면의 편집 상태와 선택 가능한 로케일 옵션 목록을 제공한다.
    /// </summary>
    public sealed class LocaleSettingsViewModel : ChangeTrackingViewModelBase, IEditableViewModel
    {
        /// <summary>
        /// 연결된 로케일 설정 서비스이다.
        /// </summary>
        private readonly LocaleSettingsService _settingsService;

        /// <summary>
        /// 마지막으로 적용된 로케일 식별 코드이며, 실제로 적용된 로케일이 없으면 null이다.
        /// </summary>
        private string _appliedLocaleCode;

        /// <summary>
        /// 기본값으로 되돌릴 때 사용할 로케일 식별 코드이다.
        /// </summary>
        private readonly string _defaultLocaleCode;

        /// <summary>
        /// 선택된 로케일 옵션 인덱스이다.
        /// </summary>
        private int _selectedLocaleIndex;

        /// <summary>
        /// 로케일 설정 ViewModel을 생성한다.
        /// </summary>
        /// <param name="settingsService">연결할 로케일 설정 서비스이며, null이면 적용 시 서비스 호출을 건너뛴다.</param>
        /// <param name="availableLocales">외부에서 제공할 로케일 목록이며, 없으면 서비스에서 가져온다.</param>
        /// <param name="selectedLocaleCode">초기 선택 로케일 식별 코드이며, 없으면 서비스의 현재 선택 로케일을 사용한다.</param>
        public LocaleSettingsViewModel(
            LocaleSettingsService settingsService,
            IEnumerable<LocaleInfo> availableLocales = null,
            string selectedLocaleCode = null)
        {
            _settingsService = settingsService;
            Locales = BuildLocaleOptions(availableLocales, _settingsService);

            var initialIndex = TryFindLocaleIndex(ResolveInitialLocaleCode(selectedLocaleCode, _settingsService));
            _selectedLocaleIndex = Math.Max(0, initialIndex);
            _appliedLocaleCode = initialIndex >= 0 ? Locales[initialIndex].Value : null;
            _defaultLocaleCode = _appliedLocaleCode;
        }

        /// <summary>
        /// 선택 가능한 로케일 옵션 목록을 가져오며,
        /// 사용자 변경이 허용되지 않거나 Localization이 구성되지 않았으면 빈 목록이다.
        /// </summary>
        public IReadOnlyList<SettingsOptionViewModel<string>> Locales { get; }

        /// <summary>
        /// 선택된 로케일 옵션 인덱스를 가져오거나 설정한다.
        /// </summary>
        public int SelectedLocaleIndex
        {
            get => _selectedLocaleIndex;
            set => SetTrackedValue(ref _selectedLocaleIndex, SettingsOptions.ClampSelectedIndex(value, Locales.Count));
        }

        /// <summary>
        /// 적용된 로케일과 편집 중인 로케일이 다른지 여부를 가져온다.
        /// 실제로 적용된 로케일이 없으면(초기 로케일 미확인 등) 현재 편집 중인 선택도 적용 대기 변경으로 본다.
        /// </summary>
        public override bool HasChanges =>
            Locales.Count > 0 && !string.Equals(GetPendingLocaleCode(), _appliedLocaleCode, StringComparison.Ordinal);

        /// <summary>
        /// 현재 편집 중인 로케일을 적용하고 저장한다.
        /// </summary>
        public void Apply()
        {
            Apply(true);
        }

        /// <summary>
        /// 현재 편집 중인 로케일을 적용한다.
        /// 서비스가 적용을 거부하면(사용자 변경 불가, 미등록 코드 등) 적용 기록을 갱신하지 않아
        /// 변경 대기 상태가 유지되고 재시도할 수 있다. 서비스가 없으면 편집 상태만 커밋한다.
        /// </summary>
        /// <param name="save">변경한 로케일을 저장할지 여부이다.</param>
        public void Apply(bool save)
        {
            if (Locales.Count == 0)
            {
                return;
            }

            var pendingLocaleCode = GetPendingLocaleCode();
            if (_settingsService != null && !_settingsService.SetSelectedLocaleCode(pendingLocaleCode, save))
            {
                return;
            }

            _appliedLocaleCode = pendingLocaleCode;
            NotifyAllChanged();
        }

        /// <summary>
        /// 편집 중인 로케일을 마지막 적용 값으로 되돌린다.
        /// </summary>
        public void Cancel()
        {
            _selectedLocaleIndex = FindLocaleIndex(_appliedLocaleCode);
            NotifyAllChanged();
        }

        /// <summary>
        /// 편집 중인 로케일을 ViewModel 생성 시점의 기본값으로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            _selectedLocaleIndex = FindLocaleIndex(_defaultLocaleCode);
            NotifyAllChanged();
        }

        /// <summary>
        /// 현재 편집 중인 로케일 식별 코드를 가져오며, 옵션이 없으면 null을 반환한다.
        /// </summary>
        /// <returns>편집 중인 로케일 식별 코드이다.</returns>
        private string GetPendingLocaleCode()
        {
            return Locales.Count > 0 ? Locales[_selectedLocaleIndex].Value : null;
        }

        /// <summary>
        /// 모든 표시 속성 변경을 알린다.
        /// </summary>
        private void NotifyAllChanged()
        {
            OnPropertiesChanged(nameof(SelectedLocaleIndex), nameof(HasChanges));
        }

        /// <summary>
        /// 로케일 옵션 목록을 만든다.
        /// </summary>
        /// <param name="availableLocales">외부에서 제공한 로케일 목록이다.</param>
        /// <param name="settingsService">로케일 목록을 가져올 설정 서비스이다.</param>
        /// <returns>로케일 옵션 목록이다.</returns>
        private static IReadOnlyList<SettingsOptionViewModel<string>> BuildLocaleOptions(
            IEnumerable<LocaleInfo> availableLocales,
            LocaleSettingsService settingsService)
        {
            var locales = availableLocales ?? settingsService?.GetAvailableLocales();
            var options = new List<SettingsOptionViewModel<string>>();
            if (locales == null)
            {
                return options;
            }

            foreach (var locale in locales)
            {
                if (!string.IsNullOrWhiteSpace(locale.Code))
                {
                    options.Add(new SettingsOptionViewModel<string>(locale.Code, locale.DisplayName, locale.Code));
                }
            }

            return options;
        }

        /// <summary>
        /// 초기 선택 로케일 식별 코드를 결정한다.
        /// </summary>
        /// <param name="selectedLocaleCode">외부에서 제공한 초기 선택 로케일 식별 코드이다.</param>
        /// <param name="settingsService">현재 선택 로케일을 가져올 설정 서비스이다.</param>
        /// <returns>초기 선택 로케일 식별 코드이다.</returns>
        private static string ResolveInitialLocaleCode(string selectedLocaleCode, LocaleSettingsService settingsService)
        {
            return !string.IsNullOrWhiteSpace(selectedLocaleCode) ? selectedLocaleCode : settingsService?.SelectedLocaleCode;
        }

        /// <summary>
        /// 옵션 목록에서 로케일 식별 코드와 일치하는 인덱스를 찾는다.
        /// </summary>
        /// <param name="localeCode">찾을 로케일 식별 코드이다.</param>
        /// <returns>찾은 인덱스이며, 없으면 0을 반환한다.</returns>
        private int FindLocaleIndex(string localeCode)
        {
            return SettingsOptions.IndexOfValueOrDefault(Locales, localeCode, StringComparer.Ordinal);
        }

        /// <summary>
        /// 옵션 목록에서 로케일 식별 코드와 일치하는 인덱스를 찾는다.
        /// </summary>
        /// <param name="localeCode">찾을 로케일 식별 코드이다.</param>
        /// <returns>찾은 인덱스이며, 없으면 -1을 반환한다.</returns>
        private int TryFindLocaleIndex(string localeCode)
        {
            return SettingsOptions.IndexOfValue(Locales, localeCode, StringComparer.Ordinal);
        }
    }
}
