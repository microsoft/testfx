// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Interface.ObjectModel;
    using UTF=TestTools.UnitTesting;

    public class TestDataSource : ITestDataSource
    {

        public bool HasDataDrivenTests(UTF.ITestMethod testMethodInfo)
        {
            return false;
        }

        public void ResetContext()
        {
            return;
        }

        public UTF.TestResult[] RunDataDrivenTest(UTF.TestContext testContext, UTF.ITestMethod testMethodInfo, ITestMethod testMethod, UTF.TestMethodAttribute executor)
        {
            return null; 
        }

        public bool SetContext(string source)
        {
            return false;
        }

    }
}
