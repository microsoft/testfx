// Copyright (c) Microsoft. All rights reserved.

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

        internal static TestProperty TestEnabledProperty = TestProperty.Register("MSTestDiscovererv2.IsEnabled", IsEnabledLabel, typeof(bool), TestPropertyAttributes.Hidden, typeof(TestCase));
        
        internal static TestProperty TestClassNameProperty = TestProperty.Register("MSTestDiscovererv2.TestClassName", TestClassNameLabel, typeof(string), TestPropertyAttributes.Hidden, typeof(TestCase));
        
        internal static TestProperty AsyncTestProperty = TestProperty.Register("MSTestDiscovererv2.IsAsync", IsAsyncLabel, typeof(bool), TestPropertyAttributes.Hidden, typeof(TestCase));

#pragma warning disable CS0618 // Type or member is obsolete
        internal static TestProperty TestCategoryProperty = TestProperty.Register("MSTestDiscovererv2.TestCategory", TestCategoryLabel, typeof(string[]), TestPropertyAttributes.Hidden | TestPropertyAttributes.Trait, typeof(TestCase));
#pragma warning restore CS0618 // Type or member is obsolete

        internal static TestProperty PriorityProperty = TestProperty.Register("MSTestDiscovererv2.Priority", PriorityLabel, typeof(int), TestPropertyAttributes.Hidden, typeof(TestCase));
        
        internal static TestProperty DeploymentItemsProperty = TestProperty.Register("MSTestDiscoverer2.DeploymentItems", DeploymentItemsLabel, typeof(KeyValuePair<string, string>[]), TestPropertyAttributes.Hidden, typeof(TestCase));

        #endregion

        #region Private Constants

        /// <summary>
        /// These are the Test properties used by the adapter, which essentially correspond
        /// to attributes on tests, and may be available in command line/TeamBuild to filter tests.
        /// These Property names should not be localized.
        /// </summary>
        private const string IsEnabledLabel = "IsEnabled";
        private const string TestClassNameLabel = "ClassName";
        private const string IsAsyncLabel = "IsAsync";
        private const string TestCategoryLabel = "TestCategory";
        private const string PriorityLabel = "Priority";
        private const string DeploymentItemsLabel = "DeploymentItems";

        #endregion
    }
}
