using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// EditMode 테스트에서 MonoBehaviour의 수명주기 콜백을 결정적으로 구동하는 도우미이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 에디터는 플레이 모드가 아닐 때 일반 MonoBehaviour의 OnDestroy를 호출하지 않는다.
    /// 그래서 <c>DestroyImmediate</c>를 불러도 파괴 시 정리·통지 코드가 실행되지 않고,
    /// 해제 규약을 검증하려는 테스트가 프로덕션 결함이 없는데도 실패한다.
    /// 여기서는 해당 콜백을 리플렉션으로 직접 호출해 플레이 모드와 같은 경로를 재현한다.
    /// </para>
    /// <para>
    /// 프레임워크 테스트 어셈블리에도 같은 도우미가 있지만 그 어셈블리는 게임 테스트가 참조하지 않으므로,
    /// 게임 쪽에 필요한 만큼만 따로 둔다.
    /// </para>
    /// </remarks>
    internal static class MonoBehaviourLifecycle
    {
        /// <summary>
        /// 컴포넌트의 Awake를 호출한다. 호출 차례는 호출자가 정하므로 불리한 순서를 재현할 수 있다.
        /// </summary>
        /// <remarks>
        /// 에디터는 플레이 모드가 아닐 때 Awake를 호출하지 않는다. 그래서 초기화가 순서에 기대는지 여부가
        /// EditMode에서는 드러나지 않고 플레이에서만 터진다. 이 호출로 그 순서를 테스트가 직접 정한다.
        /// </remarks>
        /// <param name="behaviour">수명주기를 구동할 컴포넌트이다.</param>
        internal static void InvokeAwake(MonoBehaviour behaviour)
        {
            InvokeLifecycleMethod(behaviour, "Awake");
        }

        /// <summary>
        /// 컴포넌트의 OnDestroy를 호출한다. 오브젝트 파괴 자체는 호출자가 정리한다.
        /// </summary>
        /// <param name="behaviour">수명주기를 구동할 컴포넌트이다.</param>
        internal static void InvokeOnDestroy(MonoBehaviour behaviour)
        {
            InvokeLifecycleMethod(behaviour, "OnDestroy");
        }

        /// <summary>
        /// 컴포넌트의 OnEnable을 호출한다.
        /// </summary>
        /// <remarks>
        /// 에디터는 플레이 모드가 아닐 때 OnEnable을 호출하지 않는다. 그래서 OnEnable에서 구독을 거는
        /// 컴포넌트는 이 호출 없이는 EditMode 검사에서 구독이 걸리지 않는다.
        /// </remarks>
        /// <param name="behaviour">수명주기를 구동할 컴포넌트이다.</param>
        internal static void InvokeOnEnable(MonoBehaviour behaviour)
        {
            InvokeLifecycleMethod(behaviour, "OnEnable");
        }

        /// <summary>
        /// 컴포넌트의 OnDisable을 호출한다.
        /// </summary>
        /// <param name="behaviour">수명주기를 구동할 컴포넌트이다.</param>
        internal static void InvokeOnDisable(MonoBehaviour behaviour)
        {
            InvokeLifecycleMethod(behaviour, "OnDisable");
        }

        /// <summary>
        /// 컴포넌트의 FixedUpdate를 한 번 호출한다. 에디터는 플레이 모드가 아닐 때 고정 스텝을 돌리지 않으므로
        /// 고정 스텝에 실린 일이 실제로 일어나는지는 이 호출로 본다.
        /// </summary>
        /// <param name="behaviour">수명주기를 구동할 컴포넌트이다.</param>
        internal static void InvokeFixedUpdate(MonoBehaviour behaviour)
        {
            InvokeLifecycleMethod(behaviour, "FixedUpdate");
        }

        /// <summary>
        /// 컴포넌트의 Start를 호출한다. 에디터는 플레이 모드가 아닐 때 Start를 호출하지 않으므로, 씬 진입 뒤 한 번
        /// 하는 일(재생·초기 배선)이 실제로 일어나는지는 이 호출로 본다.
        /// </summary>
        /// <param name="behaviour">수명주기를 구동할 컴포넌트이다.</param>
        internal static void InvokeStart(MonoBehaviour behaviour)
        {
            InvokeLifecycleMethod(behaviour, "Start");
        }

        /// <summary>
        /// 컴포넌트의 Update를 한 번 호출한다. 에디터는 플레이 모드가 아닐 때 화면 프레임을 돌리지 않으므로
        /// 표시 계층이 프레임마다 하는 일이 실제로 일어나는지는 이 호출로 본다.
        /// </summary>
        /// <param name="behaviour">수명주기를 구동할 컴포넌트이다.</param>
        internal static void InvokeUpdate(MonoBehaviour behaviour)
        {
            InvokeLifecycleMethod(behaviour, "Update");
        }

        /// <summary>
        /// 이름이 일치하는 비공개 수명주기 메서드를 찾아 호출한다.
        /// 파생 클래스가 재정의했으면 그쪽이 먼저 잡히므로, 기반 호출까지 포함된 실제 경로가 그대로 실행된다.
        /// </summary>
        /// <param name="behaviour">호출 대상 컴포넌트이다.</param>
        /// <param name="methodName">호출할 수명주기 메서드 이름이다.</param>
        private static void InvokeLifecycleMethod(MonoBehaviour behaviour, string methodName)
        {
            var method = FindLifecycleMethod(behaviour.GetType(), methodName);
            Assert.That(method, Is.Not.Null, $"{behaviour.GetType().Name}.{methodName} 메서드를 찾지 못했다.");
            method.Invoke(behaviour, null);
        }

        /// <summary>지정한 형식과 그 기반 형식에서 비공개 인스턴스 메서드를 찾는다.</summary>
        /// <param name="type">탐색을 시작할 형식이다.</param>
        /// <param name="methodName">찾을 메서드 이름이다.</param>
        /// <returns>찾은 메서드이며, 없으면 null을 반환한다.</returns>
        private static MethodInfo FindLifecycleMethod(Type type, string methodName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var method = current.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }
    }
}
