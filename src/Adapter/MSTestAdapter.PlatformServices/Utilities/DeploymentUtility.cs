// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP

#if NETFRAMEWORK || NETSTANDARD || NETCOREAPP3_1
using System.Diagnostics;
#endif
using System.Globalization;
#if NETFRAMEWORK
using System.Security;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

internal sealed class DeploymentUtility : DeploymentUtilityBase
{
    public DeploymentUtility()
        : base()
    {
    }

    public DeploymentUtility(DeploymentItemUtility deploymentItemUtility, AssemblyUtility assemblyUtility, FileUtility fileUtility)
        : base(deploymentItemUtility, assemblyUtility, fileUtility)
    {
    }

    public override void AddDeploymentItemsBasedOnMsTestSetting(string testSource, IList<DeploymentItem> deploymentItems, List<string> warnings)
    {
#if NETFRAMEWORK
        if (MSTestSettingsProvider.Settings.DeployTestSourceDependencies)
        {
            EqtTrace.Info("Adding the references and satellite assemblies to the deployment items list");

            // Get the referenced assemblies.
            ProcessNewStorage(testSource, deploymentItems, warnings);

            // Get the satellite assemblies
            IEnumerable<DeploymentItem> satelliteItems = GetSatellites(deploymentItems, testSource, warnings);
            foreach (DeploymentItem satelliteItem in satelliteItems)
            {
                DeploymentItemUtility.AddDeploymentItem(deploymentItems, satelliteItem);
            }
        }
        else
        {
            EqtTrace.Info("Adding the test source directory to the deployment items list");
            DeploymentItemUtility.AddDeploymentItem(deploymentItems, new DeploymentItem(Path.GetDirectoryName(testSource)));
        }
#else
        // It should add items from bin\debug but since deployment items in netcore are run from bin\debug only, so no need to implement it
#endif
    }

    /// <summary>
    /// Get root deployment directory.
    /// </summary>
    /// <param name="baseDirectory">The base directory.</param>
    /// <returns>Root deployment directory.</returns>
    public override string GetRootDeploymentDirectory(string baseDirectory)
    {
#if NETFRAMEWORK || NETSTANDARD || NETCOREAPP3_1
        string dateTimeSuffix = $"{DateTime.Now.ToString("yyyyMMddTHHmmss", DateTimeFormatInfo.InvariantInfo)}_{Process.GetCurrentProcess().Id}";
#else
        string dateTimeSuffix = $"{DateTime.Now.ToString("yyyyMMddTHHmmss", DateTimeFormatInfo.InvariantInfo)}_{Environment.ProcessId}";
#endif
        string directoryName = string.Format(CultureInfo.InvariantCulture, Resource.TestRunName, DeploymentFolderPrefix,
#if NETFRAMEWORK
            Environment.UserName,
#else
            Environment.GetEnvironmentVariable("USERNAME") ?? Environment.GetEnvironmentVariable("USER"),
#endif
            dateTimeSuffix);
        directoryName = FileUtility.ReplaceInvalidFileNameCharacters(directoryName);

        return FileUtility.GetNextIterationDirectoryName(baseDirectory, directoryName);
    }

    protected override void AddDependenciesOfDeploymentItem(string deploymentItemFile, IList<string> filesToDeploy, IList<string> warnings)
    {
#if NETFRAMEWORK
        var dependencies = new List<DeploymentItem>();

        AddDependencies(deploymentItemFile, null, dependencies, warnings);

        foreach (DeploymentItem dependencyItem in dependencies)
        {
            DebugEx.Assert(Path.IsPathRooted(dependencyItem.SourcePath), "Path of the dependency " + dependencyItem.SourcePath + " is not rooted.");

            // Add dependencies to filesToDeploy.
            filesToDeploy.Add(dependencyItem.SourcePath);
        }
#else
        // Its implemented only in full framework project as dependent files are not fetched in netcore.
#endif
    }

#if NETFRAMEWORK
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public void ProcessNewStorage(string testSource, IList<DeploymentItem> deploymentItems, IList<string> warnings)
    {
        // Add deployment items and process .config files only for storages we have not processed before.
        if (!DeploymentItemUtility.IsValidDeploymentItem(testSource, string.Empty, out string? errorMessage))
        {
            warnings.Add(errorMessage);
            return;
        }

        DeploymentItemUtility.AddDeploymentItem(deploymentItems, new DeploymentItem(testSource, string.Empty, DeploymentItemOriginType.TestStorage));

        // Deploy .config file if exists, only for assemblies, i.e. DLL and EXE.
        // First check <TestStorage>.config, then if not found check for App.Config
        // and deploy AppConfig to <TestStorage>.config.
        if (AssemblyUtility.IsAssemblyExtension(Path.GetExtension(testSource)))
        {
            string? configFile = AddTestSourceConfigFileIfExists(testSource, deploymentItems);

            // Deal with test dependencies: update dependencyDeploymentItems and missingDependentAssemblies.
            try
            {
                // We look for dependent assemblies only for DLL and EXE's.
                AddDependencies(testSource, configFile, deploymentItems, warnings);
            }
            catch (Exception e)
            {
                string warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorFailedToDeployDependencies, testSource, e);
                warnings.Add(warning);
            }
        }
    }

    public IEnumerable<DeploymentItem> GetSatellites(IEnumerable<DeploymentItem> deploymentItems, string testSource, IList<string> warnings)
    {
        List<DeploymentItem> satellites = [];
        foreach (DeploymentItem item in deploymentItems)
        {
            // We do not care about deployment items which are directories because in that case we deploy all files underneath anyway.
            string? path = null;
            try
            {
                path = GetFullPathToDeploymentItemSource(item.SourcePath, testSource);
                path = Path.GetFullPath(path);

                if (StringEx.IsNullOrEmpty(path) || !AssemblyUtility.IsAssemblyExtension(Path.GetExtension(path))
                    || !FileUtility.DoesFileExist(path) || !AssemblyUtility.IsAssembly(path))
                {
                    continue;
                }
            }
            catch (ArgumentException ex)
            {
                EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
            }
            catch (SecurityException ex)
            {
                EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
            }
            catch (IOException ex)
            {
                // This covers PathTooLongException.
                EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
            }
            catch (NotSupportedException ex)
            {
                EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
            }

            // Note: now Path operations with itemPath should not result in any exceptions.
            // path is already canonicalized.

            // If we cannot access satellite due to security, etc, we report warning.
            try
            {
                string itemDir = Path.GetDirectoryName(path);
                List<string> itemSatellites = AssemblyUtility.GetSatelliteAssemblies(path!);
                foreach (string satellite in itemSatellites)
                {
                    DebugEx.Assert(!StringEx.IsNullOrEmpty(satellite), "DeploymentManager.DoDeployment: got empty satellite!");
                    DebugEx.Assert(
                        satellite.StartsWith(itemDir, StringComparison.OrdinalIgnoreCase),
                        "DeploymentManager.DoDeployment: Got satellite that does not start with original item path");

                    string satelliteDir = Path.GetDirectoryName(satellite);

                    string localeDir = itemDir.Length > satelliteDir.Length
                        ? string.Empty
                        : satelliteDir.Substring(itemDir.Length + 1);
                    string relativeOutputDir = Path.Combine(item.RelativeOutputDirectory, localeDir);

                    // Now finally add the item!
                    DeploymentItem satelliteItem = new(satellite, relativeOutputDir, DeploymentItemOriginType.Satellite);
                    DeploymentItemUtility.AddDeploymentItem(satellites, satelliteItem);
                }
            }
            catch (ArgumentException ex)
            {
                EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
                string warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorGettingSatellite, item, ex.GetType(), ex.GetExceptionMessage());
                warnings.Add(warning);
            }
            catch (SecurityException ex)
            {
                EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
                string warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorGettingSatellite, item, ex.GetType(), ex.GetExceptionMessage());
                warnings.Add(warning);
            }
            catch (IOException ex)
            {
                // This covers PathTooLongException.
                EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
                string warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorGettingSatellite, item, ex.GetType(), ex.GetExceptionMessage());
                warnings.Add(warning);
            }
        }

        return satellites;
    }

    /// <summary>
    /// Process test storage and add dependent assemblies to dependencyDeploymentItems.
    /// </summary>
    /// <param name="testSource">The test source.</param>
    /// <param name="configFile">The config file.</param>
    /// <param name="deploymentItems">Deployment items.</param>
    /// <param name="warnings">Warnings.</param>
    private void AddDependencies(string testSource, string? configFile, IList<DeploymentItem> deploymentItems, IList<string> warnings)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(testSource), "testSource should not be null or empty.");

        // config file can be null.
        DebugEx.Assert(deploymentItems != null, "deploymentItems should not be null.");
        DebugEx.Assert(Path.IsPathRooted(testSource), "path should be rooted.");

        var sw = Stopwatch.StartNew();

        // Note: if this is not an assembly we simply return empty array, also:
        //       we do recursive search and report missing.
        IReadOnlyList<string> references = AssemblyUtility.GetFullPathToDependentAssemblies(testSource, configFile, out IList<string>? warningList);
        foreach (string warning in warningList)
        {
            warnings.Add(warning);
        }

        if (EqtTrace.IsInfoEnabled)
        {
            EqtTrace.Info("DeploymentManager: Source:{0} has following references", testSource);
            EqtTrace.Info("DeploymentManager: Resolving dependencies took {0} ms", sw.ElapsedMilliseconds);
        }

        foreach (string reference in references)
        {
            DeploymentItem deploymentItem = new(reference, string.Empty, DeploymentItemOriginType.Dependency);
            DeploymentItemUtility.AddDeploymentItem(deploymentItems, deploymentItem);

            if (EqtTrace.IsInfoEnabled)
            {
                EqtTrace.Info("DeploymentManager: Reference:{0} ", reference);
            }
        }
    }
#endif
}

#endif
