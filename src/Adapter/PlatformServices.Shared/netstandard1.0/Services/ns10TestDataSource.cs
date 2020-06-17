// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// The platform service that provides values from data source when data driven tests are run.
    /// </summary>
    /// <remarks>
    /// NOTE NOTE NOTE: This platform service refers to the inbox UTF extension assembly for UTF.TestContext which can only be loadable inside of the app domain that discovers/runs
    /// the tests since it can only be found at the test output directory. DO NOT call into this platform service outside of the appdomain context if you do not want to hit
    /// a ReflectionTypeLoadException.
    /// </remarks>
    public class TestDataSource : ITestDataSource
    {
        IEnumerable<object> ITestDataSource.GetData(UTF.ITestMethod testMethodInfo, ITestContext testContext)
        {
            return null;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
