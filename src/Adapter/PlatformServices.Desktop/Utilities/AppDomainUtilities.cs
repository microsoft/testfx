// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// Utilities for AppDomain
    /// </summary>
    internal static class AppDomainUtilities
    {
        private const string ObjectModelVersionBuiltAgainst = "11.0.0.0";

        private static Version defaultVersion = new Version();
        private static Version version45 = new Version("4.5");

        private static XmlUtilities xmlUtilities = null;

        /// <summary>
        /// Gets or sets the Xml Utilities instance.
        /// </summary>
        internal static XmlUtilities XmlUtilities
        {
            get
            {
                if (xmlUtilities == null)
                {
                    xmlUtilities = new XmlUtilities();
                }

                return xmlUtilities;
            }

            set
            {
                xmlUtilities = value;
            }
        }

        /// <summary>
        /// Set the target framework for app domain setup if target framework of dll is > 4.5
        /// </summary>
        /// <param name="setup">AppdomainSetup for app domain creation</param>
        /// <param name="frameworkVersionString">The target framework version of the test source.</param>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here.")]
        internal static void SetAppDomainFrameworkVersionBasedOnTestSource(AppDomainSetup setup, string frameworkVersionString)
        {
            if (GetTargetFrameworkVersionFromVersionString(frameworkVersionString).CompareTo(version45) > 0)
            {
                PropertyInfo pInfo = typeof(AppDomainSetup).GetProperty(PlatformServices.Constants.TargetFrameworkName);
                if (pInfo != null)
                {
                    pInfo.SetValue(setup, frameworkVersionString, null);
                }
            }
        }

        /// <summary>
        /// Get target framework version string from the given dll
        /// </summary>
        /// <param name="testSourcePath">
        /// The path of the dll
        /// </param>
        /// <returns>
        /// Framework string
        /// Todo: Need to add components/E2E tests to cover these scenarios.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        internal static string GetTargetFrameworkVersionString(string testSourcePath)
        {
            AppDomainSetup appDomainSetup = new AppDomainSetup();

            appDomainSetup.LoaderOptimization = LoaderOptimization.MultiDomainHost;

            AppDomainUtilities.SetConfigurationFile(appDomainSetup, new DeploymentUtility().GetConfigFile(testSourcePath));

            if (File.Exists(testSourcePath))
            {
                AppDomain appDomain = null;

                try
                {
                    appDomain = AppDomain.CreateDomain("Framework Version String Domain", null, appDomainSetup);

                    // Wire the eqttrace logs in this domain to the current domain.
                    EqtTrace.SetupRemoteEqtTraceListeners(appDomain);

                    // Add an assembly resolver to resolve ObjectModel or any Test Platform dependencies.
                    // Not moving to IMetaDataImport APIs because the time taken for this operation is <20 ms and
                    // IMetaDataImport needs COM registration which is not a guarantee in Dev15.
                    var assemblyResolverType = typeof(AssemblyResolver);

                    var resolutionPaths = new List<string> { Path.GetDirectoryName(typeof(TestCase).Assembly.Location) };
                    resolutionPaths.Add(Path.GetDirectoryName(testSourcePath));

                    AppDomainUtilities.CreateInstance(
                        appDomain,
                        assemblyResolverType,
                        new object[] { resolutionPaths });

                    var assemblyLoadWorker =
                        (AssemblyLoadWorker)AppDomainUtilities.CreateInstance(
                        appDomain,
                        typeof(AssemblyLoadWorker),
                        null);

                    return assemblyLoadWorker.GetTargetFrameworkVersionStringFromPath(testSourcePath);
                }
                catch (Exception exception)
                {
                    if (EqtTrace.IsErrorEnabled)
                    {
                        EqtTrace.Error(exception);
                    }
                }
                finally
                {
                    if (appDomain != null)
                    {
                        AppDomain.Unload(appDomain);
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Set configuration file on the parameter appDomain.
        /// </summary>
        /// <param name="appDomainSetup"> The app Domain Setup. </param>
        /// <param name="testSourceConfigFile"> The test Source Config File. </param>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        internal static void SetConfigurationFile(AppDomainSetup appDomainSetup, string testSourceConfigFile)
        {
            if (!string.IsNullOrEmpty(testSourceConfigFile))
            {
                if (EqtTrace.IsInfoEnabled)
                {
                    EqtTrace.Info("UnitTestAdapter: Using configuration file {0} to setup appdomain for test source {1}.", testSourceConfigFile, Path.GetFileNameWithoutExtension(testSourceConfigFile));
                }

                appDomainSetup.ConfigurationFile = Path.GetFullPath(testSourceConfigFile);

                try
                {
                    // Add redirection of the built 11.0 Object Model assembly to the current version if that is not 11.0
                    var currentVersionOfObjectModel = typeof(TestCase).Assembly.GetName().Version.ToString();
                    if (!string.Equals(currentVersionOfObjectModel, ObjectModelVersionBuiltAgainst))
                    {
                        var assemblyName = typeof(TestCase).Assembly.GetName();
                        var configurationBytes =
                            XmlUtilities.AddAssemblyRedirection(
                                testSourceConfigFile,
                                assemblyName,
                                ObjectModelVersionBuiltAgainst,
                                assemblyName.Version.ToString());
                        appDomainSetup.SetConfigurationBytes(configurationBytes);
                    }
                }
                catch (Exception ex)
                {
                    if (EqtTrace.IsErrorEnabled)
                    {
                        EqtTrace.Error("Exception hit while adding binding redirects to test source config file. Exception : {0}", ex);
                    }
                }
            }
            else
            {
                // Use the current domains configuration setting.
                appDomainSetup.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            }
        }

        internal static object CreateInstance(AppDomain appDomain, Type type, object[] arguments)
        {
            Debug.Assert(appDomain != null, "appDomain is null");
            Debug.Assert(type != null, "type is null");

            var typeAssemblyLocation = type.Assembly.Location;
            var fullFilePath = typeAssemblyLocation == null ? null : Path.Combine(appDomain.SetupInformation.ApplicationBase, Path.GetFileName(typeAssemblyLocation));

            if (fullFilePath == null || File.Exists(fullFilePath))
            {
                // If the assembly exists in the app base directory, load it from there itself.
                // Even if it does not exist, Create the type in the default Load Context and let the CLR resolve the assembly path.
                // This would load the assembly in the Default Load context.
                return appDomain.CreateInstanceAndUnwrap(
                    type.Assembly.FullName,
                    type.FullName,
                    false,
                    BindingFlags.Default,
                    null,
                    arguments,
                    null,
                    null);
            }
            else
            {
                // This means that the file is not present in the app base directory. Load it from Path instead.
                // NOTE: We expect that all types that we are creating from here are types we know the location for.
                // This would load the assembly in the Load-From context.
                // While the above if condition is satisfied for most common cases, there could be a case where the adapter dlls
                // do not get copied over to where the test assembly is, in which case we load them from where the parent AppDomain is picking them up from.
                return appDomain.CreateInstanceFromAndUnwrap(
                    typeAssemblyLocation,
                    type.FullName,
                    false,
                    BindingFlags.Default,
                    null,
                    arguments,
                    null,
                    null);
            }
        }

        /// <summary>
        /// Get the Version for the target framework version string
        /// </summary>
        /// <param name="version">Target framework string</param>
        /// <returns>Framework Version</returns>
        internal static Version GetTargetFrameworkVersionFromVersionString(string version)
        {
            try
            {
                if (version.Length > PlatformServices.Constants.DotNetFrameWorkStringPrefix.Length + 1)
                {
                    string versionPart = version.Substring(PlatformServices.Constants.DotNetFrameWorkStringPrefix.Length + 1);
                    return new Version(versionPart);
                }
            }
            catch (FormatException ex)
            {
                // if the version is ".NETPortable,Version=v4.5,Profile=Profile259", then above code will throw exception.
                EqtTrace.Warning(string.Format("AppDomainUtilities.GetTargetFrameworkVersionFromVersionString: Could not create version object from version string '{0}' due to error '{1}':", version, ex.Message));
            }

            return defaultVersion;
        }
    }
}
