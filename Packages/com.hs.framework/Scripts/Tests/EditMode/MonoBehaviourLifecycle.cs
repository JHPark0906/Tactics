using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// EditMode 테스트에서 MonoBehaviour의 수명주기 콜백을 결정적으로 구동하는 도우미이다.
    /// </summary>
    /// <remarks>
    /// 에디터는 플레이 모드가 아닐 때 일반 MonoBehaviour의 OnEnable·OnDisable·OnDestroy를 호출하지 않는다.
    /// 그래서 enabled 값을 바꾸거나 DestroyImmediate를 호출해도 구독 해제나 자원 반환 코드가 실행되지 않아,
    /// 해제 규약을 검증하려는 테스트가 프로덕션 결함이 없는데도 실패한다.
    /// 여기서는 Unity가 설정할 상태(enabled)를 먼저 맞춘 뒤 해당 콜백을 리플렉션으로 호출해
    /// 플레이 모드와 같은 순서를 재현한다. 상태를 먼저 맞추는 이유는 콜백 본문이
    /// isActiveAndEnabled를 보고 동작을 결정하는 경우가 있기 때문이다.
    /// </remarks>
    internal static class MonoBehaviourLifecycle
    {
        /// <summary>컴포넌트를 활성 상태로 만들고 OnEnable을 호출한다.</summary>
        /// <param name="behaviour">수명주기를 구동할 컴포넌트이다.</param>
        internal static void InvokeOnEnable(MonoBehaviour behaviour)
        {
            behaviour.enabled = true;
            InvokeLifecycleMethod(behaviour, "OnEnable");
        }

        /// <summary>컴포넌트를 비활성 상태로 만들고 OnDisable을 호출한다.</summary>
        /// <param name="behaviour">수명주기를 구동할 컴포넌트이다.</param>
        internal static void InvokeOnDisable(MonoBehaviour behaviour)
        {
            behaviour.enabled = false;
            InvokeLifecycleMethod(behaviour, "OnDisable");
        }

        /// <summary>컴포넌트의 OnDestroy를 호출한다. 오브젝트 파괴 자체는 호출자가 정리한다.</summary>
        /// <param name="behaviour">수명주기를 구동할 컴포넌트이다.</param>
        internal static void InvokeOnDestroy(MonoBehaviour behaviour)
        {
            InvokeLifecycleMethod(behaviour, "OnDestroy");
        }

        /// <summary>
        /// 이름이 일치하는 비공개 수명주기 메서드를 찾아 호출한다.
        /// 기반 클래스에 선언된 콜백도 찾도록 상속 계층을 직접 거슬러 올라간다.
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
