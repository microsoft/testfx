// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

internal static class MethodInfoExtensions
{
    /// <summary>
    /// Verifies that the class initialize has the correct signature.
    /// </summary>
    /// <param name="method">The method to verify.</param>
    /// <returns>True if the method has the right Assembly/Class initialize signature.</returns>
    internal static bool HasCorrectClassOrAssemblyInitializeSignature(this MethodInfo method)
    {
        DebugEx.Assert(method != null, "method should not be null.");

        ParameterInfo[] parameters = method.GetParameters();

        return
            method is { IsStatic: true, IsPublic: true } &&
            (parameters.Length == 1) &&
            parameters[0].ParameterType == typeof(TestContext) &&
            method.IsValidReturnType();
    }

    /// <summary>
    /// Verifies that the class cleanup has the correct signature.
    /// </summary>
    /// <param name="method">The method to verify.</param>
    /// <returns>True if the method has the right Assembly/Class cleanup signature.</returns>
    internal static bool HasCorrectClassOrAssemblyCleanupSignature(this MethodInfo method)
    {
        DebugEx.Assert(method != null, "method should not be null.");

        return
            method is { IsStatic: true, IsPublic: true } &&
            (method.GetParameters().Length == 0) &&
            method.IsValidReturnType();
    }

    /// <summary>
    /// Verifies that the test Initialize/cleanup has the correct signature.
    /// </summary>
    /// <param name="method">The method to verify.</param>
    /// <returns>True if the method has the right test init/cleanup signature.</returns>
    internal static bool HasCorrectTestInitializeOrCleanupSignature(this MethodInfo method)
    {
        DebugEx.Assert(method != null, "method should not be null.");

        return
            method is { IsStatic: false, IsPublic: true } &&
            (method.GetParameters().Length == 0) &&
            method.IsValidReturnType();
    }

    /// <summary>
    /// Verifies that the test method has the correct signature.
    /// </summary>
    /// <param name="method">The method to verify.</param>
    /// <param name="ignoreParameterLength">Indicates whether parameter length is to be ignored.</param>
    /// <param name="discoverInternals">True if internal test classes and test methods should be discovered in
    /// addition to public test classes and methods.</param>
    /// <returns>True if the method has the right test method signature.</returns>
    internal static bool HasCorrectTestMethodSignature(this MethodInfo method, bool ignoreParameterLength, bool discoverInternals = false)
    {
        DebugEx.Assert(method != null, "method should not be null.");

        return
            method is { IsAbstract: false, IsStatic: false, IsGenericMethod: false } &&
            (method.IsPublic || (discoverInternals && method.IsAssembly)) &&
            (method.GetParameters().Length == 0 || ignoreParameterLength) &&
            method.IsValidReturnType(); // Match return type Task for async methods only. Else return type void.
    }

    /// <summary>
    /// Checks whether test method has correct Timeout attribute.
    /// </summary>
    /// <param name="method">The method to verify.</param>
    /// <param name="timeoutAttribute">The timeout attribute when we already have it.</param>
    /// <returns>True if the method has the right test timeout signature.</returns>
    internal static bool HasCorrectTimeout(this MethodInfo method, TimeoutAttribute? timeoutAttribute = null)
    {
        DebugEx.Assert(method != null, "method should not be null.");

        // TODO: redesign this, probably change this to GetTimeout? so we don't have to do this weird dance?
        timeoutAttribute ??= ReflectHelper.Instance.GetFirstNonDerivedAttributeOrDefault<TimeoutAttribute>(method, inherit: false);

        return timeoutAttribute?.Timeout > 0;
    }

    /// <summary>
    /// Check is return type is void for non async and Task for async methods.
    /// </summary>
    /// <param name="method">The method to verify.</param>
    /// <returns>True if the method has a void/task return type..</returns>
    internal static bool IsValidReturnType(this MethodInfo method)
        => ReflectHelper.MatchReturnType(method, typeof(Task))
#if NETCOREAPP
        || ReflectHelper.MatchReturnType(method, typeof(ValueTask))
#endif
        || (ReflectHelper.MatchReturnType(method, typeof(void)) && method.GetAsyncTypeName() == null);

    /// <summary>
    /// For async methods compiler generates different type and method.
    /// Gets the compiler generated type name for given async test method.
    /// </summary>
    /// <param name="method">The method to verify.</param>
    /// <returns>Compiler generated type name for given async test method..</returns>
    internal static string? GetAsyncTypeName(this MethodInfo method)
    {
        AsyncStateMachineAttribute? asyncStateMachineAttribute = ReflectHelper.Instance.GetFirstNonDerivedAttributeOrDefault<AsyncStateMachineAttribute>(method, inherit: false);

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
    /// <param name="arguments">
    /// Arguments for the methodInfo invoke.
    /// </param>
    internal static void InvokeAsSynchronousTask(this MethodInfo methodInfo, object? classInstance, params object?[]? arguments)
    {
        ParameterInfo[]? methodParameters = methodInfo.GetParameters();

        // check if test method expected parameter values but no test data was provided,
        // throw error with appropriate message.
        if (methodParameters is { Length: > 0 } && arguments == null)
        {
            throw new TestFailedException(
                ObjectModel.UnitTestOutcome.Error,
                string.Format(
                    CultureInfo.InvariantCulture,
                    Resource.CannotRunTestMethodNoDataError,
                    methodInfo.DeclaringType!.FullName,
                    methodInfo.Name));
        }

        object? invokeResult;

        if (arguments is not null
            && methodParameters?.Length == 1
            && methodParameters[0].ParameterType == typeof(object[]))
        {
            invokeResult = methodInfo.Invoke(classInstance, [arguments]);
        }
        else
        {
            int methodParametersLengthOrZero = methodParameters?.Length ?? 0;
            int argumentsLengthOrZero = arguments?.Length ?? 0;
            if (methodParametersLengthOrZero != argumentsLengthOrZero)
            {
                throw GetParameterCountMismatchException(methodInfo, arguments, methodParameters, methodParametersLengthOrZero, argumentsLengthOrZero, innerException: null);
            }

            try
            {
                invokeResult = methodInfo.Invoke(classInstance, arguments);
            }
            catch (Exception ex) when (ex is TargetParameterCountException or ArgumentException)
            {
                throw GetParameterCountMismatchException(methodInfo, arguments, methodParameters, methodParametersLengthOrZero, argumentsLengthOrZero, ex);
            }
        }

        // If methodInfo is an async method, wait for returned task
        if (invokeResult is Task task)
        {
            task.GetAwaiter().GetResult();
        }
#if NET6_0_OR_GREATER
        else if (invokeResult is ValueTask valueTask)
        {
            valueTask.GetAwaiter().GetResult();
        }
#endif
    }

    private static TestFailedException GetParameterCountMismatchException(MethodInfo methodInfo, object?[]? arguments, ParameterInfo[]? methodParameters, int methodParametersLengthOrZero, int argumentsLengthOrZero, Exception? innerException) =>
        new(
            ObjectModel.UnitTestOutcome.Error,
            string.Format(
                CultureInfo.InvariantCulture,
                Resource.CannotRunTestArgumentsMismatchError,
                methodInfo.DeclaringType!.FullName,
                methodInfo.Name,
                methodParametersLengthOrZero,
                string.Join(", ", methodParameters?.Select(p => p.ParameterType.Name) ?? Array.Empty<string>()),
                argumentsLengthOrZero,
                string.Join(", ", arguments?.Select(a => a?.GetType().Name ?? "null") ?? Array.Empty<string>())), innerException);
}
