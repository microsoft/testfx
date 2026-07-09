// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
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
    /// <param name="reflectHelper">The reflection service to use.</param>
    /// <returns>True if the method has a void/task return type..</returns>
    internal static bool IsValidReturnType(this MethodInfo method, ReflectHelper? reflectHelper = null)
        => ReflectHelper.MatchReturnType(method, typeof(Task))
        || (ReflectHelper.MatchReturnType(method, typeof(void)) && method.GetAsyncTypeName(reflectHelper) == null)
        // Keep this the last check, as it avoids loading System.Threading.Tasks.Extensions unnecessarily.
        || method.IsValueTask();

    // Avoid loading System.Threading.Tasks.Extensions if not needed.
    // Note: .NET runtime will load all types once it's entering the method.
    // So, moving this out of the method will load System.Threading.Tasks.Extensions
    // Even when invokeResult is null or Task.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsValueTask(this MethodInfo method)
        => ReflectHelper.MatchReturnType(method, typeof(ValueTask));

    /// <summary>
    /// For async methods compiler generates different type and method.
    /// Gets the compiler generated type name for given async test method.
    /// </summary>
    /// <param name="method">The method to verify.</param>
    /// <param name="reflectHelper">The reflection service to use.</param>
    /// <returns>Compiler generated type name for given async test method..</returns>
    internal static string? GetAsyncTypeName(this MethodInfo method, ReflectHelper? reflectHelper = null)
    {
        AsyncStateMachineAttribute? asyncStateMachineAttribute = (reflectHelper ?? ReflectHelper.Instance).GetFirstAttributeOrDefault<AsyncStateMachineAttribute>(method);
        return asyncStateMachineAttribute?.StateMachineType?.FullName;
    }

    /// <summary>
    /// Invokes a static lifecycle fixture method (assembly/class initialize or cleanup) and captures the
    /// <see cref="ExecutionContext"/> that results from its execution.
    /// </summary>
    /// <param name="methodInfo">The fixture method to invoke.</param>
    /// <param name="testContext">The test context to set as current and (when the method is parameterized) pass to the method.</param>
    /// <param name="setExecutionContext">
    /// Receives the <see cref="ExecutionContext"/> captured after the fixture method executed. This context
    /// contains async locals set by the fixture method and is used to flow state to subsequent lifecycle methods.
    /// </param>
    /// <param name="afterExecutionContextCaptured">Optional callback invoked after the execution context has been captured (e.g. to snapshot test context properties).</param>
    internal static async SynchronizationContextPreservingTask InvokeAsFixtureMethodAsync(
        this MethodInfo methodInfo,
        TestContext testContext,
        Action<ExecutionContext?> setExecutionContext,
        Action? afterExecutionContextCaptured = null)
    {
        // NOTE: It's unclear what the effect is if we reset the current test context before vs after the capture.
        // It's safer to reset it before the capture.
        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            Task? task = methodInfo.GetParameters().Length == 0
                ? methodInfo.GetInvokeResultAsync(null)
                : methodInfo.GetInvokeResultAsync(null, testContext);
            if (task is not null)
            {
                await task.ConfigureAwait(false);
            }
        }

        // **After** we have executed the fixture method, we save the current context.
        // This context will contain async locals set by the fixture method.
        setExecutionContext(ExecutionContext.Capture());

        afterExecutionContextCaptured?.Invoke();
    }

    internal static Task? GetInvokeResultAsync(this MethodInfo methodInfo, object? classInstance, params object?[]? arguments)
        // MethodInfo.GetParameters() allocates a fresh ParameterInfo[] on every call (CLR safety
        // guarantee). Non-hot-path callers go through this thin wrapper; the hot path (data-driven
        // test invocation) calls GetInvokeResultWithParametersAsync directly with an already-cached
        // array to avoid the per-invocation allocation.
        => methodInfo.GetInvokeResultWithParametersAsync(classInstance, methodInfo.GetParameters(), arguments);

    internal static Task? GetInvokeResultWithParametersAsync(this MethodInfo methodInfo, object? classInstance, ParameterInfo[] methodParameters, params object?[]? arguments)
    {
        // check if test method expected parameter values but no test data was provided,
        // throw error with appropriate message.
        if (methodParameters is { Length: > 0 } && arguments is null)
        {
            throw new TestFailedException(
                UnitTestOutcome.Error,
                string.Format(
                    CultureInfo.InvariantCulture,
                    Resource.CannotRunTestMethodNoDataError,
                    methodInfo.DeclaringType!.FullName,
                    methodInfo.Name));
        }

        // Reflection-free fast path: when the source generator registered a direct invoker for this
        // method, call it instead of MethodInfo.Invoke. The registry invoker always returns a
        // non-null Task (Task.CompletedTask for void/sync methods), so the unwrap below resolves via
        // the `Task t` arm; the switch is shared with the reflection path for shape consistency.
        // Generic methods are never source-generated, so they always fall through to reflection.
        Func<object?, object?[]?, object?>? sourceGeneratedInvoker = PlatformServiceProvider.Instance.ReflectionOperations.GetTestMethodInvoker(methodInfo);
        if (sourceGeneratedInvoker is not null)
        {
            object?[]? sourceGeneratedArguments = TestDataSourceHelpers.IsDataConsideredSingleArgumentValue(arguments, methodParameters)
                ? [arguments]
                : arguments;

            // The emitted invoker indexes its arguments positionally, so an argument-count mismatch
            // (e.g. a runtime data source supplying the wrong number of values) would throw an opaque
            // IndexOutOfRangeException. Mirror the reflection path's friendly diagnostic instead. We
            // only validate the count here — a type mismatch surfaces as the test's own exception and
            // must not be reinterpreted as an arguments error.
            int expectedParameterCount = methodParameters.Length;
            int providedArgumentCount = sourceGeneratedArguments?.Length ?? 0;
            if (expectedParameterCount != providedArgumentCount)
            {
                throw new TestFailedException(
                    UnitTestOutcome.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        Resource.CannotRunTestArgumentsMismatchError,
                        methodInfo.DeclaringType!.FullName,
                        methodInfo.Name,
                        expectedParameterCount,
                        string.Join(", ", methodParameters.Select(p => p.ParameterType.Name)),
                        providedArgumentCount,
                        string.Join(", ", sourceGeneratedArguments?.Select(a => a?.GetType().Name ?? "null") ?? [])));
            }

            object? sourceGeneratedResult = sourceGeneratedInvoker(classInstance, sourceGeneratedArguments);
            return sourceGeneratedResult switch
            {
                null => null,
                Task t => t,
                _ => TryGetTaskFromValueTaskAsync(sourceGeneratedResult),
            };
        }

        object? invokeResult;

        if (TestDataSourceHelpers.IsDataConsideredSingleArgumentValue(arguments, methodParameters))
        {
            invokeResult = methodInfo.Invoke(classInstance, [arguments]);
        }
        else
        {
            int methodParametersLengthOrZero = methodParameters.Length;
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
                    methodInfo = ConstructGenericMethod(methodInfo, methodParameters, arguments);
                }

                invokeResult = methodInfo.Invoke(classInstance, arguments);
            }
            catch (Exception ex) when (ex is TargetParameterCountException or ArgumentException)
            {
                throw new TestFailedException(
                    UnitTestOutcome.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        Resource.CannotRunTestArgumentsMismatchError,
                        methodInfo.DeclaringType!.FullName,
                        methodInfo.Name,
                        methodParametersLengthOrZero,
                        string.Join(", ", methodParameters.Select(p => p.ParameterType.Name)),
                        argumentsLengthOrZero,
                        string.Join(", ", arguments?.Select(a => a?.GetType().Name ?? "null") ?? [])), ex);
            }
        }

        return invokeResult switch
        {
            null => null,
            Task t => t,
            _ => TryGetTaskFromValueTaskAsync(invokeResult),
        };
    }

    // Avoid loading System.Threading.Tasks.Extensions if not needed.
    // Note: .NET runtime will load all types once it's entering the method.
    // So, moving this out of the method will load System.Threading.Tasks.Extensions
    // Even when invokeResult is null or Task.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task? TryGetTaskFromValueTaskAsync(object invokeResult)
        => (invokeResult as ValueTask?)?.AsTask();

    private static void InferGenerics(Type parameterType, Type argumentType, List<(Type ParameterType, Type Substitution)> result)
    {
        if (parameterType.IsGenericParameter && parameterType.DeclaringMethod is not null)
        {
            // We found a generic parameter. The argument type should be the substitution for it.
            result.Add((parameterType, argumentType));
            return;
        }

        if (!parameterType.ContainsGenericParameters)
        {
            // We don't have any generics.
            return;
        }

        if (parameterType.GetElementType() is { } parameterTypeElementType &&
            argumentType.GetElementType() is { } argumentTypeElementType)
        {
            // If we have arrays, we need to infer the generic types for the element types.
            // For example, if parameterType is `T[]` and argumentType is `string[]`, we need to infer that `T` is `string`.
            // So, we call InferGenerics with `T` and `string`.
            InferGenerics(parameterTypeElementType, argumentTypeElementType, result);
        }
        else if (parameterType.GenericTypeArguments.Length == argumentType.GenericTypeArguments.Length)
        {
            for (int i = 0; i < parameterType.GenericTypeArguments.Length; i++)
            {
                if (parameterType.GenericTypeArguments[i].ContainsGenericParameters)
                {
                    InferGenerics(parameterType.GenericTypeArguments[i], argumentType.GenericTypeArguments[i], result);
                }
            }
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
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2060:Call to 'System.Reflection.MethodInfo.MakeGenericMethod' can not be statically analyzed.", Justification = "Generic test methods with substituted type arguments are part of MSTest's reflection-mode adapter. Native AOT support relies on MSTest source-generated metadata, not on this code path.")]
    [UnconditionalSuppressMessage("Aot", "IL3050:Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT", Justification = "Generic test methods with substituted type arguments are part of MSTest's reflection-mode adapter. Native AOT support relies on MSTest source-generated metadata, not on this code path.")]
    private static MethodInfo ConstructGenericMethod(MethodInfo methodInfo, ParameterInfo[] parameters, object?[]? arguments)
    {
        DebugEx.Assert(methodInfo.IsGenericMethod, "ConstructGenericMethod should only be called for a generic method.");

        if (arguments is null)
        {
            // An example where this could happen is:
            // [TestMethod]
            // public void MyTestMethod<T>() { }
            throw new TestFailedException(UnitTestOutcome.Error, string.Format(CultureInfo.InvariantCulture, Resource.GenericParameterCantBeInferredBecauseNoArguments, methodInfo.Name));
        }

        Type[] genericDefinitions = methodInfo.GetGenericArguments();
        var map = new (Type GenericDefinition, Type? Substitution)[genericDefinitions.Length];
        for (int i = 0; i < map.Length; i++)
        {
            map[i] = (genericDefinitions[i], null);
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            if (!parameterType.ContainsGenericParameters || arguments[i] is null)
            {
                continue;
            }

            var result = new List<(Type ParameterType, Type Substitution)>();
            InferGenerics(parameterType, arguments[i]!/*Very strange nullability warning*/.GetType(), result);
            foreach ((Type genericParameterType, Type substitution) in result)
            {
                int mapIndexForParameter = GetMapIndexForParameterType(genericParameterType, map);
                Type? existingSubstitution = map[mapIndexForParameter].Substitution;

                if (existingSubstitution is null || substitution.IsAssignableFrom(existingSubstitution))
                {
                    map[mapIndexForParameter] = (genericParameterType, substitution);
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
            throw new TestFailedException(UnitTestOutcome.Error, e.TryGetMessage(), e.TryGetStackTraceInformation(), e);
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
