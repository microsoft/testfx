// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System;
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// Constants used throughout.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// Uri of the MSTest executor.
        /// </summary>
        internal const string ExecutorUriString = "executor://MSTestAdapter/v2";

        /// <summary>
        /// The name of test run parameters node in the runsettings.
        /// </summary>
        internal const string TestRunParametersName = "TestRunParameters";

        /// <summary>
        /// The executor uri for this adapter.
        /// </summary>
        internal static readonly Uri ExecutorUri = new Uri(ExecutorUriString);

        #region Test Property registration

        internal static readonly TestProperty TestClassNameProperty = TestProperty.Register("MSTestDiscoverer.TestClassName", TestClassNameLabel, typeof(string), TestPropertyAttributes.Hidden, typeof(TestCase));

        internal static readonly TestProperty AsyncTestProperty = TestProperty.Register("MSTestDiscoverer.IsAsync", IsAsyncLabel, typeof(bool), TestPropertyAttributes.Hidden, typeof(TestCase));

#pragma warning disable CS0618 // Type or member is obsolete
        internal static readonly TestProperty TestCategoryProperty = TestProperty.Register("MSTestDiscoverer.TestCategory", TestCategoryLabel, typeof(string[]), TestPropertyAttributes.Hidden | TestPropertyAttributes.Trait, typeof(TestCase));
#pragma warning restore CS0618 // Type or member is obsolete

        internal static readonly TestProperty PriorityProperty = TestProperty.Register("MSTestDiscoverer.Priority", PriorityLabel, typeof(int), TestPropertyAttributes.Hidden, typeof(TestCase));

        internal static readonly TestProperty DeploymentItemsProperty = TestProperty.Register("MSTestDiscoverer.DeploymentItems", DeploymentItemsLabel, typeof(KeyValuePair<string, string>[]), TestPropertyAttributes.Hidden, typeof(TestCase));

        internal static readonly TestProperty DoNotParallelizeProperty = TestProperty.Register("MSTestDiscovererv2.DoNotParallelize", DoNotParallelizeLabel, typeof(bool), TestPropertyAttributes.Hidden, typeof(TestCase));

        #endregion

        #region Private Constants

        /// <summary>
        /// These are the Test properties used by the adapter, which essentially correspond
        /// to attributes on tests, and may be available in command line/TeamBuild to filter tests.
        /// These Property names should not be localized.
        /// </summary>
        private const string TestClassNameLabel = "ClassName";
        private const string IsAsyncLabel = "IsAsync";
        private const string TestCategoryLabel = "TestCategory";
        private const string PriorityLabel = "Priority";
        private const string DeploymentItemsLabel = "DeploymentItems";
        private const string DoNotParallelizeLabel = "DoNotParallelize";

        #endregion
    }
}
