# Framework

Unity 6000.3용 공통 런타임 패키지입니다. 씬 전환, 설정, 저장, UI, 어빌리티, 캐릭터와 AI를 제공합니다. 게임 코드는 `FrameworkLifetimeScope.ConfigureProjectServices`에서 서비스를 추가하고 필요한 Framework 어셈블리를 참조합니다.

개발 과정에서 생성형 AI의 도움을 받은 프로젝트입니다.

이 공개 패키지는 **코드 전용**입니다. 패키지 내부의 `Scripts`(Tests·Editor 포함)와 코드 메타데이터, 패키지 선언을 포함하며 공통 씬·프리팹·설정·입력 에셋과 외부 라이브러리 DLL·소스는 포함하지 않습니다. 패키지를 가져오는 것만으로 샘플 게임이나 기본 부팅 씬이 만들어지지는 않습니다.

## 설치와 통합

1. Unity 6000.3 프로젝트를 준비합니다. 원본이 사용하는 에디터는 6000.3.15f1입니다.
2. [의존성 문서](../../docs/DEPENDENCIES.md)의 외부 패키지와 DLL을 준비합니다. 특히 UniTask, R3 Unity, R3 NuGet DLL, MessagePipe 및 MessagePipe.VContainer는 별도 설치가 필요합니다. VContainer의 Git 공급 경로도 소비 프로젝트에 지정합니다.
3. 이 폴더를 소비 프로젝트의 `Packages/com.hs.framework`에 embedded 패키지로 넣거나, 외부 폴더를 소비 프로젝트의 `Packages/manifest.json`에서 `file:` 의존성으로 등록합니다. `.meta` 파일을 함께 보존합니다.
4. 사용할 런타임 기능에 필요한 씬·설정·입력·UI 에셋을 소비 프로젝트에서 구성합니다. Tactics 코드까지 통합하는 경우 게임 정의와 프리팹도 별도로 필요합니다.

| 공급 경로 | 필요한 구성 |
| --- | --- |
| [package.json](package.json)의 선언 | Cinemachine 3.1.7, Input System 1.19.0, Localization 1.5.12, Test Framework 1.6.0, uGUI 2.0.0, Unity 모듈, VContainer 1.19.0 |
| 소비 프로젝트의 별도 설치 | UniTask 2.5.11, R3 Unity 1.3.1, R3 1.3.1과 의존 DLL, MessagePipe 및 MessagePipe.VContainer 각각 1.8.2 |

R3 Unity 패키지는 R3 본체 DLL을 제공하지 않습니다. 여러 asmdef가 `R3.dll`을 명시적으로 참조하며, 서비스 계층은 `MessagePipe`와 `MessagePipe.VContainer` 어셈블리를 참조합니다. 패키지 경계 검사의 허용 목록은 이 외부 참조를 허용하는 규칙일 뿐 의존성을 설치하지 않습니다. Git 커밋과 NuGet 복원 구성은 [의존성 문서](../../docs/DEPENDENCIES.md)에 있습니다.

## 부팅과 프로젝트 설정

`FrameworkLifetimeScope`는 `FrameworkProjectConfiguration.LoadRequired()`로 프로젝트 설정을 읽고 서비스와 `FrameworkInitializer`를 등록합니다. 프로젝트 설정은 Resources에 `FrameworkProjectConfiguration` 이름으로 준비해야 하며, 편집기 도구가 사용하는 경로는 `Assets/_HS/ProjectSettings/Resources/FrameworkProjectConfiguration.asset`입니다.

런타임 구성에는 Bootstrap·Loading·MainMenu·Gameplay 역할의 씬 참조, Gameplay 레벨 ID, 기본 시작 레벨과 초기 클라이언트 설정이 필요합니다. 씬을 Build Settings에 등록하고 Bootstrap을 인덱스 0으로 둡니다. 게임 서비스를 추가할 때는 Bootstrap 씬에 배치할 scope를 `FrameworkLifetimeScope`의 파생 타입으로 구성합니다. 초기 그래픽·오디오·입력 설정 에셋은 별도로 준비하며 로케일 프리셋은 선택 사항입니다.

**이 코드 전용 사본에서는 `Setup Project Scenes` 메뉴와 Framework 씬 템플릿만으로 설정을 완료할 수 없습니다.** 해당 도구는 `Scenes/Bootstrap.unity`, `Scenes/Loading.unity`, `Settings/DefaultClientSettingsConfiguration.asset` 등 패키지의 공통 에셋을 읽거나 복사하지만 이 사본에는 그 파일들이 없습니다. 소비 프로젝트에서 필요한 에셋과 참조를 직접 준비하거나, 해당 공통 에셋을 별도로 제공받아야 합니다.

플레이어가 바꾼 값은 프리셋과 별도로 `SettingsStorage.Current`에 저장되며 기본 백엔드는 PlayerPrefs입니다. 다른 백엔드를 사용할 때는 설정 서비스 초기화 전에 지정합니다. 게임 진행 저장은 프로젝트가 `ISaveable` 참여자와 `SaveOrchestrator`를 등록해 구성합니다.

## 런타임 계약

- `StartNewGameAsync`와 Gameplay 이동은 Loading 씬을 경유합니다. `ReturnToMainMenuAsync`는 MainMenu를 직접 로드합니다. 목적지가 활성화되기 전까지 기존 결과 화면을 유지하여 전환 실패 시 재시도할 수 있게 합니다. 기본 DI 구성은 다른 목적지의 로드 실패를 MainMenu로 복구하도록 연결합니다.
- `UiWindowBase.OnClosed`는 창 비활성화와 `Closed` 알림을 마친 뒤 호출됩니다. 이 훅의 요청자 콜백에서 새 창을 열어도 이전 닫힘 처리가 새 창을 숨기지 않습니다.
- `UiWindowManager.CloseAllWindows`는 호출 시점의 표시 요청만 닫습니다. 콜백에서 다시 열거나 교체한 요청은 유지합니다. 이미 열린 창의 요청자를 바꾸는 사용자 정의 `Show`는 `BeginRequest()`를 호출해야 합니다. `Unregister`는 등록을 해제한 뒤 닫으므로, 닫힘 콜백에서 다시 `Register` 또는 `OpenWindow`한 창은 새 등록으로 관리됩니다.
- `GameplayEffectRunner.TryApply`는 `Rejected`, `Executed`, `Applied`, `Cancelled`를 구분합니다. `Apply`는 즉시 효과 성공과 적용 거부 모두 `null`을 반환하므로 성공 판정에는 `TryApply`를 사용합니다. `Dispose`가 시작되면 새 효과는 거부됩니다. `RemoveAll`은 호출 당시의 효과만 제거하므로 제거 콜백이 새로 적용한 효과는 유지됩니다.
- 어빌리티는 비용·쿨다운 효과의 적용 조건과 실제 적용 결과를 확인합니다. 실패하면 본 동작을 시작하지 않고 `CostApplicationFailed` 또는 `CooldownApplicationFailed`를 반환합니다. 준비 중 취소되면 `Cancelled`를 반환합니다. 이미 실행된 즉시 비용 변화는 되돌리지 않으므로, 콜백에서 상태를 바꾸는 소비자는 이 계약을 고려해야 합니다. 효과가 부여한 어빌리티의 정리는 실제 인스턴스를 기준으로 하며 같은 태그로 교체된 어빌리티를 제거하지 않습니다.
- `FileSaveDataStorage`는 저장 루트의 `v2/key-*.json`에 씁니다. 대문자까지 인코딩하므로 Windows에서도 `Player`와 `player`가 서로 다른 키입니다. 호환성을 위해 루트의 JSON도 **실제 파일명 대소문자가 요청 키와 정확히 일치할 때** 읽습니다. `v2` 파일을 우선해서 읽으며 루트의 파일은 보존합니다. `Delete(key)`는 그 키의 두 형식을 함께 삭제합니다. 덮어쓴 데이터의 복구는 지원하지 않습니다. PlayerPrefs는 별도 저장 형식을 사용합니다.

## 테스트

테스트 소스는 포함되어 있으나 전체 테스트를 바로 실행할 수 있는 에셋 구성은 포함되어 있지 않습니다. 외부 의존성을 설치하고 Unity Test Runner를 사용합니다. 소비 프로젝트의 `Packages/manifest.json`에 `"testables": ["com.hs.framework"]`를 추가하면 패키지 테스트를 노출할 수 있습니다. 테스트 어셈블리는 `UNITY_INCLUDE_TESTS` 조건을 사용합니다.

자료구조·어빌리티·효과 등 런타임 대역을 조립하는 테스트는 필요한 의존성을 준비한 뒤 선택해 실행할 수 있습니다. 프로젝트 설정, Build Settings, 씬 전환과 패키지 기본 에셋을 읽는 테스트는 해당 씬·설정·입력 에셋을 별도로 구성해야 합니다. Tactics의 실제 씬·프리팹·정의 에셋 검사도 같은 제약을 가집니다.

원본 저장소 루트의 `Scripts` 폴더와 프로젝트 검증·빌드 자동화는 공개 사본에 포함되지 않습니다. 포함된 테스트는 소비 프로젝트의 Unity Test Runner에서 실행하며, 검증 결과는 그 프로젝트에서 실제로 구성하고 실행한 범위에 한해 판단합니다.

## 라이선스

이 코드 전용 패키지의 소스와 문서는 **MIT No Attribution (MIT-0)** 라이선스로 제공합니다. 전문은 [LICENSE](LICENSE)에 있습니다. 외부 의존성에는 각 공급자의 라이선스가 적용됩니다.
