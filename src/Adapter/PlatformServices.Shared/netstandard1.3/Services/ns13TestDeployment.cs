// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    /// <summary>
    /// The test deployment.
    /// </summary>
    public class TestDeployment : ITestDeployment
    {
        #region Service Utility Variables

        private DeploymentItemUtility deploymentItemUtility;
        private DeploymentUtility deploymentUtility;
        private FileUtility fileUtility;
        private MSTestAdapterSettings adapterSettings;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="TestDeployment"/> class.
        /// </summary>
        public TestDeployment()
            : this(new DeploymentItemUtility(new ReflectionUtility()), new DeploymentUtility(), new FileUtility())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestDeployment"/> class. Used for unit tests.
        /// </summary>
        /// <param name="deploymentItemUtility"> The deployment item utility. </param>
        /// <param name="deploymentUtility"> The deployment utility. </param>
        /// <param name="fileUtility"> The file utility. </param>
        internal TestDeployment(DeploymentItemUtility deploymentItemUtility, DeploymentUtility deploymentUtility, FileUtility fileUtility)
        {
            this.deploymentItemUtility = deploymentItemUtility;
            this.deploymentUtility = deploymentUtility;
            this.fileUtility = fileUtility;
            this.adapterSettings = null;
            RunDirectories = null;
        }

        /// <summary>
        /// Gets the current run directories for this session.
        /// </summary>
        /// <remarks>
        /// This is initialized at the beginning of a run session when Deploy is called.
        /// Leaving this as a static variable since the testContext needs to be filled in with this information.
        /// </remarks>
        internal static TestRunDirectories RunDirectories
        {
            get;
            private set;
        }

        /// <summary>
        /// The get deployment items.
        /// </summary>
        /// <param name="method"> The method. </param>
        /// <param name="type"> The type. </param>
        /// <param name="warnings"> The warnings. </param>
        /// <returns> A string of deployment items. </returns>
        public KeyValuePair<string, string>[] GetDeploymentItems(MethodInfo method, Type type, ICollection<string> warnings)
        {
            return this.deploymentItemUtility.GetDeploymentItems(method, this.deploymentItemUtility.GetClassLevelDeploymentItems(type, warnings), warnings);
        }

        /// <summary>
        /// The cleanup.
        /// </summary>
        public void Cleanup()
        {
            // Delete the deployment directory
            if (RunDirectories != null && this.adapterSettings.DeleteDeploymentDirectoryAfterTestRunIsComplete)
            {
                EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "Deleting deployment directory {0}", RunDirectories.RootDeploymentDirectory);

                this.fileUtility.DeleteDirectories(RunDirectories.RootDeploymentDirectory);

                EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "Deleted deployment directory {0}", RunDirectories.RootDeploymentDirectory);
            }
        }

        /// <summary>
        /// Gets the deployment output directory where the source file along with all its dependencies is dropped.
        /// </summary>
        /// <returns> The deployment output directory. </returns>
        public string GetDeploymentDirectory()
        {
            return RunDirectories?.OutDirectory;
        }

        /// <summary>
        /// Deploy files related to the list of tests specified.
        /// </summary>
        /// <param name="tests"> The tests. </param>
        /// <param name="runContext"> The run context. </param>
        /// <param name="frameworkHandle"> The framework handle. </param>
        /// <returns> Return true if deployment is done. </returns>
        public bool Deploy(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            Debug.Assert(tests != null, "tests");

            // Reset runDirectories before doing deployment, so that older values of runDirectories is not picked
            // even if test host is kept alive.
            RunDirectories = null;

            this.adapterSettings = MSTestSettingsProvider.Settings;
            bool canDeploy = this.CanDeploy();
            var hasDeploymentItems = tests.Any(test => this.deploymentItemUtility.HasDeploymentItems(test));

            // deployment directories should not be created in this case,simply return
            if (!canDeploy && hasDeploymentItems)
            {
                return false;
            }

            RunDirectories = this.deploymentUtility.CreateDeploymentDirectories(runContext);

            // Deployment directories are created but deployment will not happen.
            // This is added just to keep consistency with MSTestv1 behavior.
            if (!hasDeploymentItems)
            {
                return false;
            }

            // Object model currently does not have support for SuspendCodeCoverage. We can remove this once support is added
#if !NETSTANDARD1_5
            using (new SuspendCodeCoverage())
#endif
            {
                // Group the tests by source
                var testsBySource = from test in tests
                                    group test by test.Source into testGroup
                                    select new { Source = testGroup.Key, Tests = testGroup };

                var runDirectories = RunDirectories;
                foreach (var group in testsBySource)
                {
                    // do the deployment
                    this.deploymentUtility.Deploy(@group.Tests, @group.Source, runContext, frameworkHandle, RunDirectories);
                }

                // Update the runDirectories
                RunDirectories = runDirectories;
            }

            return true;
        }

        internal static IDictionary<string, object> GetDeploymentInformation(string source)
        {
            var properties = new Dictionary<string, object>();

            var applicationBaseDirectory = string.Empty;

            // Run directories can be null in win8.
            if (RunDirectories == null && !string.IsNullOrEmpty(source))
            {
                // applicationBaseDirectory is set at source level
                applicationBaseDirectory = Path.GetDirectoryName(source);
            }

            properties[TestContextPropertyStrings.TestRunDirectory] = RunDirectories != null
                                                                          ? RunDirectories.RootDeploymentDirectory
                                                                          : applicationBaseDirectory;
            properties[TestContextPropertyStrings.DeploymentDirectory] = RunDirectories != null
                                                                             ? RunDirectories.OutDirectory
                                                                             : applicationBaseDirectory;
            properties[TestContextPropertyStrings.ResultsDirectory] = RunDirectories != null
                                                                          ? RunDirectories.InDirectory
                                                                          : applicationBaseDirectory;
            properties[TestContextPropertyStrings.TestRunResultsDirectory] = RunDirectories != null
                                                                                 ? RunDirectories.InMachineNameDirectory
                                                                                 : applicationBaseDirectory;
            properties[TestContextPropertyStrings.TestResultsDirectory] = RunDirectories != null
                                                                              ? RunDirectories.InDirectory
                                                                              : applicationBaseDirectory;
            properties[TestContextPropertyStrings.TestDir] = RunDirectories != null
                                                                 ? RunDirectories.RootDeploymentDirectory
                                                                 : applicationBaseDirectory;
            properties[TestContextPropertyStrings.TestDeploymentDir] = RunDirectories != null
                                                                           ? RunDirectories.OutDirectory
                                                                           : applicationBaseDirectory;
            properties[TestContextPropertyStrings.TestLogsDir] = RunDirectories != null
                                                                     ? RunDirectories.InMachineNameDirectory
                                                                     : applicationBaseDirectory;

            return properties;
        }

        /// <summary>
        /// Reset the static variable to default values. Used only for testing purposes.
        /// </summary>
        internal static void Reset()
        {
            RunDirectories = null;
        }

        /// <summary>
        /// Returns whether deployment can happen or not
        /// </summary>
        /// <returns>True if deployment can be done.</returns>
        private bool CanDeploy()
        {
            if (!this.adapterSettings.DeploymentEnabled)
            {
                EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "MSTestExecutor: CanDeploy is false.");
                return false;
            }

            return true;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
