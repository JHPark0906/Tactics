using Cysharp.Threading.Tasks;

namespace HS.Framework.Scene
{
    /// <summary>로딩 화면의 표시 정책을 씬 전환 조정자에 제공한다.</summary>
    public interface ILoadingSceneDisplayPolicy
    {
        /// <summary>현재 로딩 화면이 요구하는 최소 표시 시간을 기다린다.</summary>
        UniTask WaitForMinimumDisplayAsync();
    }

    /// <summary>씬에 배치된 LoadingSceneController를 사용하는 기본 표시 정책이다.</summary>
    public sealed class LoadingSceneDisplayPolicy : ILoadingSceneDisplayPolicy
    {
        /// <inheritdoc />
        public UniTask WaitForMinimumDisplayAsync()
        {
            return LoadingSceneController.WaitForMinimumDisplayAsync();
        }
    }
}
