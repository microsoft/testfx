// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal static class TestClassInstanceFactoryProvider
{
    private static readonly AsyncLocal<ITestClassInstanceFactory?> Factory = new();

    internal static ITestClassInstanceFactory? Current => Factory.Value;

    internal static IDisposable Push(ITestClassInstanceFactory? factory)
    {
        ITestClassInstanceFactory? previousFactory = Factory.Value;
        Factory.Value = factory;
        return new Scope(previousFactory);
    }

    private sealed class Scope(ITestClassInstanceFactory? previousFactory) : IDisposable
    {
        public void Dispose()
            => Factory.Value = previousFactory;
    }
}
