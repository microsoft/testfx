// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
#endif
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// The test deployment.
/// </summary>
internal sealed class TestDeployment : ITestDeployment
{
    #region Service Utility Variables

    private readonly DeploymentItemUtility _deploymentItemUtility;
    private readonly DeploymentUtility _deploymentUtility;
    private readonly FileUtility _fileUtility;
    private MSTestAdapterSettings? _adapterSettings;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="TestDeployment"/> class.
    /// </summary>
    public TestDeployment()
        : this(new DeploymentItemUtility(ReflectHelper.Instance), new DeploymentUtility(), new FileUtility())
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
        _deploymentItemUtility = deploymentItemUtility;
        _deploymentUtility = deploymentUtility;
        _fileUtility = fileUtility;
        _adapterSettings = null;
        RunDirectories = null;
    }

    /// <summary>
    /// Gets the current run directories for this session.
    /// </summary>
    /// <remarks>
    /// This is initialized at the beginning of a run session when Deploy is called.
    /// Leaving this as a static variable since the testContext needs to be filled in with this information.
    /// </remarks>
    internal static TestRunDirectories? RunDirectories { get; private set; }

    /// <summary>
    /// The get deployment items.
    /// </summary>
    /// <param name="method"> The method. </param>
    /// <param name="type"> The type. </param>
    /// <param name="warnings"> The warnings. </param>
    /// <returns> A string of deployment items. </returns>
    public KeyValuePair<string, string>[]? GetDeploymentItems(MethodInfo method, Type type, ICollection<string> warnings) =>
        _deploymentItemUtility.GetDeploymentItems(method, _deploymentItemUtility.GetClassLevelDeploymentItems(type, warnings), warnings);

    /// <summary>
    /// Cleanup deployment item directories.
    /// </summary>
    public void Cleanup()
    {
        // Delete the deployment directory
        if (RunDirectories != null && _adapterSettings?.DeleteDeploymentDirectoryAfterTestRunIsComplete == true)
        {
            EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "Deleting deployment directory {0}", RunDirectories.RootDeploymentDirectory);

            _fileUtility.DeleteDirectories(RunDirectories.RootDeploymentDirectory);

            EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "Deleted deployment directory {0}", RunDirectories.RootDeploymentDirectory);
        }
    }

    /// <summary>
    /// Gets the deployment output directory where the source file along with all its dependencies is dropped.
    /// </summary>
    /// <returns> The deployment output directory. </returns>
    public string? GetDeploymentDirectory() =>
        RunDirectories?.OutDirectory;

    /// <summary>
    /// Deploy files related to the list of tests specified.
    /// </summary>
    /// <param name="testCases"> The tests. </param>
    /// <param name="runContext"> The run context. </param>
    /// <param name="frameworkHandle"> The framework handle. </param>
    /// <returns> Return true if deployment is done. </returns>
    public bool Deploy(IEnumerable<TestCase> testCases, IRunContext? runContext, IFrameworkHandle frameworkHandle)
    {
        DebugEx.Assert(testCases != null, "tests");

        // Reset runDirectories before doing deployment, so that older values of runDirectories is not picked
        // even if test host is kept alive.
        RunDirectories = null;

        _adapterSettings = MSTestSettingsProvider.Settings;
        bool canDeploy = CanDeploy();
        bool hasDeploymentItems = testCases.Any(DeploymentItemUtility.HasDeploymentItems);

        // deployment directories should not be created in this case,simply return
        if (!canDeploy && hasDeploymentItems)
        {
            return false;
        }

        string? firstTestSource = testCases.FirstOrDefault()?.Source;
        RunDirectories = _deploymentUtility.CreateDeploymentDirectories(runContext, firstTestSource);

        // Deployment directories are created but deployment will not happen.
        // This is added just to keep consistency with MSTest v1 behavior.
        if (!hasDeploymentItems)
        {
            return false;
        }

        // Object model currently does not have support for SuspendCodeCoverage. We can remove this once support is added
#if NETFRAMEWORK
        using (new SuspendCodeCoverage())
#endif
        {
            // Group the tests by source
            var testsBySource = from test in testCases
                                group test by test.Source into testGroup
                                select new { Source = testGroup.Key, Tests = testGroup };

            TestRunDirectories runDirectories = RunDirectories;
            foreach (var group in testsBySource)
            {
                // do the deployment
                _deploymentUtility.Deploy(@group.Tests, @group.Source, runContext, frameworkHandle, RunDirectories);
            }

            // Update the runDirectories
            RunDirectories = runDirectories;
        }

        return true;
    }

    internal static IDictionary<string, object> GetDeploymentInformation(string? source)
    {
        var properties = new Dictionary<string, object>(capacity: 8);

        string applicationBaseDirectory = string.Empty;

        // Run directories can be null in win8.
        if (RunDirectories == null && !StringEx.IsNullOrEmpty(source))
        {
            // applicationBaseDirectory is set at source level
            applicationBaseDirectory = Path.GetDirectoryName(source)!;
        }

        properties[TestContext.TestRunDirectoryLabel] = RunDirectories?.RootDeploymentDirectory ?? applicationBaseDirectory;
        properties[TestContext.DeploymentDirectoryLabel] = RunDirectories?.OutDirectory ?? applicationBaseDirectory;
        properties[TestContext.ResultsDirectoryLabel] = RunDirectories?.InDirectory ?? applicationBaseDirectory;
        properties[TestContext.TestRunResultsDirectoryLabel] = RunDirectories?.InMachineNameDirectory ?? applicationBaseDirectory;
        properties[TestContext.TestResultsDirectoryLabel] = RunDirectories?.InDirectory ?? applicationBaseDirectory;
        return properties;
    }

    /// <summary>
    /// Reset the static variable to default values. Used only for testing purposes.
    /// </summary>
    internal static void Reset() => RunDirectories = null;

    /// <summary>
    /// Returns whether deployment can happen or not.
    /// </summary>
    /// <returns>True if deployment can be done.</returns>
    private bool CanDeploy()
    {
        DebugEx.Assert(_adapterSettings is not null, "Adapter settings should not be null.");
        if (!_adapterSettings.DeploymentEnabled)
        {
            EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "MSTestExecutor: CanDeploy is false.");
            return false;
        }

        return true;
    }
}
#endif
