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
    /// <returns>
    /// An instance of the TestClass.
    /// </returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private object CreateTestClassInstance(out ITestClassInstanceLease? lease)
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
        lease = requiresCleanup ? new DefaultTestClassInstanceLease(instance) : null;
        return instance;
    }

    private sealed class DefaultTestClassInstanceLease(object instance) : ITestClassInstanceLease
    {
        public object Instance { get; } = instance;

        public bool RequiresCleanup => true;

        public async Task DisposeAsync()
        {
#if NET6_0_OR_GREATER
            if (Instance is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
#endif
            if (Instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
