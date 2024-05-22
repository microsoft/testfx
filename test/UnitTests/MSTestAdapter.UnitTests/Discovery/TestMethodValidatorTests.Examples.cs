// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class InvalidTestMethods
{
    public void MethodWithoutTestAttribute()
    {
    }

    [TestMethod]
    public void GenericTestMethod<T>()
    {
    }

    [TestMethod]
    // By default this method won't be detected, but will
    // be detected if you enable discover internals.
    internal void InternalTestMethod()
    {
    }

    // Make the name accessible, so we don't have to use magic strings that are hard to refactor.
    public static string PrivateTestMethodName { get; } = nameof(PrivateTestMethod);

    [TestMethod]
    private void PrivateTestMethod()
    {
    }

    [TestMethod]
    public static void StaticTestMethod()
    {
    }

    [TestMethod]
#pragma warning disable VSTHRD100 // Avoid async void methods
    public async void AsyncTestMethodWithVoidReturnType() =>
        await Task.CompletedTask;
#pragma warning restore VSTHRD100 // Avoid async void methods

    [TestMethod]
    public int TestMethodWithIntReturnType() => 0;
}

public class ValidTestMethods()
{
    [TestMethod]
    public async Task AsyncTestMethodWithTaskReturnType() =>
        await Task.CompletedTask;

    [TestMethod]
    public Task SyncTestMethodWithTaskReturnType() =>
        Task.CompletedTask;

    [TestMethod]
    public void TestMethodWithVoidReturnType()
    {
    }
}

public abstract class InvalidAbstractTestMethods
{
    [TestMethod]
    public abstract void AbstractTestMethod();
}
