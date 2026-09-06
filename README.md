# Tactics

유닛을 편성·배치하고 행동 트리와 어빌리티로 전투를 진행하는 Unity 게임의 소스 코드다. 게임 규칙을 구현하는 **Tactics**와 공통 런타임인 **HS Framework**를 함께 담았다.

이 저장소는 코드 열람과 설계 검토를 위한 사본이다. 씬·프리팹·모델·텍스처·애니메이션·오디오·설정 에셋 및 외부 라이브러리를 포함하지 않으므로, 복제한 상태 그대로 게임을 실행하거나 전체 테스트를 통과시킬 수는 없다.

## 코드 구성

| 경로 | 내용 |
| --- | --- |
| [Assets/_HS/Tactics/Scripts](Assets/_HS/Tactics/Scripts) | 편성·배치, 전투, 엄폐, 경로 탐색, 유닛 성장, 화면과 에디터 도구 |
| [Packages/com.hs.framework/Scripts](Packages/com.hs.framework/Scripts) | 어빌리티·효과·태그·어트리뷰트, 행동 트리, 부팅·씬 전환, 저장·설정·UI |
| [Tactics 테스트](Assets/_HS/Tactics/Scripts/Tests) | 게임 규칙의 EditMode 테스트와 씬 연동 PlayMode 테스트 |
| [Framework 테스트](Packages/com.hs.framework/Scripts/Tests) | 공통 런타임의 단위·통합 테스트; 각 기능 폴더에도 전용 테스트가 있다 |

게임 조립은 `TacticsLifetimeScope`에서 Framework 확장 지점을 사용한다. 전투의 이동·판정은 고정 스텝에서 진행하고, 화면 갱신에서는 고정 스텝 사이의 시각 상태를 보간한다. 원본 프로젝트의 논리 주기는 **30Hz**이며, 프로젝트를 새로 구성할 때는 `Fixed Timestep`을 `1 / 30`초로 지정해야 한다.

## 읽기 시작할 곳

- [TacticalUnit](Assets/_HS/Tactics/Scripts/Units/TacticalUnit.cs): 유닛 구성과 런타임 연결
- [PlanarCharacterMover](Assets/_HS/Tactics/Scripts/Character/Movement/PlanarCharacterMover.cs): 수평 이동과 경로 추종
- [CoverSelection](Assets/_HS/Tactics/Scripts/Cover/CoverSelection.cs): 엄폐 후보 선택 규칙
- [GameplayAbilitySystem](Packages/com.hs.framework/Scripts/Ability/Abilities/GameplayAbilitySystem.cs): 어빌리티 활성화·취소와 수명 관리
- [Framework 안내](Packages/com.hs.framework/README.md): 공통 런타임의 계약과 구성 제약

## 개발 환경과 실행 조건

기준 에디터는 **Unity 6000.3.15f1**이다. 필요한 외부 라이브러리와 버전은 [의존성 안내](docs/DEPENDENCIES.md)에 정리했다. 외부 패키지의 코드·DLL은 각 공급 경로에서 별도로 준비해야 한다.

게임을 실행하려면 프로젝트 설정, Bootstrap·Loading·MainMenu·전투 씬, 유닛·UI 프리팹, 스테이지·어빌리티 정의와 입력·그래픽·오디오 등의 에셋을 구성하고 코드의 참조에 연결해야 한다. 소스에 등장하는 에셋 경로와 GUID는 코드의 연결 계약이며 해당 파일이 이 저장소에 있다는 뜻은 아니다. 코드와 어셈블리 정의의 `.meta`는 Unity 식별자를 보존하기 위해 포함했다.

에셋을 읽는 EditMode 테스트, 씬 기반 PlayMode 테스트와 에디터 배선 메뉴는 이 사본만으로 검증할 수 없다. 순수 규칙 테스트도 먼저 Unity 어셈블리와 의존성을 구성해야 한다.

## 공개 범위

공개하는 소스는 `HS.Framework`와 `HS.Tactics` 두 영역의 런타임·에디터·테스트 코드다. 어셈블리 정의, 이에 대응하는 메타데이터와 폴더 메타데이터, 패키지 설명 및 문서를 함께 제공한다. 게임 에셋, 외부 공급자 코드·바이너리, 소비자 프로젝트 검증 코드, 로컬 검증·추출 스크립트, Unity 생성 파일과 개발 인계 노트는 포함하지 않는다.

## 라이선스

이 저장소의 HS.Framework·HS.Tactics 소스와 문서는 **MIT No Attribution (MIT-0)** 라이선스로 제공한다. 전문은 [LICENSE](LICENSE)에 있다. 외부 의존성에는 각 공급자의 라이선스가 적용된다.
