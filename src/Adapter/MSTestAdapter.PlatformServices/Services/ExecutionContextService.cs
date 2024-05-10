// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal static class ExecutionContextService
{
    /// <summary>
    /// The execution context to use by class. This context will be derived from the assembly level context and shared down
    /// to the tests of this class. The type used as key is the type of the test class and not the type of the method info.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, ExecutionContext?> ClassesExecutionContexts = new();

    /// <summary>
    /// The execution context to use for the tests (test init, method, test cleanup) of an instance. This context will be derived
    /// from the test class (if it exists) or the assembly level context. The key is the instance of the test class.
    /// </summary>
    private static readonly ConcurrentDictionary<object, ExecutionContext?> InstancesExecutionContexts = new();

    /// <summary>
    /// As we only support one assembly level context, we store it here.
    /// </summary>
    private static ExecutionContext? s_assemblyExecutionContext;

    /// <summary>
    /// When we execute the action, we need to ensure we are restoring the execution context that was captured in the logical flow of execution.
    /// After the action is executed we capture the current execution context and save it for the next action to use based on the current execution context scope.
    ///
    /// The logical flow of execution is:
    /// - AssemblyInitialize execution context is saved at "assembly level" and a copy is flown to each ClassInitialize.
    /// - ClassInitialize execution context is saved at "class level" and a copy is flown to each TestInitialize/TestMethod/TestCleanup.
    /// - TestInitialize/TestMethod/TestCleanup execution context is mutating the "instance level"
    /// - ClassCleanup reuses the "class level" execution context.
    /// - AssemblyCleanup reuses the "assembly level" execution context.
    /// </summary>
    internal static void RunActionOnContext(Action action, IExecutionContextScope executionContextScope)
    {
        if (GetOrCaptureExecutionContext(executionContextScope) is not { } executionContext)
        {
            // We don't have any execution context (that's usually the case when it is being suppressed), so we can run the action directly.
            action();
            return;
        }

        // We have an execution context, so we need to run the action in that context to ensure the flow of execution is preserved.
        ExecutionContext.Run(
            executionContext,
            _ =>
            {
                action();

                if (executionContextScope.IsCleanup)
                {
                    CleanupExecutionContext(executionContextScope);
                }
                else
                {
                    SaveExecutionContext(executionContextScope);
                }
            },
            null);
    }

    /// <summary>
    /// Capture the new state of the execution context and mutate the right variable/dictionary based on the execution context scope.
    /// </summary>
    private static void SaveExecutionContext(IExecutionContextScope executionContextScope)
    {
        var capturedContext = ExecutionContext.Capture();
        switch (executionContextScope)
        {
            case AssemblyExecutionContextScope:
                s_assemblyExecutionContext = capturedContext;
                break;

            case ClassExecutionContextScope classExecutionContextScope:
                ClassesExecutionContexts.AddOrUpdate(classExecutionContextScope.Type, _ => capturedContext, (_, _) => capturedContext);
                break;

            case InstanceExecutionContextScope instanceExecutionContextScope:
                InstancesExecutionContexts.AddOrUpdate(instanceExecutionContextScope.Instance, _ => capturedContext, (_, _) => capturedContext);
                break;
        }
    }

    private static void CleanupExecutionContext(IExecutionContextScope executionContextScope)
    {
        switch (executionContextScope)
        {
            case AssemblyExecutionContextScope:
                InstancesExecutionContexts.Clear();
                ClassesExecutionContexts.Clear();
                s_assemblyExecutionContext = null;
                break;

            case ClassExecutionContextScope classExecutionContextScope:
                _ = ClassesExecutionContexts.TryRemove(classExecutionContextScope.Type, out ExecutionContext? _);
                break;

            case InstanceExecutionContextScope instanceExecutionContextScope:
                _ = InstancesExecutionContexts.TryRemove(instanceExecutionContextScope.Instance, out ExecutionContext? _);
                break;
        }
    }

    private static ExecutionContext? GetOrCaptureExecutionContext(IExecutionContextScope executionContextScope)
        => executionContextScope switch
        {
            AssemblyExecutionContextScope => s_assemblyExecutionContext ??= ExecutionContext.Capture()?.CreateCopy(),
            ClassExecutionContextScope classExecutionContextScope => ClassesExecutionContexts.GetOrAdd(classExecutionContextScope.Type, _ => s_assemblyExecutionContext?.CreateCopy()),
            InstanceExecutionContextScope instanceExecutionContextScope => InstancesExecutionContexts.GetOrAdd(
                instanceExecutionContextScope.Instance,
                _ => ClassesExecutionContexts.TryGetValue(instanceExecutionContextScope.Type, out ExecutionContext? classExecutionContext)
                    ? classExecutionContext?.CreateCopy()
                    : s_assemblyExecutionContext?.CreateCopy()),
            _ => throw new NotSupportedException($"Unsupported execution context scope: {executionContextScope.GetType()}"),
        };
}
