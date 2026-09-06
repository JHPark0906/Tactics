using System;
using HS.Framework.Foundation.Input;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 기본 상태와 범위별 차단 토큰 개수를 합성해 실효 입력 상태를 보고하는 테스트용 입력 컨트롤러이다.
    /// </summary>
    /// <remarks>
    /// 실제 구현과 같은 참조 카운트 규칙을 따르므로, 여러 차단 주체가 각자 토큰을 쥐었다 놓는 상황을
    /// 액션 에셋 없이 재현할 수 있다. 차단 주체가 어떤 범위를 골랐는지도 개수로 드러나므로
    /// "모달이 UI까지 막지 않는다" 같은 판정을 직접 검증할 수 있다.
    /// </remarks>
    internal sealed class FakeInputStateController : IInputStateController
    {
        private bool _isBaseInputEnabled;
        private int _gameplayBlockCount;
        private int _uiBlockCount;
        private int _liveTokenCount;

        /// <summary>지정한 기본 활성 상태로 가짜 컨트롤러를 생성한다.</summary>
        /// <param name="isInputEnabled">기본 입력 활성화 상태이다.</param>
        public FakeInputStateController(bool isInputEnabled)
        {
            _isBaseInputEnabled = isInputEnabled;
        }

        /// <summary>현재 살아 있는 차단 토큰의 개수이며 범위와 무관하게 센다.</summary>
        public int LiveBlockCount => _liveTokenCount;

        /// <summary>게임플레이 범위를 막고 있는 토큰의 개수이다.</summary>
        public int GameplayBlockCount => _gameplayBlockCount;

        /// <summary>UI 범위를 막고 있는 토큰의 개수이다.</summary>
        public int UiBlockCount => _uiBlockCount;

        /// <inheritdoc />
        public bool IsInputEnabled => IsScopeEnabled(InputBlockScope.Gameplay);

        /// <summary>UI 입력이 실제로 활성화되어 있는지 여부이다.</summary>
        public bool IsUiInputEnabled => IsScopeEnabled(InputBlockScope.Ui);

        /// <inheritdoc />
        public bool IsScopeEnabled(InputBlockScope scope)
        {
            if (!_isBaseInputEnabled)
            {
                return false;
            }

            if (scope.HasFlag(InputBlockScope.Gameplay) && _gameplayBlockCount > 0)
            {
                return false;
            }

            return !scope.HasFlag(InputBlockScope.Ui) || _uiBlockCount <= 0;
        }

        /// <inheritdoc />
        public void SetInputEnabled(bool isEnabled)
        {
            _isBaseInputEnabled = isEnabled;
        }

        /// <inheritdoc />
        public IDisposable AcquireInputBlock()
        {
            return AcquireInputBlock(InputBlockScope.Gameplay);
        }

        /// <inheritdoc />
        public IDisposable AcquireInputBlock(InputBlockScope scope)
        {
            if (scope.HasFlag(InputBlockScope.Gameplay))
            {
                _gameplayBlockCount++;
            }

            if (scope.HasFlag(InputBlockScope.Ui))
            {
                _uiBlockCount++;
            }

            _liveTokenCount++;
            return new BlockToken(this, scope);
        }

        /// <summary>해제 시 자신이 막고 있던 범위의 개수를 줄이는 테스트용 차단 토큰이다.</summary>
        private sealed class BlockToken : IDisposable
        {
            private readonly InputBlockScope _scope;
            private FakeInputStateController _owner;

            public BlockToken(FakeInputStateController owner, InputBlockScope scope)
            {
                _owner = owner;
                _scope = scope;
            }

            public void Dispose()
            {
                var owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                if (_scope.HasFlag(InputBlockScope.Gameplay))
                {
                    owner._gameplayBlockCount--;
                }

                if (_scope.HasFlag(InputBlockScope.Ui))
                {
                    owner._uiBlockCount--;
                }

                owner._liveTokenCount--;
            }
        }
    }
}
