# 의존성과 실행 조건

이 공개 사본은 Tactics와 HS Framework의 소스, 어셈블리 정의, 코드 메타데이터를 제공하며 두 코드 트리의 Tests·Editor 코드를 포함한다. 게임 및 Framework의 씬·프리팹·설정·입력 에셋과 외부 라이브러리의 DLL·소스는 포함하지 않는다. 코드를 Unity 프로젝트에 통합하려면 아래 의존성과 사용할 기능의 에셋 구성을 별도로 준비해야 한다.

## 관측된 버전

버전은 원본 프로젝트의 `Packages/manifest.json`, `Packages/packages-lock.json`, Framework의 `package.json`, 복원된 UPM 패키지의 `package.json`, `Assets/packages.config` 및 외부 패키지 메타데이터에서 확인한 값이다. 모든 항목이 Framework의 `package.json`만으로 설치되지는 않는다.

| 구성 | 버전 | 공급 경로와 용도 |
| --- | --- | --- |
| Unity Editor | 6000.3.15f1 | 원본 프로젝트의 에디터 버전. Framework의 최소 Unity 선언은 6000.3이다. |
| HS Framework | 0.1.0 | 이 사본의 `Packages/com.hs.framework`. 공통 에셋을 제외한 소스 패키지다. |
| VContainer | 1.19.0 | Git UPM, `jp.hadashikick.vcontainer`. 의존성 주입과 LifetimeScope. |
| UniTask | 2.5.11 | Git UPM, `com.cysharp.unitask`. `UniTask`와 MessagePipe가 사용하는 `UniTask.Linq` 어셈블리. |
| R3 Unity | 1.3.1 | Git UPM, `com.cysharp.r3`. Unity 통합이며 R3 본체 DLL은 별도로 필요하다. |
| R3 | 1.3.1 | NuGet DLL. 여러 asmdef가 `R3.dll`을 명시적으로 참조한다. |
| MessagePipe / MessagePipe.VContainer | 각각 1.8.2 | 별도 설치. 원본은 `Assets/Plugins` 아래에 두 모듈의 소스를 둔다. UPM 매니페스트에 포함되어 있지 않다. |
| Cinemachine | 3.1.7 | `com.unity.cinemachine`. 카메라 구성과 관련 테스트. |
| Input System | 1.19.0 | `com.unity.inputsystem`. 입력 처리와 UI 입력. |
| Localization | 1.5.12 | `com.unity.localization`. 로케일 설정. ResourceManager도 참조한다. |
| uGUI / TextMeshPro | 2.0.0 | `com.unity.ugui`가 제공하는 `UnityEngine.UI` 및 `Unity.TextMeshPro`. |
| Unity Test Framework | 1.6.0 | `com.unity.test-framework`. Unity Test Runner와 NUnit 기반 테스트. |
| NuGetForUnity | 4.5.0 | `com.github-glitchenzo.nugetforunity`. 원본의 NuGet 복원 도구이며 게임 런타임 의존성은 아니다. |

R3의 원본 NuGet 복원 구성은 다음과 같다. Unity에서 사용할 DLL과 해당 의존 DLL을 함께 준비해야 한다.

| NuGet 패키지 | 버전 |
| --- | --- |
| R3 | 1.3.1 |
| Microsoft.Bcl.AsyncInterfaces | 6.0.0 |
| Microsoft.Bcl.TimeProvider | 8.0.0 |
| System.ComponentModel.Annotations | 5.0.0 |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 |
| System.Threading.Channels | 8.0.0 |

원본 lock의 주요 간접 의존성은 Addressables 2.9.1, Splines 2.8.4, Unity Newtonsoft Json 3.2.2, Mathematics 1.3.3, NUnit 확장 패키지 2.0.5다. 이 값은 전체 원본 프로젝트가 해결한 버전이다. 의존성을 줄인 소비 프로젝트가 같은 간접 버전을 자동으로 선택한다고 가정하지 않는다.

Framework는 엔진의 AI·오디오·JSON 직렬화·물리·UI·UIElements 모듈도 선언한다. 특히 Framework의 일반 경로 조건은 `UnityEngine.AI.NavMesh` API를 사용한다. Tactics의 평면 이동 코드가 NavMeshSurface를 사용하지 않는다는 이유로 엔진 AI 모듈까지 제거해서는 안 된다.

## Git UPM 참조

아래 객체는 소비 프로젝트의 `Packages/manifest.json`에 있는 `dependencies`에 합칠 수 있는 외부 Git 의존성이다. 원본 lock에서 확인한 커밋을 명시했다. NuGetForUnity는 위 NuGet DLL을 그 도구로 복원할 때 사용한다.

```json
{
  "com.cysharp.r3": "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#fdfb36e3d5af90ec06403714492ee85b2d8bf7cd",
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#ceac8d6946b1125fe782cd171fbcb245b567dbf9",
  "com.github-glitchenzo.nugetforunity": "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity#acc1c7bc9ea34c33b830e40316fca52553878d29",
  "jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#5401e5a7ebc4980a2b82141ffc26391a6547edd7"
}
```

MessagePipe와 MessagePipe.VContainer는 위 객체에 없다. 두 모듈의 1.8.2 배포본을 별도로 설치하고 `MessagePipe`, `MessagePipe.VContainer` 어셈블리가 생성되는지 확인한다. R3 Unity 설치도 R3 NuGet DLL 복원을 대신하지 않는다.

원본 매니페스트에는 URP, Toon Shader, AI 도구, IDE 연동 및 로컬 embedded SpringBone 등 전체 프로젝트용 패키지도 있다. 특히 `file:com.unity.springbone`이 가리키는 외부 패키지는 이 공개 사본에 없다. 원본 매니페스트 전체를 코드 전용 사본의 설치 목록으로 사용하지 말고, 소비 프로젝트가 필요한 패키지와 에셋을 구성해야 한다.

## 어셈블리와 메타데이터

직접 작성한 asmdef는 Tactics 4개와 Framework 23개이며, 모두 어셈블리 이름으로 참조한다. 이 범위에는 `GUID:` asmdef 참조나 asmref가 없다. 외부 어셈블리 이름과 `R3.dll` 같은 명시적 DLL 이름을 제공해야 하며, GUID를 새로 쓰는 것으로 누락된 의존성이 해결되지는 않는다.

`.cs.meta`, `.asmdef.meta`와 포함된 코드 폴더의 `.meta`는 함께 보존한다. 현재 asmdef 연결에는 이름을 쓰지만, Unity의 스크립트 식별과 이후 씬·프리팹·설정의 스크립트 참조는 `.cs.meta`의 GUID를 사용한다. 메타데이터를 다시 생성하면 원본 GUID를 사용하는 에셋을 가져올 때 연결이 달라질 수 있다. 제외한 에셋의 `.meta`만 따로 가져오지는 않는다.

## 실행과 테스트의 범위

- 이 사본은 완성된 실행 프로젝트나 플레이 가능한 샘플이 아니다. Bootstrap·Loading·MainMenu·Gameplay 씬, Build Settings, Framework 프로젝트 설정과 클라이언트 설정, 입력 액션, Tactics의 유닛·스테이지·어빌리티·행동 트리 정의 및 프리팹을 구성해야 게임을 실행할 수 있다.
- Framework의 `Tools > HS Framework > Setup Project Scenes`와 씬 템플릿 메뉴는 제외된 공통 씬과 기본 설정 에셋을 읽는다. 소스만 가져온 상태에서 실행해 완성된 부팅 구성을 만들 수 없다. [Framework README](../Packages/com.hs.framework/README.md)의 공개 범위 설명을 참고한다.
- Tactics의 Editor 메뉴는 특정 씬·정의·프리팹 경로와 에셋 GUID를 사용한다. 일부 연결을 보조하는 도구이며, 제외한 게임·외형·입력 에셋 전체를 재생성하는 도구는 아니다.
- 테스트에는 에셋 없이 대역을 조립하는 검사와 원본 에셋을 직접 읽는 검사가 함께 있다. `TacticsActualSceneFlowTests`는 실제 Bootstrap·MainMenu·Level1 씬과 Rifleman 정의를 읽는다. 정의·프리팹·씬 배선·행동 트리 에셋 검사 및 Framework의 패키지 에셋·프로젝트 설정·씬 전환 검사도 별도 에셋과 씬 구성을 요구한다.
- 에셋을 읽지 않는 테스트도 Unity와 외부 어셈블리가 필요하다. 테스트 asmdef는 `UNITY_INCLUDE_TESTS` 조건을 사용하며, Framework 패키지 테스트를 노출하려면 소비 프로젝트의 매니페스트에 `"testables": ["com.hs.framework"]`를 지정한다. Unity Test Runner에서 준비한 범위의 테스트를 선택해 실행한다. 전체 테스트 통과를 코드 전용 사본의 제공 보장으로 해석하지 않는다.
- 원본 저장소 루트의 `Scripts` 폴더와 프로젝트 검증·빌드 자동화는 공개 사본에 포함되지 않는다. 포함된 Tests·Editor 코드의 검증은 소비 프로젝트에 필요한 의존성과 에셋을 준비한 뒤 Unity Test Runner에서 수행한다.
