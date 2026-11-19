// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal sealed partial class TestContextImplementation
{
    internal readonly struct ScopedTestContextSetter : IDisposable
    {
        private readonly LiveOutputScope _liveOutputScope;

        internal ScopedTestContextSetter(TestContext? testContext)
        {
            TestContext.Current = testContext;
            _liveOutputScope = new(testContext);
            CurrentLiveOutputScope.Value = _liveOutputScope;
        }

        public void Dispose()
        {
            _liveOutputScope.Deactivate();
            TestContext.Current = null;
            CurrentLiveOutputScope.Value = null;
        }
    }

    internal static ScopedTestContextSetter SetCurrentTestContext(TestContext? testContext)
        => new(testContext);
}
