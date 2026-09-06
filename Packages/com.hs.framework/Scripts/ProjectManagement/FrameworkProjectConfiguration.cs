using System;
using System.Collections.Generic;
using HS.Framework.Ability;
using HS.Framework.Scene;
using HS.Framework.Settings;
using UnityEngine;

namespace HS.Framework.ProjectManagement
{
    /// <summary>플레이어가 런타임에서 변경할 수 있는 설정 범주이다.</summary>
    [Flags]
    public enum ProjectUserSetting
    {
        None = 0,
        Graphics = 1 << 0,
        Audio = 1 << 1,
        Input = 1 << 2,
        Locale = 1 << 3,
        All = Graphics | Audio | Input | Locale
    }

    /// <summary>프로젝트에서 사용하는 씬의 역할이다.</summary>
    public enum ProjectSceneCategory
    {
        Bootstrap,
        Loading,
        MainMenu,
        Gameplay,
        General,
        Test
    }

    /// <summary>런타임 시스템이 사용하는 프로젝트 초기 설정 계약이다.</summary>
    /// <remarks>
    /// <para>
    /// 초기 설정 묶음을 그대로 노출하므로 이 계약을 참조하면 Settings 모듈도 함께 필요하다.
    /// 씬 정보만 필요한 호출부는 <see cref="IProjectSceneCatalog"/>만 요구해 결합을 줄인다.
    /// </para>
    /// <para>
    /// 어빌리티 시스템·효과 실행기·행동 트리 실행기가 시간을 흘릴 자리(게임의 "GameMode"에 해당)는
    /// 이 인터페이스가 아니라 <see cref="IGameplayTickModeSource"/>가 따로 정의한다. <see cref="FrameworkProjectConfiguration"/>이
    /// 두 인터페이스를 나란히(상속 없이) 구현하고, 등록하는 쪽(Runtime)이 같은 인스턴스를 두 토큰으로
    /// 컨테이너에 올려 둔다. 이 인터페이스가 <see cref="IGameplayTickModeSource"/>를 상속하면 이 인터페이스만
    /// 쓰는 모든 호출부(UI 설정 창, 에디터 도구 등)가 이유 없이 Ability 모듈까지 참조해야 하므로 상속을 쓰지 않는다.
    /// </para>
    /// </remarks>
    public interface IProjectConfiguration
    {
        /// <summary>초기 그래픽, 오디오, 입력과 로케일 설정이다.</summary>
        ClientSettingsConfiguration ClientSettings { get; }

        /// <summary>플레이어가 변경할 수 있는 설정 범주이다.</summary>
        ProjectUserSetting UserConfigurableSettings { get; }

        /// <summary>지정한 설정을 플레이어가 변경할 수 있는지 확인한다.</summary>
        bool IsUserConfigurable(ProjectUserSetting setting);
    }

    /// <summary>분류된 프로젝트 씬과 게임 레벨 연결을 조회하는 계약이다.</summary>
    public interface IProjectSceneCatalog
    {
        /// <summary>프로젝트가 사용하는 전체 씬 정의이다.</summary>
        IReadOnlyList<ProjectSceneDefinition> Scenes { get; }

        /// <summary>새 게임의 시작 레벨 ID이다.</summary>
        int DefaultGameplayLevelId { get; }

        /// <summary>Bootstrap 씬이다.</summary>
        SceneReference BootstrapScene { get; }

        /// <summary>전환 중 표시할 Loading 씬이다.</summary>
        SceneReference LoadingScene { get; }

        /// <summary>초기 진입 및 전환 실패 복구에 사용하는 MainMenu 씬이다.</summary>
        SceneReference MainMenuScene { get; }

        /// <summary>지정한 레벨 ID에 연결된 Gameplay 씬을 찾는다.</summary>
        bool TryGetGameplayScene(int levelId, out SceneReference scene);

        /// <summary>지정한 씬 경로에 연결된 Gameplay 레벨 ID를 찾는다.</summary>
        bool TryGetGameplayLevelId(string scenePath, out int levelId);
    }

    /// <summary>하나의 씬에 대한 안정적인 ID, 역할과 빌드 포함 정책이다.</summary>
    [Serializable]
    public sealed class ProjectSceneDefinition
    {
        [SerializeField] private string sceneId;
        [SerializeField] private ProjectSceneCategory category;
        [SerializeField] private SceneReference scene;
        [SerializeField] [Min(0)] private int gameplayLevelId;
        [SerializeField] private bool includeInBuild = true;

        /// <summary>저장 데이터와 기획에서 사용할 안정적인 씬 ID이다.</summary>
        public string SceneId => sceneId;

        /// <summary>씬의 역할이다.</summary>
        public ProjectSceneCategory Category => category;

        /// <summary>Unity 프로젝트 안의 씬 참조이다.</summary>
        public SceneReference Scene => scene;

        /// <summary>Gameplay 씬의 레벨 ID이며 다른 분류에서는 0이다.</summary>
        public int GameplayLevelId => gameplayLevelId;

        /// <summary>일반 또는 테스트 씬을 활성 Build Settings에 포함할지 여부이다.</summary>
        public bool IncludeInBuild => includeInBuild;

        /// <summary>프로젝트 씬 정의를 생성한다.</summary>
        public ProjectSceneDefinition(
            string sceneId,
            ProjectSceneCategory category,
            SceneReference scene,
            int gameplayLevelId = 0,
            bool includeInBuild = true)
        {
            this.sceneId = sceneId;
            this.category = category;
            this.scene = scene;
            this.gameplayLevelId = gameplayLevelId;
            this.includeInBuild = includeInBuild;
        }

        /// <summary>Build Settings에서 활성화해야 하는 씬인지 확인한다.</summary>
        public bool ShouldEnableInBuild()
        {
            return category is ProjectSceneCategory.Bootstrap
                or ProjectSceneCategory.Loading
                or ProjectSceneCategory.MainMenu || includeInBuild;
        }
    }

    /// <summary>Framework 초기화와 씬 전환을 구동하는 프로젝트 단위 설정 에셋이다.</summary>
    /// <remarks>
    /// 이 에셋은 씬 카탈로그와 초기 클라이언트 설정을 한 곳에 묶는 집계 루트이며,
    /// 그래서 ProjectManagement 모듈이 Settings 모듈을 참조한다. 모듈 이름이
    /// "프로젝트 정의 전체를 모으는 역할"까지 드러내지는 못하지만, 부트스트랩이
    /// 한 번의 조회로 프로젝트 정의를 모두 얻게 하려는 의도된 결합이므로 유지한다.
    /// 두 관심사를 별도 에셋으로 나누면 결합은 사라지지만 조립 계층이 여러 에셋을
    /// 찾아 맞춰야 하고 설정 누락을 실행 시점에야 알게 되어, 얻는 것보다 잃는 것이 크다.
    /// </remarks>
    public sealed class FrameworkProjectConfiguration : ScriptableObject, IProjectConfiguration, IGameplayTickModeSource, IProjectSceneCatalog
    {
        private const string ResourcesPath = "FrameworkProjectConfiguration";

        [SerializeField] private ClientSettingsConfiguration clientSettings;
        [SerializeField] private ProjectUserSetting userConfigurableSettings = ProjectUserSetting.All;
        [SerializeField] [Min(1)] private int defaultGameplayLevelId = 1;
        [SerializeField] private ProjectSceneDefinition[] scenes = Array.Empty<ProjectSceneDefinition>();

        [Tooltip("어빌리티 시스템·효과 실행기·행동 트리 실행기가 시간을 흘릴 자리이다. " +
                 "기본은 FixedUpdate이며, Update를 고르면 프레임률이 결과에 영향을 준다. " +
                 "이 프로젝트의 모든 유닛이 같은 자리를 쓰며, 유닛마다 다르게 고르는 방법은 없다.")]
        [SerializeField]
        private GameplayTickMode gameplayTickMode = GameplayTickMode.OnFixedUpdate;

        /// <inheritdoc />
        public ClientSettingsConfiguration ClientSettings => clientSettings;

        /// <inheritdoc />
        public ProjectUserSetting UserConfigurableSettings => userConfigurableSettings;

        /// <inheritdoc />
        public int DefaultGameplayLevelId => defaultGameplayLevelId;

        /// <inheritdoc />
        public GameplayTickMode GameplayTickMode => gameplayTickMode;

        /// <inheritdoc />
        public IReadOnlyList<ProjectSceneDefinition> Scenes => scenes;

        /// <inheritdoc />
        public SceneReference BootstrapScene => FindScene(ProjectSceneCategory.Bootstrap);

        /// <inheritdoc />
        public SceneReference LoadingScene => FindScene(ProjectSceneCategory.Loading);

        /// <inheritdoc />
        public SceneReference MainMenuScene => FindScene(ProjectSceneCategory.MainMenu);

        /// <summary>프로젝트 Resources에 등록된 런타임 설정을 불러온다.</summary>
        public static FrameworkProjectConfiguration Load()
        {
            return Resources.Load<FrameworkProjectConfiguration>(ResourcesPath);
        }

        /// <summary>프로젝트 Resources의 런타임 설정을 불러오며 없으면 예외를 발생시킨다.</summary>
        public static FrameworkProjectConfiguration LoadRequired()
        {
            return Load() ?? throw new InvalidOperationException(
                "FrameworkProjectConfiguration이 Resources에 등록되지 않았습니다. " +
                "Unity Project Settings > HS Framework에서 프로젝트를 초기화하세요.");
        }

        /// <inheritdoc />
        public bool IsUserConfigurable(ProjectUserSetting setting)
        {
            return setting != ProjectUserSetting.None && (userConfigurableSettings & setting) == setting;
        }

        /// <inheritdoc />
        public bool TryGetGameplayScene(int levelId, out SceneReference scene)
        {
            foreach (var definition in scenes)
            {
                if (definition is { Category: ProjectSceneCategory.Gameplay } &&
                    definition.GameplayLevelId == levelId &&
                    definition.Scene is { IsAssigned: true })
                {
                    scene = definition.Scene;
                    return true;
                }
            }

            scene = null;
            return false;
        }

        /// <inheritdoc />
        public bool TryGetGameplayLevelId(string scenePath, out int levelId)
        {
            var normalizedPath = SceneLoader.NormalizeScenePath(scenePath);
            foreach (var definition in scenes)
            {
                if (definition is not { Category: ProjectSceneCategory.Gameplay } ||
                    definition.Scene == null ||
                    !string.Equals(definition.Scene.ScenePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                levelId = definition.GameplayLevelId;
                return true;
            }

            levelId = default;
            return false;
        }

        /// <summary>필수 설정, 씬 분류와 ID의 일관성을 검사한다.</summary>
        public bool TryValidate(out string error)
        {
            if (clientSettings == null)
            {
                error = "초기 클라이언트 설정이 지정되지 않았습니다.";
                return false;
            }

            if (!clientSettings.TryValidate(out error))
            {
                return false;
            }

            if (scenes == null || scenes.Length == 0)
            {
                error = "프로젝트 씬 목록이 비어 있습니다.";
                return false;
            }

            var sceneIds = new HashSet<string>(StringComparer.Ordinal);
            var scenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var gameplayLevelIds = new HashSet<int>();
            foreach (var definition in scenes)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.SceneId))
                {
                    error = "모든 프로젝트 씬에는 비어 있지 않은 씬 ID가 필요합니다.";
                    return false;
                }

                if (!sceneIds.Add(definition.SceneId))
                {
                    error = $"씬 ID '{definition.SceneId}'가 중복되었습니다.";
                    return false;
                }

                if (definition.Scene is not { IsAssigned: true })
                {
                    error = $"씬 '{definition.SceneId}'의 Unity 씬이 지정되지 않았습니다.";
                    return false;
                }

                if (!scenePaths.Add(definition.Scene.ScenePath))
                {
                    error = $"씬 경로 '{definition.Scene.ScenePath}'가 중복되었습니다.";
                    return false;
                }

                if (definition.Category == ProjectSceneCategory.Gameplay &&
                    (definition.GameplayLevelId < 1 || !gameplayLevelIds.Add(definition.GameplayLevelId)))
                {
                    error = $"Gameplay 씬 '{definition.SceneId}'의 레벨 ID가 유효하지 않거나 중복되었습니다.";
                    return false;
                }
            }

            if (!HasExactlyOne(ProjectSceneCategory.Bootstrap) ||
                !HasExactlyOne(ProjectSceneCategory.Loading) ||
                !HasExactlyOne(ProjectSceneCategory.MainMenu))
            {
                error = "Bootstrap, Loading, MainMenu 씬은 각각 정확히 하나씩 필요합니다.";
                return false;
            }

            if (!TryGetGameplayScene(defaultGameplayLevelId, out _))
            {
                error = $"기본 Gameplay 레벨 ID {defaultGameplayLevelId}에 연결된 씬이 없습니다.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>구조 검증과 함께 활성 Build Settings가 프로젝트 설정과 일치하는지 검사한다.</summary>
        public bool TryValidateRuntime(out string error)
        {
            if (!TryValidate(out error))
            {
                return false;
            }

            foreach (var definition in scenes)
            {
                if (definition.ShouldEnableInBuild() && !SceneLoader.CanLoadScene(definition.Scene))
                {
                    error = $"씬 '{definition.SceneId}'가 활성 Build Settings에 없습니다.";
                    return false;
                }
            }

            if (!SceneLoader.TryGetBuildIndex(BootstrapScene, out var bootstrapIndex) || bootstrapIndex != 0)
            {
                error = "Bootstrap 씬은 활성 Build Settings의 인덱스 0이어야 합니다.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>테스트와 런타임 조립에 사용할 프로젝트 설정 인스턴스를 생성한다. 틱 방식은 기본값을 쓴다.</summary>
        public static FrameworkProjectConfiguration CreateRuntime(
            ClientSettingsConfiguration clientSettings,
            ProjectUserSetting userSettings,
            int defaultLevelId,
            params ProjectSceneDefinition[] sceneDefinitions)
        {
            return CreateRuntime(
                clientSettings, userSettings, defaultLevelId, GameplayTickMode.OnFixedUpdate, sceneDefinitions);
        }

        /// <summary>테스트와 런타임 조립에 사용할 프로젝트 설정 인스턴스를, 틱 방식까지 지정해 생성한다.</summary>
        public static FrameworkProjectConfiguration CreateRuntime(
            ClientSettingsConfiguration clientSettings,
            ProjectUserSetting userSettings,
            int defaultLevelId,
            GameplayTickMode gameplayTickMode,
            params ProjectSceneDefinition[] sceneDefinitions)
        {
            var configuration = CreateInstance<FrameworkProjectConfiguration>();
            configuration.clientSettings = clientSettings;
            configuration.userConfigurableSettings = userSettings;
            configuration.defaultGameplayLevelId = defaultLevelId;
            configuration.gameplayTickMode = gameplayTickMode;
            configuration.scenes = sceneDefinitions ?? Array.Empty<ProjectSceneDefinition>();
            return configuration;
        }

        private SceneReference FindScene(ProjectSceneCategory category)
        {
            foreach (var definition in scenes)
            {
                if (definition?.Category == category)
                {
                    return definition.Scene;
                }
            }

            return null;
        }

        private bool HasExactlyOne(ProjectSceneCategory category)
        {
            var count = 0;
            foreach (var definition in scenes)
            {
                if (definition?.Category == category)
                {
                    count++;
                }
            }

            return count == 1;
        }
    }
}
