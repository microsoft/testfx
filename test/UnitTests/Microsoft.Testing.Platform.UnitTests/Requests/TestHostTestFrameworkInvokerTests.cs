// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Requests;

namespace Microsoft.Testing.Platform.UnitTests.Requests;

[TestClass]
public sealed class TestHostTestFrameworkInvokerTests
{
    private const string CleanupOperation = "TestExecutionScopeCleanupException";

    [TestMethod]
    public void AddSecondaryExceptionOrThrow_WithWritableData_AttachesCleanupException()
    {
        var primaryException = new InvalidOperationException("framework failure");
        var cleanupException = new InvalidOperationException("cleanup failure");

        InvokeAddSecondaryExceptionOrThrow(primaryException, cleanupException);

        Assert.AreSame(cleanupException, primaryException.Data[CleanupOperation]);
    }

    [TestMethod]
    public void AddSecondaryExceptionOrThrow_WithUnavailableData_PreservesBothExceptions()
    {
        var primaryException = new ThrowingDataException("framework failure");
        var cleanupException = new InvalidOperationException("cleanup failure");

        AggregateException exception = Assert.ThrowsExactly<AggregateException>(
            () => InvokeAddSecondaryExceptionOrThrow(primaryException, cleanupException));

        Assert.HasCount(2, exception.InnerExceptions);
        Assert.AreSame(primaryException, exception.InnerExceptions[0]);
        Assert.AreSame(cleanupException, exception.InnerExceptions[1]);
    }

    private static void InvokeAddSecondaryExceptionOrThrow(Exception primaryException, Exception cleanupException)
    {
        MethodInfo method = typeof(TestHostTestFrameworkInvoker).GetMethod(
            "AddSecondaryExceptionOrThrow",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var action = (Action<Exception, string, Exception>)method.CreateDelegate(
            typeof(Action<Exception, string, Exception>));
        action(primaryException, CleanupOperation, cleanupException);
    }

    private sealed class ThrowingDataException(string message) : Exception(message)
    {
        public override IDictionary Data => throw new NotSupportedException("Exception data is unavailable.");
    }
}
