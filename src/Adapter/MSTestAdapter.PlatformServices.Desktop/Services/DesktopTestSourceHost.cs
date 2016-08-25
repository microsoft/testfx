// Copyright (c) Microsoft. All rights reserved.

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
        /// AppDomain used to discover tests
        /// </summary>
        private AppDomain appDomain;

        /// <summary>
        /// Assembly resolver used in the app-domain
        /// </summary>
        private AssemblyResolver assemblyResolver;

        private List<string> cachedResolutionPaths;

        /// <summary>
        /// Creates an instance of a given type in the test source host.
        /// </summary>
        /// <param name="type"> The type that needs to be created in the host. </param>
        /// <param name="args">The arguments to pass to the constructor. 
        /// This array of arguments must match in number, order, and type the parameters of the constructor to invoke. 
        /// Pass in null for a constructor with no arguments.
        /// </param>
        /// <param name="sourceFileName"> The source. </param>
        /// <param name="runSettings"> The run-settings provided for this session. </param>
        /// <returns> An instance of the type created in the host. </returns>
        /// <remarks> If a type is to be created in isolation then it needs to be a MarshalByRefObject. </remarks>
        public object CreateInstanceForType(Type type, object[] args, string sourceFileName, IRunSettings runSettings)
        {
            List<string> resolutionPaths = this.GetResolutionPaths(sourceFileName, VSInstallationUtilities.CheckIfTestProcessIsRunningInXcopyableMode());

            // Check if user specified any runsettings
            MSTestAdapterSettings adapterSettings = MSTestSettingsProvider.Settings;

            if (resolutionPaths != null && resolutionPaths.Count > 0)
            {
                if (EqtTrace.IsInfoEnabled)
                {
                    EqtTrace.Info("TestSourceHost: Creating assembly resolver with resolution paths {0}.", string.Join(",", resolutionPaths.ToArray()));
                }

                // Adding adapter folder to resolution paths
                if (!resolutionPaths.Contains(Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location)))
                {
                    resolutionPaths.Add(Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location));
                }

                // Adding extensions folder to resolution paths
                if (!resolutionPaths.Contains(Path.GetDirectoryName(typeof(AssemblyHelper).Assembly.Location)))
                {
                    resolutionPaths.Add(Path.GetDirectoryName(typeof(AssemblyHelper).Assembly.Location));
                }
            }

            //Honour DisableAppDomain setting if it is present in runsettings
            if (runSettings!=null && MSTestAdapterSettings.IsAppDomainCreationDisabled(runSettings.SettingsXml))
            {
                if (adapterSettings != null)
                {
                    try
                    {
                        this.assemblyResolver = new AssemblyResolver(resolutionPaths);
                        this.assemblyResolver.AddSearchDirectoriesFromRunSetting(adapterSettings.GetDirectoryListWithRecursiveProperty(null));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
                return Activator.CreateInstance(type, args);
            }

            var appDomainSetup = new AppDomainSetup();
            if (EqtTrace.IsInfoEnabled)
            {
                EqtTrace.Info("TestSourceHost: Creating app-domain for source {0} with application base path {1}.", sourceFileName, appDomainSetup.ApplicationBase);
            }
           
            AppDomainUtilities.SetAppDomainFrameworkVersionBasedOnTestSource(appDomainSetup, sourceFileName);

            AppDomainUtilities.SetConfigurationFile(appDomainSetup, sourceFileName);

            this.appDomain = AppDomain.CreateDomain("TestSourceHost: Enumering assembly", null, appDomainSetup);

            // #824545 Load objectModel before creating assembly resolver otherwise in 3.5 process, we run into a recurive assembly resolution
            // which is trigged by AppContainerUtilities.AttachEventToResolveWinmd method. 
            EqtTrace.SetupRemoteEqtTraceListeners(this.appDomain);

            // Add an assembly resolver...
            Type assemblyResolverType = typeof(AssemblyResolver);

            if (EqtTrace.IsInfoEnabled)
            {
                EqtTrace.Info("TestSourceHost: assemblyenumerator location: {0} , fullname: {1} ", assemblyResolverType.Assembly.Location, assemblyResolverType.FullName);
            }

            var resolver = this.appDomain.CreateInstanceFromAndUnwrap(
                    assemblyResolverType.Assembly.Location,
                    assemblyResolverType.FullName,
                    false,
                    BindingFlags.Default,
                    null,
                    new object[] { resolutionPaths },
                    null,
                    null);

            if (EqtTrace.IsInfoEnabled)
            {
                EqtTrace.Info(
                    "TestSourceHost: resolver type: {0} , resolve type assembly: {1} ",
                    resolver.GetType().FullName,
                    resolver.GetType().Assembly.Location);
            }

            this.assemblyResolver = (AssemblyResolver)resolver;

            if (adapterSettings != null)
            {
                try
                {
                    this.assemblyResolver.AddSearchDirectoriesFromRunSetting(adapterSettings.GetDirectoryListWithRecursiveProperty(appDomainSetup.ApplicationBase));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            // This has to be LoadFrom, otherwise we will have to use AssemblyResolver to find self.
            var enumerator = this.appDomain.CreateInstanceFromAndUnwrap(
                type.Assembly.Location,
                type.FullName,
                false,
                BindingFlags.Default,
                null,
                args,
                null,
                null);

            EqtTrace.SetupRemoteEqtTraceListeners(this.appDomain);

            return enumerator;
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
        internal List<string> GetResolutionPaths(string sourceFileName, bool isPortableMode)
        {
            if (this.cachedResolutionPaths == null || this.cachedResolutionPaths.Count <= 0)
            {
                this.cachedResolutionPaths = new List<string>();

                // Add path of test assembly in resolution path. Mostly will be used for resovling winmd.
                this.cachedResolutionPaths.Add(Path.GetDirectoryName(sourceFileName));

                if (!isPortableMode)
                {
                    if (EqtTrace.IsInfoEnabled)
                    {
                        EqtTrace.Info("TestSourceHost: Not running in portable mode");
                    }

                    string pathToPublicAssemblies = VSInstallationUtilities.PathToPublicAssemblies;
                    if (!StringUtilities.IsNullOrWhiteSpace(pathToPublicAssemblies))
                    {
                        this.cachedResolutionPaths.Add(pathToPublicAssemblies);
                    }

                    string pathToPrivateAssemblies = VSInstallationUtilities.PathToPrivateAssemblies;
                    if (!StringUtilities.IsNullOrWhiteSpace(pathToPrivateAssemblies))
                    {
                        this.cachedResolutionPaths.Add(pathToPrivateAssemblies);
                    }
                }
            }

            return this.cachedResolutionPaths;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.assemblyResolver != null)
            {
                this.assemblyResolver.Dispose();
                this.assemblyResolver = null;
            }

            if (this.appDomain != null)
            {
                AppDomain.Unload(this.appDomain);
                this.appDomain = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
