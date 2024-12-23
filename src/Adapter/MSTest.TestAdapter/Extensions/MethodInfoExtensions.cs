// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter;
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
            HasCorrectClassOrAssemblyCleanupParameters(method) &&
            method.IsValidReturnType();
    }

    private static bool HasCorrectClassOrAssemblyCleanupParameters(MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        return parameters.Length == 0 || (parameters.Length == 1 && parameters[0].ParameterType == typeof(TestContext));
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
            method is { IsAbstract: false, IsStatic: false } &&
            (method.IsPublic || (discoverInternals && method.IsAssembly)) &&
            (method.GetParameters().Length == 0 || ignoreParameterLength) &&
            method.IsValidReturnType(); // Match return type Task for async methods only. Else return type void.
    }

    /// <summary>
    /// Check is return type is void for non async and Task for async methods.
    /// </summary>
    /// <param name="method">The method to verify.</param>
    /// <returns>True if the method has a void/task return type..</returns>
    internal static bool IsValidReturnType(this MethodInfo method)
        => ReflectHelper.MatchReturnType(method, typeof(Task))
        || ReflectHelper.MatchReturnType(method, typeof(ValueTask))
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
            && (arguments.Length != 1 || arguments[0] is not null)
            && methodParameters.Length == 1
            && methodParameters[0].ParameterType == typeof(object[]))
        {
            invokeResult = methodInfo.Invoke(classInstance, [arguments]);
        }
        else
        {
            int methodParametersLengthOrZero = methodParameters?.Length ?? 0;
            int argumentsLengthOrZero = arguments?.Length ?? 0;

#if WINDOWS_UWP
            // There is a bug with UWP in release mode where the arguments are wrapped in an object[], so we need to unwrap it.
            // See https://github.com/microsoft/testfx/issues/3071
            if (argumentsLengthOrZero == 1
                && argumentsLengthOrZero < methodParametersLengthOrZero
                && arguments![0] is object[] args)
            {
                arguments = args;
                argumentsLengthOrZero = args.Length;
            }
#endif

            try
            {
                if (methodInfo.IsGenericMethod)
                {
                    methodInfo = ConstructGenericMethod(methodInfo, arguments);
                }

                invokeResult = methodInfo.Invoke(classInstance, arguments);
            }
            catch (Exception ex) when (ex is TargetParameterCountException or ArgumentException)
            {
                throw new TestFailedException(
                    ObjectModel.UnitTestOutcome.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        Resource.CannotRunTestArgumentsMismatchError,
                        methodInfo.DeclaringType!.FullName,
                        methodInfo.Name,
                        methodParametersLengthOrZero,
                        string.Join(", ", methodParameters?.Select(p => p.ParameterType.Name) ?? Array.Empty<string>()),
                        argumentsLengthOrZero,
                        string.Join(", ", arguments?.Select(a => a?.GetType().Name ?? "null") ?? Array.Empty<string>())), ex);
            }
        }

        // If methodInfo is an async method, wait for returned task
        if (invokeResult is Task task)
        {
            task.GetAwaiter().GetResult();
        }
        else if (invokeResult is ValueTask valueTask)
        {
            valueTask.GetAwaiter().GetResult();
        }
    }

    // Scenarios to test:
    //
    // [DataRow(null, "Hello")]
    // [DataRow("Hello", null)]
    // public void TestMethod<T>(T t1, T t2) { }
    //
    // [DataRow(0, "Hello")]
    // public void TestMethod<T1, T2>(T2 p0, T1, p1) { }
    private static MethodInfo ConstructGenericMethod(MethodInfo methodInfo, object?[]? arguments)
    {
        DebugEx.Assert(methodInfo.IsGenericMethod, "ConstructGenericMethod should only be called for a generic method.");

        if (arguments is null)
        {
            // An example where this could happen is:
            // [TestMethod]
            // public void MyTestMethod<T>() { }
            throw new TestFailedException(ObjectModel.UnitTestOutcome.Error, string.Format(CultureInfo.InvariantCulture, Resource.GenericParameterCantBeInferredBecauseNoArguments, methodInfo.Name));
        }

        Type[] genericDefinitions = methodInfo.GetGenericArguments();
        var map = new (Type GenericDefinition, Type? Substitution)[genericDefinitions.Length];
        for (int i = 0; i < map.Length; i++)
        {
            map[i] = (genericDefinitions[i], null);
        }

        ParameterInfo[] parameters = methodInfo.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            if (!parameterType.IsGenericMethodParameter() || arguments[i] is null)
            {
                continue;
            }

            Type substitution = arguments[i]!/*Very strange nullability warning*/.GetType();
            int mapIndexForParameter = GetMapIndexForParameterType(parameterType, map);
            Type? existingSubstitution = map[mapIndexForParameter].Substitution;

            if (existingSubstitution is null || substitution.IsAssignableFrom(existingSubstitution))
            {
                map[mapIndexForParameter] = (parameterType, substitution);
            }
            else if (existingSubstitution.IsAssignableFrom(substitution))
            {
                // Do nothing. We already have a good existing substitution.
            }
            else
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Resource.GenericParameterConflict, parameterType.Name, existingSubstitution, substitution));
            }
        }

        for (int i = 0; i < map.Length; i++)
        {
            // TODO: Better to throw? or tolerate and transform to typeof(object)?
            // This is reachable in the following case for example:
            // [DataRow(null)]
            // public void TestMethod<T>(T t) { }
            Type substitution = map[i].Substitution ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Resource.GenericParameterCantBeInferred, map[i].GenericDefinition.Name));
            genericDefinitions[i] = substitution;
        }

        try
        {
            return methodInfo.MakeGenericMethod(genericDefinitions);
        }
        catch (Exception e)
        {
            // The caller catches ArgumentExceptions and will lose the original exception details.
            // We transform the exception to TestFailedException here to preserve its details.
            throw new TestFailedException(ObjectModel.UnitTestOutcome.Error, e.TryGetMessage(), e.TryGetStackTraceInformation(), e);
        }
    }

    private static int GetMapIndexForParameterType(Type parameterType, (Type GenericDefinition, Type? Substitution)[] map)
    {
        for (int i = 0; i < map.Length; i++)
        {
            if (parameterType == map[i].GenericDefinition)
            {
                return i;
            }
        }

        throw ApplicationStateGuard.Unreachable();
    }
}
