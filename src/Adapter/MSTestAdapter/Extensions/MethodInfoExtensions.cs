// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions
{
    using System.Diagnostics;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Threading.Tasks;
    using System.Runtime.CompilerServices;

    internal static class MethodInfoExtensions
    {
        /// <summary>
        /// Verifies that the class initialize has the correct signature
        /// </summary>
        internal static bool HasCorrectClassOrAssemblyInitializeSignature(this MethodInfo method)
        {
            Debug.Assert(method != null);

            ParameterInfo[] parameters = method.GetParameters();

            return
                method.IsStatic &&
                method.IsPublic &&
                (parameters.Length == 1) &&
                parameters[0].ParameterType == typeof(TestContext) &&
                method.IsVoidOrTaskReturnType();
        }

        /// <summary>
        /// Verifies that the class cleanup has the correct signature
        /// </summary>
        internal static bool HasCorrectClassOrAssemblyCleanupSignature(this MethodInfo method)
        {
            Debug.Assert(method != null);

            return
                method.IsStatic &&
                method.IsPublic &&
                (method.GetParameters().Length == 0) &&
                method.IsVoidOrTaskReturnType();
        }

        /// <summary>
        /// Verifies that the test Initiailize/cleanup has the correct signature
        /// </summary>
        internal static bool HasCorrectTestInitializeOrCleanupSignature(this MethodInfo method)
        {
            Debug.Assert(method != null);

            return
                !method.IsStatic &&
                method.IsPublic &&
                (method.GetParameters().Length == 0) &&
                method.IsVoidOrTaskReturnType();
        }

        /// <summary>
        /// Verifies that the test method has the correct signature
        /// </summary>
        internal static bool HasCorrectTestMethodSignature(this MethodInfo method, bool ignoreParameterLength)
        {
            Debug.Assert(method != null);

            return
                !method.IsAbstract &&
                !method.IsStatic &&
                !method.IsGenericMethod &&
                method.IsPublic &&
                (method.GetParameters().Length == 0 || ignoreParameterLength) &&
                method.IsVoidOrTaskReturnType(); // Match return type Task for async methods only. Else return type void.
        }

        /// <summary>
        /// Checks whether test method has correct Timeout attribute.
        /// </summary>
        internal static bool HasCorrectTimeout(this MethodInfo method)
        {
            Debug.Assert(method != null);

            // There should be one and only one TimeoutAttribute.
            var attributes = ReflectHelper.GetCustomAttributes(method, typeof(TimeoutAttribute), false);
            if (attributes?.Length != 1)
            {
                return false;
            }

            // Timeout cannot be less than 0.
            var attribute = attributes[0] as TimeoutAttribute;

            return !(attribute?.Timeout < 0);
        }

        /// <summary>
        /// Check is return type is void for non async and Task for async methods.
        /// </summary>
        internal static bool IsVoidOrTaskReturnType(this MethodInfo method)
        {
            return method.GetAsyncTypeName() == null ? ReflectHelper.MatchReturnType(method, typeof(void))
                : ReflectHelper.MatchReturnType(method, typeof(Task));
        }

        /// <summary>
        /// For async methods compiler generates different type and method.
        /// Return compiler generated type name for given async test method.
        /// </summary>
        internal static string GetAsyncTypeName(this MethodInfo method)
        {
            var asyncStateMachineAttribute = method.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) as AsyncStateMachineAttribute;

            return asyncStateMachineAttribute?.StateMachineType?.FullName;
        }

        /// <summary>
        /// Invoke a <see cref="MethodInfo"/> as a synchronous <see cref="Task"/>.
        /// </summary>
        /// <param name="methodInfo">
        /// <see cref="MethodInfo"/> instance.
        /// </param>
        /// <param name="classInstance">
        /// Instance of the on which methodInfo is invoked.
        /// </param>
        /// <param name="parameters">
        /// Arguments for the methodInfo invoke.
        /// </param>
        internal static void InvokeAsSynchronousTask(this MethodInfo methodInfo, object classInstance, params object[] parameters)
        {
            var task = methodInfo.Invoke(classInstance, parameters) as Task;

            // If methodInfo is an Async method, wait for returned task
            task?.GetAwaiter().GetResult();
        }
    }
}
