using System;
using HS.Framework.ProjectManagement;

namespace HS.Tactics.Flow
{
    /// <summary>
    /// 씬 전환에도 유지되어야 하는 스테이지 진행 상태와 그 저장 참여자를 함께 보관하는 프로젝트 서비스이다.
    /// </summary>
    /// <remarks>
    /// 스테이지를 오갈 때마다 전투 씬의 컴포넌트는 새로 만들어지므로, 진행 상태를 그 컴포넌트가 들고 있으면
    /// 이동할 때마다 초기화된다. 그래서 진행 상태는 이 서비스가 소유하고
    /// VContainer에 등록해 씬 수명과 분리한다. 전투 씬의 <see cref="StageFlowController"/>와
    /// 메인 메뉴 화면이 같은 서비스를 주입받으므로, 전투가 남긴 클리어 기록을 메뉴가 그대로 본다.
    /// </remarks>
    public sealed class StageProgressionService
    {
        /// <summary>스테이지 진행 서비스를 생성한다.</summary>
        /// <param name="sceneCatalog">첫 스테이지 번호를 조회할 프로젝트 씬 카탈로그이다.</param>
        /// <exception cref="ArgumentNullException">씬 카탈로그가 null이면 발생한다.</exception>
        public StageProgressionService(IProjectSceneCatalog sceneCatalog)
        {
            if (sceneCatalog == null)
            {
                throw new ArgumentNullException(nameof(sceneCatalog));
            }

            Progression = new StageProgression(sceneCatalog.DefaultGameplayLevelId);
            Saveable = new StageProgressSaveable(Progression);
        }

        /// <summary>현재 캠페인 진행 상태이다.</summary>
        public StageProgression Progression { get; }

        /// <summary>진행 상태를 저장 시스템에 참여시키는 어댑터이다.</summary>
        public StageProgressSaveable Saveable { get; }
    }
}
