using System.Runtime.CompilerServices;

// EditMode·PlayMode 검사가 경로 계획과 엄폐 선택의 internal 이음매를 검증할 수 있게 한다.
[assembly: InternalsVisibleTo("Tactics.Game.Tests.EditMode")]
[assembly: InternalsVisibleTo("Tactics.Game.Tests.PlayMode")]
