using System.Collections.Generic;
using System.ComponentModel;
using HS.Framework.Foundation.MVVM;
using HS.Framework.Settings;
using UnityEngine.InputSystem;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 입력 설정 화면의 바인딩 목록과 적용 흐름을 제공한다.
    /// </summary>
    public sealed class InputSettingsViewModel : ChangeTrackingViewModelBase, IEditableViewModel
    {
        /// <summary>
        /// 연결된 입력 설정 서비스이다.
        /// </summary>
        private readonly InputSettingsService _settingsService;

        /// <summary>
        /// 입력 설정 ViewModel을 생성한다.
        /// </summary>
        /// <param name="settingsService">연결할 입력 설정 서비스이다.</param>
        public InputSettingsViewModel(InputSettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new System.ArgumentNullException(nameof(settingsService));
            Bindings = BuildBindingItems(_settingsService.InputActionAsset, _settingsService);

            foreach (var binding in Bindings)
            {
                binding.PropertyChanged += OnBindingPropertyChanged;
            }
        }

        /// <summary>
        /// 설정창에 표시할 입력 바인딩 목록을 가져온다.
        /// </summary>
        public IReadOnlyList<InputBindingItemViewModel> Bindings { get; }

        /// <summary>
        /// 등록된 입력 액션 에셋이 있는지 여부를 가져온다.
        /// </summary>
        public bool HasInputActionAsset => _settingsService.HasInputActionAsset;

        /// <summary>
        /// 편집 중인 바인딩 값이 있는지 여부를 가져온다.
        /// </summary>
        public override bool HasChanges
        {
            get
            {
                foreach (var binding in Bindings)
                {
                    if (binding.HasChanges)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 편집 중인 입력 바인딩 값을 입력 액션 에셋에 적용하고 저장한다.
        /// </summary>
        public void Apply()
        {
            Apply(true);
        }

        /// <summary>
        /// 편집 중인 입력 바인딩 값을 입력 액션 에셋에 적용한다.
        /// </summary>
        /// <param name="save">PlayerPrefs에 저장할지 여부이다.</param>
        public void Apply(bool save = true)
        {
            foreach (var binding in Bindings)
            {
                binding.Apply();
            }

            _settingsService.SaveBindingOverrides(save);
            OnHasChangesChanged();
        }

        /// <summary>
        /// 편집 중인 값을 마지막 적용 값으로 되돌린다.
        /// </summary>
        public void Cancel()
        {
            foreach (var binding in Bindings)
            {
                binding.Cancel();
            }

            OnHasChangesChanged();
        }

        /// <summary>
        /// 편집 중인 값을 기본 바인딩 경로로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            foreach (var binding in Bindings)
            {
                binding.ResetToDefaults();
            }

            OnHasChangesChanged();
        }

        /// <summary>
        /// 입력 액션 에셋에서 표시 가능한 바인딩 항목을 만든다.
        /// </summary>
        /// <param name="inputActionAsset">입력 액션 에셋이다.</param>
        /// <returns>입력 바인딩 항목 목록이다.</returns>
        private static IReadOnlyList<InputBindingItemViewModel> BuildBindingItems(
            InputActionAsset inputActionAsset,
            InputSettingsService inputSettingsService)
        {
            var items = new List<InputBindingItemViewModel>();

            if (inputActionAsset == null)
            {
                return items;
            }

            foreach (var actionMap in inputActionAsset.actionMaps)
            {
                foreach (var action in actionMap.actions)
                {
                    for (var i = 0; i < action.bindings.Count; i++)
                    {
                        if (action.bindings[i].isComposite)
                        {
                            continue;
                        }

                        items.Add(new InputBindingItemViewModel(action, i, actionMap.name, inputSettingsService));
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// 하위 바인딩 항목 변경 시 전체 변경 여부를 갱신한다.
        /// </summary>
        /// <param name="sender">변경된 바인딩 항목이다.</param>
        /// <param name="e">속성 변경 이벤트 인자이다.</param>
        private void OnBindingPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InputBindingItemViewModel.HasChanges) || e.PropertyName == nameof(InputBindingItemViewModel.PendingPath))
            {
                OnHasChangesChanged();
            }
        }

        /// <summary>
        /// 하위 바인딩 항목 구독을 해제한다.
        /// </summary>
        protected override void OnDispose()
        {
            foreach (var binding in Bindings)
            {
                binding.PropertyChanged -= OnBindingPropertyChanged;
                binding.CancelRebinding();
                binding.Dispose();
            }

            base.OnDispose();
        }
    }
}
