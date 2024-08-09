// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

internal abstract class DeploymentUtilityBase
{
    protected const string TestAssemblyConfigFileExtension = ".config";
    protected const string NetAppConfigFile = "App.Config";

    /// <summary>
    /// Prefix for deployment folder to avoid confusions with other folders (like trx attachments).
    /// </summary>
    protected const string DeploymentFolderPrefix = "Deploy";

    public DeploymentUtilityBase()
        : this(new DeploymentItemUtility(new ReflectionUtility()), new AssemblyUtility(), new FileUtility())
    {
    }

    public DeploymentUtilityBase(DeploymentItemUtility deploymentItemUtility, AssemblyUtility assemblyUtility, FileUtility fileUtility)
    {
        DeploymentItemUtility = deploymentItemUtility;
        AssemblyUtility = assemblyUtility;
        FileUtility = fileUtility;
    }

    protected FileUtility FileUtility { get; set; }

    protected DeploymentItemUtility DeploymentItemUtility { get; set; }

    protected AssemblyUtility AssemblyUtility { get; set; }

    public bool Deploy(IEnumerable<TestCase> tests, string source, IRunContext? runContext, ITestExecutionRecorder testExecutionRecorder, TestRunDirectories runDirectories)
    {
        IList<DeploymentItem> deploymentItems = DeploymentItemUtility.GetDeploymentItems(tests);

        // we just deploy source if there are no deployment items for current source but there are deployment items for other sources
        return Deploy(source, runContext, testExecutionRecorder, deploymentItems, runDirectories);
    }

    /// <summary>
    /// Create deployment directories.
    /// </summary>
    /// <param name="runContext">The run context.</param>
    /// <returns>TestRunDirectories instance.</returns>
    public TestRunDirectories CreateDeploymentDirectories(IRunContext? runContext)
    {
        string resultsDirectory = GetTestResultsDirectory(runContext);
        string rootDeploymentDirectory = GetRootDeploymentDirectory(resultsDirectory);

        var result = new TestRunDirectories(rootDeploymentDirectory);

        FileUtility.CreateDirectoryIfNotExists(rootDeploymentDirectory);
        FileUtility.CreateDirectoryIfNotExists(result.InDirectory);
        FileUtility.CreateDirectoryIfNotExists(result.OutDirectory);
        FileUtility.CreateDirectoryIfNotExists(result.InMachineNameDirectory);

        return result;
    }

    /// <summary>
    /// add deployment items based on MSTestSettingsProvider.Settings.DeployTestSourceDependencies. This property is ignored in net core.
    /// </summary>
    /// <param name="testSource">The test source.</param>
    /// <param name="deploymentItems">Deployment Items.</param>
    /// <param name="warnings">Warnings.</param>
    public abstract void AddDeploymentItemsBasedOnMsTestSetting(string testSource, IList<DeploymentItem> deploymentItems, List<string> warnings);

    /// <summary>
    /// Get the parent test results directory where deployment will be done.
    /// </summary>
    /// <param name="runContext">The run context.</param>
    /// <returns>The test results directory.</returns>
    public static string GetTestResultsDirectory(IRunContext? runContext) => !StringEx.IsNullOrEmpty(runContext?.TestRunDirectory)
            ? runContext.TestRunDirectory
            : Path.GetFullPath(Path.Combine(Path.GetTempPath(), TestRunDirectories.DefaultDeploymentRootDirectory));

    /// <summary>
    /// Get root deployment directory.
    /// </summary>
    /// <param name="baseDirectory">The base directory.</param>
    /// <returns>Root deployment directory.</returns>
    public abstract string GetRootDeploymentDirectory(string baseDirectory);

    internal string? GetConfigFile(string testSource)
    {
        string? configFile = null;

        string assemblyConfigFile = testSource + TestAssemblyConfigFileExtension;
        if (FileUtility.DoesFileExist(assemblyConfigFile))
        {
            // Path to config file cannot be bad: storage is already checked, and extension is valid.
            configFile = testSource + TestAssemblyConfigFileExtension;
        }
        else
        {
            string netAppConfigFile = Path.Combine(Path.GetDirectoryName(testSource)!, NetAppConfigFile);
            if (FileUtility.DoesFileExist(netAppConfigFile))
            {
                configFile = netAppConfigFile;
            }
        }

        return configFile;
    }

    /// <summary>
    /// Does the deployment of parameter deployment items and the testSource to the parameter directory.
    /// </summary>
    /// <param name="deploymentItems">The deployment item.</param>
    /// <param name="testSource">The test source.</param>
    /// <param name="deploymentDirectory">The deployment directory.</param>
    /// <param name="resultsDirectory">Root results directory.</param>
    /// <returns>Returns a list of deployment warnings.</returns>
    protected IEnumerable<string> Deploy(IList<DeploymentItem> deploymentItems, string testSource, string deploymentDirectory, string resultsDirectory)
    {
        Validate.IsFalse(StringEx.IsNullOrWhiteSpace(deploymentDirectory), "Deployment directory is null or empty");
        Validate.IsTrue(FileUtility.DoesDirectoryExist(deploymentDirectory), $"Deployment directory {deploymentDirectory} does not exist");
        Validate.IsFalse(StringEx.IsNullOrWhiteSpace(testSource), "TestSource directory is null/empty");
        Validate.IsTrue(FileUtility.DoesFileExist(testSource), $"TestSource {testSource} does not exist.");

        testSource = Path.GetFullPath(testSource);
        var warnings = new List<string>();

        AddDeploymentItemsBasedOnMsTestSetting(testSource, deploymentItems, warnings);

        // Maps relative to Out dir destination -> source and used to determine if there are conflicted items.
        var destToSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Copy the deployment items. (As deployment item can correspond to directories as well, so each deployment item may map to n files)
        foreach (DeploymentItem deploymentItem in deploymentItems)
        {
            ValidateArg.NotNull(deploymentItem, "deploymentItem should not be null.");

            // Validate the output directory.
            if (!IsOutputDirectoryValid(deploymentItem, deploymentDirectory, warnings))
            {
                continue;
            }

            // Get the files corresponding to this deployment item
            List<string>? deploymentItemFiles = GetFullPathToFilesCorrespondingToDeploymentItem(deploymentItem, testSource, resultsDirectory, warnings, out bool itemIsDirectory);
            if (deploymentItemFiles == null)
            {
                continue;
            }

            string fullPathToDeploymentItemSource = GetFullPathToDeploymentItemSource(deploymentItem.SourcePath, testSource);

            // Note: source is already rooted.
            foreach (string deploymentItemFile in deploymentItemFiles)
            {
                DebugEx.Assert(Path.IsPathRooted(deploymentItemFile), "File " + deploymentItemFile + " is not rooted");

                // List of files to deploy, by default, just itemFile.
                var filesToDeploy = new List<string>(1)
                {
                    deploymentItemFile,
                };

                // Find dependencies of test deployment items and deploy them at the same time as the main file.
                if (deploymentItem.OriginType == DeploymentItemOriginType.PerTestDeployment &&
                    AssemblyUtility.IsAssemblyExtension(Path.GetExtension(deploymentItemFile)))
                {
                    AddDependenciesOfDeploymentItem(deploymentItemFile, filesToDeploy, warnings);
                }

                foreach (string fileToDeploy in filesToDeploy)
                {
                    DebugEx.Assert(Path.IsPathRooted(fileToDeploy), $"File {fileToDeploy} is not rooted");

                    // Ignore the test platform files.
                    string tempFile = Path.GetFileName(fileToDeploy);
                    string assemblyName = Path.GetFileName(GetType().Assembly.Location);
                    if (tempFile.Equals(assemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string relativeDestination;
                    if (itemIsDirectory)
                    {
                        // Deploy into subdirectory of deployment (Out) dir.
                        DebugEx.Assert(fileToDeploy.StartsWith(fullPathToDeploymentItemSource, StringComparison.Ordinal), "Somehow source is outside original dir.");
                        relativeDestination = FileUtility.TryConvertPathToRelative(fileToDeploy, fullPathToDeploymentItemSource);
                    }
                    else
                    {
                        // Deploy just to the deployment (Out) dir.
                        relativeDestination = Path.GetFileName(fileToDeploy);
                    }

                    relativeDestination = Path.Combine(deploymentItem.RelativeOutputDirectory, relativeDestination);  // Ignores empty arg1.
                    string destination = Path.Combine(deploymentDirectory, relativeDestination);
                    try
                    {
                        destination = Path.GetFullPath(destination);
                    }
                    catch (Exception e)
                    {
                        string warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorFailedToAccessFile, destination, e.GetType(), e.Message);
                        warnings.Add(warning);

                        continue;
                    }

                    if (!destToSource.TryGetValue(relativeDestination, out string? value))
                    {
                        destToSource.Add(relativeDestination, fileToDeploy);

                        // Now, finally we can copy the file...
                        destination = FileUtility.CopyFileOverwrite(fileToDeploy, destination, out string? warning);
                        if (!StringEx.IsNullOrEmpty(warning))
                        {
                            warnings.Add(warning);
                        }

                        if (StringEx.IsNullOrEmpty(destination))
                        {
                            continue;
                        }

                        // We clear the attributes so that e.g. you can write to the copies of files originally under SCC.
                        FileUtility.SetAttributes(destination, FileAttributes.Normal);

                        // Deploy PDB for line number info in stack trace.
                        FileUtility.FindAndDeployPdb(destination, relativeDestination, fileToDeploy, destToSource);
                    }
                    else if (!string.Equals(fileToDeploy, value, StringComparison.OrdinalIgnoreCase))
                    {
                        EqtTrace.WarningIf(
                            EqtTrace.IsWarningEnabled,
                            "Conflict during copying file: '{0}' and '{1}' are from different origins although they might be the same.",
                            fileToDeploy,
                            value);
                    }
                } // foreach fileToDeploy.
            } // foreach itemFile.
        }

        return warnings;
    }

    // Find dependencies of test deployment items
    protected abstract void AddDependenciesOfDeploymentItem(string deploymentItemFile, IList<string> filesToDeploy, IList<string> warnings);

    /// <summary>
    /// Get files corresponding to parameter deployment item.
    /// </summary>
    /// <param name="deploymentItem">Deployment Item.</param>
    /// <param name="testSource">The test source.</param>
    /// <param name="resultsDirectory">Results directory which should be skipped for deployment.</param>
    /// <param name="warnings">Warnings.</param>
    /// <param name="isDirectory">Is this a directory.</param>
    /// <returns>Paths to items to deploy.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    protected List<string>? GetFullPathToFilesCorrespondingToDeploymentItem(DeploymentItem deploymentItem, string testSource, string resultsDirectory, IList<string> warnings, out bool isDirectory)
    {
        DebugEx.Assert(deploymentItem != null, "deploymentItem should not be null.");
        DebugEx.Assert(!StringEx.IsNullOrEmpty(testSource), "testSource should not be null or empty.");

        try
        {
            isDirectory = IsDeploymentItemSourceADirectory(deploymentItem, testSource, out string? directory);
            if (isDirectory)
            {
                return FileUtility.AddFilesFromDirectory(
                    directory!,
                    (deployDirectory) => string.Equals(deployDirectory, resultsDirectory, StringComparison.OrdinalIgnoreCase), false);
            }

            if (IsDeploymentItemSourceAFile(deploymentItem.SourcePath, testSource, out string fileName))
            {
                return [fileName];
            }

            // If file/directory is not found, then try removing the prefix and see if it is present.
            string fileOrDirNameOnly = Path.GetFileName(deploymentItem.SourcePath);
            if (IsDeploymentItemSourceAFile(fileOrDirNameOnly, testSource, out fileName))
            {
                return [fileName];
            }

            string message = string.Format(CultureInfo.CurrentCulture, Resource.CannotFindFile, fileName);
            throw new FileNotFoundException(message, fileName);
        }
        catch (Exception e)
        {
            warnings.Add(string.Format(
                CultureInfo.CurrentCulture, Resource.DeploymentErrorFailedToGetFileForDeploymentItem, deploymentItem, e.GetType(), e.Message));
        }

        isDirectory = false;
        return null;
    }

    protected static string GetFullPathToDeploymentItemSource(string deploymentItemSourcePath, string testSource) => Path.IsPathRooted(deploymentItemSourcePath)
            ? deploymentItemSourcePath
            : Path.Combine(Path.GetDirectoryName(testSource)!, deploymentItemSourcePath);

    /// <summary>
    /// Validate the output directory for the parameter deployment item.
    /// </summary>
    /// <param name="deploymentItem">The deployment item.</param>
    /// <param name="deploymentDirectory">The deployment directory.</param>
    /// <param name="warnings">Warnings.</param>
    /// <returns>True if valid.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    protected static bool IsOutputDirectoryValid(DeploymentItem deploymentItem, string deploymentDirectory, IList<string> warnings)
    {
        DebugEx.Assert(deploymentItem != null, "deploymentItem should not be null.");
        DebugEx.Assert(!StringEx.IsNullOrEmpty(deploymentDirectory), "deploymentDirectory should not be null or empty.");
        DebugEx.Assert(warnings != null, "warnings should not be null.");

        // Check that item.output dir does not go outside deployment Out dir, otherwise you can erase any file!
        string outputDir = deploymentDirectory;
        try
        {
            outputDir = Path.GetFullPath(Path.Combine(deploymentDirectory, deploymentItem.RelativeOutputDirectory));

            // convert the short path to full length path (like joe~1.dom to joe.domain) and the comparison
            // startsWith in the next loop will work for the matching paths.
            deploymentDirectory = Path.GetFullPath(deploymentDirectory);
        }
        catch (Exception e)
        {
            string warning = string.Format(
                CultureInfo.CurrentCulture,
                Resource.DeploymentErrorFailedToAccesOutputDirectory,
                deploymentItem.SourcePath,
                outputDir,
                e.GetType(),
                e.GetExceptionMessage());

            warnings.Add(warning);
            return false;
        }

        if (!outputDir.StartsWith(deploymentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            string warning = string.Format(
                CultureInfo.CurrentCulture,
                Resource.DeploymentErrorBadDeploymentItem,
                deploymentItem.SourcePath,
                deploymentItem.RelativeOutputDirectory);
            warnings.Add(warning);

            return false;
        }

        return true;
    }

    protected string? AddTestSourceConfigFileIfExists(string testSource, IList<DeploymentItem> deploymentItems)
    {
        string? configFile = GetConfigFile(testSource);

        if (!StringEx.IsNullOrEmpty(configFile))
        {
            DeploymentItemUtility.AddDeploymentItem(deploymentItems, new DeploymentItem(configFile));
        }

        return configFile;
    }

    /// <summary>
    /// Log the parameter warnings on the parameter logger.
    /// </summary>
    /// <param name="testExecutionRecorder">Execution recorder.</param>
    /// <param name="warnings">Warnings.</param>
    private static void LogWarnings(ITestExecutionRecorder testExecutionRecorder, IEnumerable<string> warnings)
    {
        if (warnings == null)
        {
            return;
        }

        DebugEx.Assert(testExecutionRecorder != null, "Logger should not be null");

        // log the warnings
        foreach (string warning in warnings)
        {
            testExecutionRecorder.SendMessage(TestMessageLevel.Warning, warning);
        }
    }

    private bool Deploy(string source, IRunContext? runContext, ITestExecutionRecorder testExecutionRecorder, IList<DeploymentItem> deploymentItems, TestRunDirectories runDirectories)
    {
        ValidateArg.NotNull(runDirectories, "runDirectories");
        if (EqtTrace.IsInfoEnabled)
        {
            EqtTrace.Info("MSTestExecutor: Found that deployment items for source {0} are: ", source);
            foreach (DeploymentItem item in deploymentItems)
            {
                EqtTrace.Info("MSTestExecutor: SourcePath: - {0}", item.SourcePath);
            }
        }

        // Do the deployment.
        EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "MSTestExecutor: Using deployment directory {0} for source {1}.", runDirectories.OutDirectory, source);
        IEnumerable<string> warnings = Deploy(new List<DeploymentItem>(deploymentItems), source, runDirectories.OutDirectory, GetTestResultsDirectory(runContext));

        // Log warnings
        LogWarnings(testExecutionRecorder, warnings);
        return deploymentItems is { Count: > 0 };
    }

    private bool IsDeploymentItemSourceAFile(string deploymentItemSourcePath, string testSource, out string file)
    {
        file = GetFullPathToDeploymentItemSource(deploymentItemSourcePath, testSource);

        return FileUtility.DoesFileExist(file);
    }

    private bool IsDeploymentItemSourceADirectory(DeploymentItem deploymentItem, string testSource, [NotNullWhen(true)] out string? resultDirectory)
    {
        resultDirectory = null;

        string directory = GetFullPathToDeploymentItemSource(deploymentItem.SourcePath, testSource);
        directory = directory.TrimEnd('/', '\\');

        if (FileUtility.DoesDirectoryExist(directory))
        {
            resultDirectory = directory;
            return true;
        }

        return false;
    }
}
#endif
