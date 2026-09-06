using System.Collections.Generic;
using UnityEngine;

namespace HS.Framework.Editor.Packaging
{
    /// <summary>
    /// 패키지 경계 검사에서 예외로 허용할 참조 이름을 프로젝트가 직접 선언하는 설정 에셋이다.
    /// 프레임워크 파일을 고치지 않고 예외를 추가할 수 있도록, 검증기는 프로젝트 어디에 있든
    /// 이 형식의 에셋을 모두 찾아 기본 허용 목록과 합친다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PackageBoundaryAllowList",
        menuName = "HS Framework/Package Boundary Allow List")]
    public sealed class PackageBoundaryAllowList : ScriptableObject
    {
        [SerializeField]
        [Tooltip("패키지 밖에 있어도 허용할 어셈블리 이름이나 DLL 파일 이름이다. 예: MyPlugin.dll")]
        private string[] allowedReferences = new string[0];

        /// <summary>이 에셋이 허용하는 참조 이름 목록이다.</summary>
        public IReadOnlyList<string> AllowedReferences => allowedReferences ?? new string[0];
    }
}
