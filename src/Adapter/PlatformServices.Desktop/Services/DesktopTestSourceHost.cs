// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    /// <summary>
    /// A host that loads the test source.This can be in isolation for desktop using an AppDomain or just loading the source in the current context.
    /// </summary>
    public class TestSourceHost : ITestSourceHost
    {
        /// <summary>
        /// Child AppDomain used to discover/execute tests
        /// </summary>
        private AppDomain domain;

        /// <summary>
        /// Assembly resolver used in the current app-domain
        /// </summary>
        private AssemblyResolver parentDomainAssemblyResolver;

        /// <summary>
        /// Assembly resolver used in the new child app-domain created for discovery/execution
        /// </summary>
        private AssemblyResolver childDomainAssemblyResolver;

        /// <summary>
        /// Determines whether child-appdomain needs to be created based on DisableAppDomain Flag set in runsettings
        /// </summary>
        private bool isAppDomainCreationDisabled;

        private string sourceFileName;
        private IRunSettings runSettings;
        private IFrameworkHandle frameworkHandle;

        private string currentDirectory = null;
        private IAppDomain appDomain;

        private string targetFrameworkVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestSourceHost"/> class.
        /// </summary>
        /// <param name="sourceFileName"> The source file name. </param>
        /// <param name="runSettings"> The run-settings provided for this session. </param>
        /// <param name="frameworkHandle"> The handle to the test platform. </param>
        public TestSourceHost(string sourceFileName, IRunSettings runSettings, IFrameworkHandle frameworkHandle)
            : this(sourceFileName, runSettings, frameworkHandle, new AppDomainWrapper())
        {
        }

        internal TestSourceHost(string sourceFileName, IRunSettings runSettings, IFrameworkHandle frameworkHandle, IAppDomain appDomain)
        {
            this.sourceFileName = sourceFileName;
            this.runSettings = runSettings;
            this.frameworkHandle = frameworkHandle;
            this.appDomain = appDomain;

            // Set the environment context.
            this.SetContext(sourceFileName);

            // Set isAppDomainCreationDisabled flag
            this.isAppDomainCreationDisabled = (this.runSettings != null) && MSTestAdapterSettings.IsAppDomainCreationDisabled(this.runSettings.SettingsXml);
        }

        internal AppDomain AppDomain
        {
            get
            {
                return this.domain;
            }
        }

        /// <summary>
        /// Setup the isolation host.
        /// </summary>
        public void SetupHost()
        {
            List<string> resolutionPaths = this.GetResolutionPaths(this.sourceFileName, VSInstallationUtilities.IsCurrentProcessRunningInPortableMode());

            if (EqtTrace.IsInfoEnabled)
            {
                EqtTrace.Info("DesktopTestSourceHost.SetupHost(): Creating assembly resolver with resolution paths {0}.", string.Join(",", resolutionPaths.ToArray()));
            }

            // Case when DisableAppDomain setting is present in runsettings and no child-appdomain needs to be created
            if (this.isAppDomainCreationDisabled)
            {
                this.parentDomainAssemblyResolver = new AssemblyResolver(resolutionPaths);
                this.AddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver(this.parentDomainAssemblyResolver, Path.GetDirectoryName(this.sourceFileName));
            }

            // Create child-appdomain and set assembly resolver on it
            else
            {
                // Setup app-domain
                var appDomainSetup = new AppDomainSetup();
                this.targetFrameworkVersion = this.GetTargetFrameworkVersionString(this.sourceFileName);
                AppDomainUtilities.SetAppDomainFrameworkVersionBasedOnTestSource(appDomainSetup, this.targetFrameworkVersion);

                appDomainSetup.ApplicationBase = this.GetAppBaseAsPerPlatform();
                var configFile = this.GetConfigFileForTestSource(this.sourceFileName);
                AppDomainUtilities.SetConfigurationFile(appDomainSetup, configFile);

                EqtTrace.Info("DesktopTestSourceHost.SetupHost(): Creating app-domain for source {0} with application base path {1}.", this.sourceFileName, appDomainSetup.ApplicationBase);

                string domainName = string.Format("TestSourceHost: Enumerating source ({0})", this.sourceFileName);
                this.domain = this.appDomain.CreateDomain(domainName, null, appDomainSetup);

                // Load objectModel before creating assembly resolver otherwise in 3.5 process, we run into a recursive assembly resolution
                // which is trigged by AppContainerUtilities.AttachEventToResolveWinmd method.
                EqtTrace.SetupRemoteEqtTraceListeners(this.domain);

                // Add an assembly resolver in the child app-domain...
                Type assemblyResolverType = typeof(AssemblyResolver);

                EqtTrace.Info("DesktopTestSourceHost.SetupHost(): assemblyenumerator location: {0} , fullname: {1} ", assemblyResolverType.Assembly.Location, assemblyResolverType.FullName);

                var resolver = AppDomainUtilities.CreateInstance(
                    this.domain,
                    assemblyResolverType,
                    new object[] { resolutionPaths });

                EqtTrace.Info(
                    "DesktopTestSourceHost.SetupHost(): resolver type: {0} , resolve type assembly: {1} ",
                    resolver.GetType().FullName,
                    resolver.GetType().Assembly.Location);

                this.childDomainAssemblyResolver = (AssemblyResolver)resolver;

                this.AddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver(this.childDomainAssemblyResolver, Path.GetDirectoryName(this.sourceFileName));
            }
        }

        /// <summary>
        /// Creates an instance of a given type in the test source host.
        /// </summary>
        /// <param name="type"> The type that needs to be created in the host. </param>
        /// <param name="args">The arguments to pass to the constructor.
        /// This array of arguments must match in number, order, and type the parameters of the constructor to invoke.
        /// Pass in null for a constructor with no arguments.
        /// </param>
        /// <returns> An instance of the type created in the host. </returns>
        /// <remarks> If a type is to be created in isolation then it needs to be a MarshalByRefObject. </remarks>
        public object CreateInstanceForType(Type type, object[] args)
        {
            // Honor DisableAppDomain setting if it is present in runsettings
            if (this.isAppDomainCreationDisabled)
            {
                return Activator.CreateInstance(type, args);
            }

            return AppDomainUtilities.CreateInstance(this.domain, type, args);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.parentDomainAssemblyResolver != null)
            {
                this.parentDomainAssemblyResolver.Dispose();
                this.parentDomainAssemblyResolver = null;
            }

            if (this.childDomainAssemblyResolver != null)
            {
                this.childDomainAssemblyResolver.Dispose();
                this.childDomainAssemblyResolver = null;
            }

            if (this.domain != null)
            {
                try
                {
                    this.appDomain.Unload(this.domain);
                }
                catch (Exception exception)
                {
                    // This happens usually when a test spawns off a thread and fails to clean it up.
                    EqtTrace.Error("DesktopTestSourceHost.Dispose(): The app domain running tests could not be unloaded. Exception: {0}", exception);

                    if (this.frameworkHandle != null)
                    {
                        // Let the test platform know that it should tear down the test host process
                        // since we have issues in unloading appdomain. We do so to avoid any assembly locking issues.
                        this.frameworkHandle.EnableShutdownAfterTestRun = true;

                        EqtTrace.Verbose("DesktopTestSourceHost.Dispose(): Notifying the test platform that the test host process should be shut down because the app domain running tests could not be unloaded successfully.");
                    }
                }

                this.domain = null;
            }

            this.ResetContext();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets child-domain's appbase to point to appropriate location.
        /// </summary>
        /// <returns>Appbase path that should be set for child appdomain</returns>
        internal string GetAppBaseAsPerPlatform()
        {
            // The below logic of preferential setting the appdomains appbase is needed because:
            // 1. We set this to the location of the test source if it is built for Full CLR  -> Ideally this needs to be done in all situations.
            // 2. We set this to the location where the current adapter is being picked up from for UWP and .Net Core scenarios -> This needs to be
            //    different especially for UWP because we use the desktop adapter(from %temp%\VisualStudioTestExplorerExtensions) itself for test discovery
            //    in IDE scenarios. If the app base is set to the test source location, discovery will not work because we drop the
            //    UWP platform service assembly at the test source location and since CLR starts looking for assemblies from the app base location,
            //    there would be a mismatch of platform service assemblies during discovery.
            if (this.targetFrameworkVersion.Contains(PlatformServices.Constants.DotNetFrameWorkStringPrefix))
            {
                return Path.GetDirectoryName(this.sourceFileName) ?? Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location);
            }
            else
            {
                return Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location);
            }
        }

        /// <summary>
        /// Gets the probing paths to load the test assembly dependencies.
        /// </summary>
        /// <param name="sourceFileName">
        /// The source File Name.
        /// </param>
        /// <param name="isPortableMode">
        /// True if running in portable mode else false.
        /// </param>
        /// <returns>
        /// A list of path.
        /// </returns>
        internal virtual List<string> GetResolutionPaths(string sourceFileName, bool isPortableMode)
        {
            List<string> resolutionPaths = new List<string>();

            // Add path of test assembly in resolution path. Mostly will be used for resolving winmd.
            resolutionPaths.Add(Path.GetDirectoryName(sourceFileName));

            if (!isPortableMode)
            {
                EqtTrace.Info("DesktopTestSourceHost.GetResolutionPaths(): Not running in portable mode");

                string pathToPublicAssemblies = VSInstallationUtilities.PathToPublicAssemblies;
                if (!StringUtilities.IsNullOrWhiteSpace(pathToPublicAssemblies))
                {
                    resolutionPaths.Add(pathToPublicAssemblies);
                }

                string pathToPrivateAssemblies = VSInstallationUtilities.PathToPrivateAssemblies;
                if (!StringUtilities.IsNullOrWhiteSpace(pathToPrivateAssemblies))
                {
                    resolutionPaths.Add(pathToPrivateAssemblies);
                }
            }

            // Adding adapter folder to resolution paths
            if (!resolutionPaths.Contains(Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location)))
            {
                resolutionPaths.Add(Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location));
            }

            // Adding TestPlatform folder to resolution paths
            if (!resolutionPaths.Contains(Path.GetDirectoryName(typeof(AssemblyHelper).Assembly.Location)))
            {
                resolutionPaths.Add(Path.GetDirectoryName(typeof(AssemblyHelper).Assembly.Location));
            }

            return resolutionPaths;
        }

        internal virtual string GetTargetFrameworkVersionString(string sourceFileName)
        {
            return AppDomainUtilities.GetTargetFrameworkVersionString(sourceFileName);
        }

        private string GetConfigFileForTestSource(string sourceFileName)
        {
            return new DeploymentUtility().GetConfigFile(sourceFileName);
        }

        /// <summary>
        /// Sets context required for running tests.
        /// </summary>
        /// <param name="source">
        /// source parameter used for setting context
        /// </param>
        private void SetContext(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            Exception setWorkingDirectoryException = null;
            this.currentDirectory = Environment.CurrentDirectory;

            try
            {
                Environment.CurrentDirectory = Path.GetDirectoryName(source);
                EqtTrace.Info("MSTestExecutor: Changed the working directory to {0}", Environment.CurrentDirectory);
            }
            catch (IOException ex)
            {
                setWorkingDirectoryException = ex;
            }
            catch (System.Security.SecurityException ex)
            {
                setWorkingDirectoryException = ex;
            }

            if (setWorkingDirectoryException != null)
            {
                EqtTrace.Error("MSTestExecutor.SetWorkingDirectory: Failed to set the working directory to '{0}'. {1}", Path.GetDirectoryName(source), setWorkingDirectoryException);
            }
        }

        /// <summary>
        /// Resets the context as it was before calling SetContext()
        /// </summary>
        private void ResetContext()
        {
            if (!string.IsNullOrEmpty(this.currentDirectory))
            {
                Environment.CurrentDirectory = this.currentDirectory;
            }
        }

        private void AddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver(AssemblyResolver assemblyResolver, string baseDirectory)
        {
            // Check if user specified any adapter settings
            MSTestAdapterSettings adapterSettings = MSTestSettingsProvider.Settings;

            if (adapterSettings != null)
            {
                try
                {
                    var additionalSearchDirectories = adapterSettings.GetDirectoryListWithRecursiveProperty(baseDirectory);
                    if (additionalSearchDirectories?.Count > 0)
                    {
                        assemblyResolver.AddSearchDirectoriesFromRunSetting(additionalSearchDirectories);
                    }
                }
                catch (Exception exception)
                {
                    EqtTrace.Error(
                        "DesktopTestSourceHost.AddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver(): Exception hit while trying to set assembly resolver for domain. Exception : {0} \n Message : {1}",
                        exception,
                        exception.Message);
                }
            }
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
