// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
internal partial class TestMethodInfo
{
    /// <summary>
    /// Creates an instance of TestClass. The TestMethod is invoked on this instance.
    /// </summary>
    /// <param name="testSynchronizationContext">The synchronization context that was current when the test started.</param>
    /// <param name="lease">The lease that owns the cleanup of the created instance, if any.</param>
    /// <returns>
    /// An instance of the TestClass.
    /// </returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private object CreateTestClassInstance(SynchronizationContext? testSynchronizationContext, out ITestClassInstanceLease? lease)
    {
        if (TestClassInstanceFactoryProvider.Current is { } factory)
        {
            lease = factory.CreateInstance(Parent.ClassType, TestContext.Context);
            return lease.Instance;
        }

        object?[]? arguments = Parent.IsParameterlessConstructor ? null : [TestContext];

        // Reflection-free fast path: use the source-generated constructor invoker when available;
        // otherwise fall back to ConstructorInfo.Invoke (reflection mode).
        Func<object?[]?, object>? sourceGeneratedInvoker = PlatformServiceProvider.Instance.ReflectionOperations.GetConstructorInvoker(Parent.ClassType);
        object instance = sourceGeneratedInvoker is not null
            ? sourceGeneratedInvoker(arguments)
            : Parent.Constructor!.Invoke(arguments);

        bool requiresCleanup = instance is IDisposable;
#if NET6_0_OR_GREATER
        requiresCleanup |= instance is IAsyncDisposable;
#endif
        lease = requiresCleanup ? new DefaultTestClassInstanceLease(instance, testSynchronizationContext) : null;
        return instance;
    }

    private sealed class DefaultTestClassInstanceLease(object instance, SynchronizationContext? testSynchronizationContext) : ITestClassInstanceLease
    {
        public object Instance { get; } = instance;

        public SynchronizationContext? TestSynchronizationContext { get; } = testSynchronizationContext;

        public bool RequiresCleanup => true;

        public async Task DisposeAsync()
        {
#if NET6_0_OR_GREATER
            if (Instance is IAsyncDisposable asyncDisposable)
            {
                // Keep Dispose() on the test's synchronization context even when DisposeAsync completes on another thread (#12561).
                await asyncDisposable.DisposeAsync().ConfigureAwait(IsRunningOnSynchronizationContext(TestSynchronizationContext));
            }
#endif
            if (Instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
