// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
#endif
#if !WINDOWS_UWP
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
#endif
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
#if NETFRAMEWORK || NET
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
#endif
#if !WINDOWS_UWP
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// A host that loads the test source. This can be in isolation for desktop using an AppDomain or just loading the source in the current context.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class TestSourceHost : ITestSourceHost
{
#if !WINDOWS_UWP
#pragma warning disable IDE0052 // Remove unread private members
    private readonly string _sourceFileName;
#pragma warning restore IDE0052 // Remove unread private members
    private string? _currentDirectory;
#endif

#if NETFRAMEWORK || NET
    /// <summary>
    /// Assembly resolver used in the current app-domain.
    /// </summary>
    private AssemblyResolver? _parentDomainAssemblyResolver;
#endif

#if NETFRAMEWORK
    /// <summary>
    /// Determines whether child-appdomain needs to be created based on DisableAppDomain Flag set in runsettings.
    /// </summary>
#pragma warning disable SA1214 // Readonly fields should appear before non-readonly fields
    private readonly bool _isAppDomainCreationDisabled;
#pragma warning restore SA1214 // Readonly fields should appear before non-readonly fields

    private readonly IRunSettings? _runSettings;
    private readonly IFrameworkHandle? _frameworkHandle;
    private readonly IAppDomain _appDomain;

    private string? _targetFrameworkVersion;

    /// <summary>
    /// Assembly resolver used in the new child app-domain created for discovery/execution.
    /// </summary>
    private AssemblyResolver? _childDomainAssemblyResolver;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="TestSourceHost"/> class.
    /// </summary>
    /// <param name="sourceFileName"> The source file name. </param>
    /// <param name="runSettings"> The run-settings provided for this session. </param>
    /// <param name="frameworkHandle"> The handle to the test platform. </param>
    public TestSourceHost(string sourceFileName, IRunSettings? runSettings, IFrameworkHandle? frameworkHandle)
#if NETFRAMEWORK
        : this(sourceFileName, runSettings, frameworkHandle, new AppDomainWrapper())
#endif
    {
#if !WINDOWS_UWP && !NETFRAMEWORK
        _sourceFileName = sourceFileName;

        // Set the environment context.
        SetContext(sourceFileName);
#endif
    }

#if NETFRAMEWORK
    internal TestSourceHost(string sourceFileName, IRunSettings? runSettings, IFrameworkHandle? frameworkHandle, IAppDomain appDomain)
    {
        _sourceFileName = sourceFileName;
        _runSettings = runSettings;
        _frameworkHandle = frameworkHandle;
        _appDomain = appDomain;

        // Set the environment context.
        SetContext(sourceFileName);

        // Set isAppDomainCreationDisabled flag
        _isAppDomainCreationDisabled = _runSettings != null && MSTestAdapterSettings.IsAppDomainCreationDisabled(_runSettings.SettingsXml);
    }

    /// <summary>
    /// Gets the child AppDomain used to discover/execute tests.
    /// </summary>
    internal AppDomain? AppDomain { get; private set; }
#endif

    /// <summary>
    /// Setup the isolation host.
    /// </summary>
    public void SetupHost()
    {
#if NET
        List<string> resolutionPaths = GetResolutionPaths(_sourceFileName, false);

        if (EqtTrace.IsInfoEnabled)
        {
            EqtTrace.Info("DesktopTestSourceHost.SetupHost(): Creating assembly resolver with resolution paths {0}.", string.Join(",", resolutionPaths));
        }

        var assemblyResolver = new AssemblyResolver(resolutionPaths);
        if (TryAddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver(assemblyResolver, Path.GetDirectoryName(_sourceFileName)!))
        {
            _parentDomainAssemblyResolver = assemblyResolver;
        }
        else
        {
            assemblyResolver.Dispose();
        }
#elif NETFRAMEWORK
        List<string> resolutionPaths = GetResolutionPaths(_sourceFileName, VSInstallationUtilities.IsCurrentProcessRunningInPortableMode());

        if (EqtTrace.IsInfoEnabled)
        {
            EqtTrace.Info("DesktopTestSourceHost.SetupHost(): Creating assembly resolver with resolution paths {0}.", string.Join(",", resolutionPaths));
        }

        // NOTE: These 2 lines are super important, see https://github.com/microsoft/testfx/issues/2922
        // It's not entirely clear why but not assigning directly the resolver to the field (or/and) disposing the resolver in
        // case of an error in TryAddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver causes the issue.
        _parentDomainAssemblyResolver = new AssemblyResolver(resolutionPaths);
        _ = TryAddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver(_parentDomainAssemblyResolver, Path.GetDirectoryName(_sourceFileName)!);

        // Case when DisableAppDomain setting is present in runsettings and no child-appdomain needs to be created
        if (!_isAppDomainCreationDisabled)
        {
            // Setup app-domain
            var appDomainSetup = new AppDomainSetup();
            _targetFrameworkVersion = GetTargetFrameworkVersionString(_sourceFileName);
            AppDomainUtilities.SetAppDomainFrameworkVersionBasedOnTestSource(appDomainSetup, _targetFrameworkVersion);

            appDomainSetup.ApplicationBase = GetAppBaseAsPerPlatform();
            string? configFile = GetConfigFileForTestSource(_sourceFileName);
            AppDomainUtilities.SetConfigurationFile(appDomainSetup, configFile);

            EqtTrace.Info("DesktopTestSourceHost.SetupHost(): Creating app-domain for source {0} with application base path {1}.", _sourceFileName, appDomainSetup.ApplicationBase);

            string domainName = $"TestSourceHost: Enumerating source ({_sourceFileName})";
            AppDomain = _appDomain.CreateDomain(domainName, null!, appDomainSetup);

            // Load objectModel before creating assembly resolver otherwise in 3.5 process, we run into a recursive assembly resolution
            // which is trigged by AppContainerUtilities.AttachEventToResolveWinmd method.
            EqtTrace.SetupRemoteEqtTraceListeners(AppDomain);

            // Force loading Microsoft.TestPlatform.CoreUtilities in the new app domain to ensure there is no assembly resolution issue.
            // For unknown reasons, with MSTest 3.4+ we start to see infinite cycles of assembly resolution of this dll in the new app
            // domain. In older versions, this was not the case, and the callback was allowing to fully lookup and load the dll before
            // triggering the next resolution.
            AppDomain.Load(typeof(EqtTrace).Assembly.GetName());

            // Add an assembly resolver in the child app-domain...
            Type assemblyResolverType = typeof(AssemblyResolver);

            EqtTrace.Info("DesktopTestSourceHost.SetupHost(): assemblyenumerator location: {0} , fullname: {1} ", assemblyResolverType.Assembly.Location, assemblyResolverType.FullName);

            object resolver = AppDomainUtilities.CreateInstance(
                AppDomain,
                assemblyResolverType,
                [resolutionPaths]);

            EqtTrace.Info(
                "DesktopTestSourceHost.SetupHost(): resolver type: {0} , resolve type assembly: {1} ",
                resolver.GetType().FullName,
                resolver.GetType().Assembly.Location);

            _childDomainAssemblyResolver = (AssemblyResolver)resolver;

            _ = TryAddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver(_childDomainAssemblyResolver, Path.GetDirectoryName(_sourceFileName));
        }
#endif
    }

    /// <summary>
    /// Creates an instance of a given type in the test source host.
    /// </summary>
    /// <param name="type"> The type that needs to be created in the host. </param>
    /// <param name="args">The arguments to pass to the constructor.
    /// This array of arguments must match in number, order, and type the parameters of the constructor to invoke.
    /// Pass in null for a constructor with no arguments.
    /// </param>
    /// <returns>  An instance of the type created in the host. </returns>
    /// <remarks> If a type is to be created in isolation then it needs to be a MarshalByRefObject. </remarks>
    public object? CreateInstanceForType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type, object?[]? args) =>
#if NETFRAMEWORK
        // Honor DisableAppDomain setting if it is present in runsettings
        _isAppDomainCreationDisabled
            ? Activator.CreateInstance(type, args)
            : AppDomainUtilities.CreateInstance(AppDomain!, type, args);
#else
        Activator.CreateInstance(type, args);
#endif

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
#if NETFRAMEWORK || NET
        if (_parentDomainAssemblyResolver != null)
        {
            _parentDomainAssemblyResolver.Dispose();
            _parentDomainAssemblyResolver = null;
        }
#endif

#if NETFRAMEWORK
        if (_childDomainAssemblyResolver != null)
        {
            _childDomainAssemblyResolver.Dispose();
            _childDomainAssemblyResolver = null;
        }

        if (AppDomain != null)
        {
            try
            {
                _appDomain.Unload(AppDomain);
            }
            catch (Exception exception)
            {
                // This happens usually when a test spawns off a thread and fails to clean it up.
                EqtTrace.Error("DesktopTestSourceHost.Dispose(): The app domain running tests could not be unloaded. Exception: {0}", exception);

                if (_frameworkHandle != null)
                {
                    // Let the test platform know that it should tear down the test host process
                    // since we have issues in unloading appdomain. We do so to avoid any assembly locking issues.
                    _frameworkHandle.EnableShutdownAfterTestRun = true;

                    EqtTrace.Verbose("DesktopTestSourceHost.Dispose(): Notifying the test platform that the test host process should be shut down because the app domain running tests could not be unloaded successfully.");
                }
            }

            AppDomain = null;
        }

        ResetContext();

#elif !WINDOWS_UWP
        ResetContext();
#endif

        GC.SuppressFinalize(this);
    }

#if !WINDOWS_UWP
    /// <summary>
    /// Sets context required for running tests.
    /// </summary>
    /// <param name="source">
    /// source parameter used for setting context.
    /// </param>
    private void SetContext(string? source)
    {
        if (StringEx.IsNullOrEmpty(source))
        {
            return;
        }

        Exception? setWorkingDirectoryException = null;
        _currentDirectory = Environment.CurrentDirectory;
        try
        {
            // If the source is in the format of an assembly qualified name, then calls to
            // Path.GetDirectoryName will return empty string. But if we use Path.GetFullPath first
            // then directory resolution works properly.
            string? dirName = Path.GetDirectoryName(Path.GetFullPath(source));
#if WIN_UI
            if (StringEx.IsNullOrEmpty(dirName))
            {
                dirName = Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location)!;
            }

            Directory.SetCurrentDirectory(dirName);
#else
            Environment.CurrentDirectory = dirName!;
#if NETFRAMEWORK
            EqtTrace.Info("MSTestExecutor: Changed the working directory to {0}", Environment.CurrentDirectory);
#endif
#endif
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
    /// Resets the context as it was before calling SetContext().
    /// </summary>
    private void ResetContext()
    {
        if (!StringEx.IsNullOrEmpty(_currentDirectory))
        {
            Environment.CurrentDirectory = _currentDirectory;
        }
    }
#endif

#if NETFRAMEWORK
    /// <summary>
    /// Gets child-domain's appbase to point to appropriate location.
    /// </summary>
    /// <returns>Appbase path that should be set for child appdomain.</returns>
    internal string GetAppBaseAsPerPlatform()
    {
        // The below logic of preferential setting the appdomains appbase is needed because:
        // 1. We set this to the location of the test source if it is built for Full CLR  -> Ideally this needs to be done in all situations.
        // 2. We set this to the location where the current adapter is being picked up from for UWP and .Net Core scenarios -> This needs to be
        //    different especially for UWP because we use the desktop adapter(from %temp%\VisualStudioTestExplorerExtensions) itself for test discovery
        //    in IDE scenarios. If the app base is set to the test source location, discovery will not work because we drop the
        //    UWP platform service assembly at the test source location and since CLR starts looking for assemblies from the app base location,
        //    there would be a mismatch of platform service assemblies during discovery.
        DebugEx.Assert(_targetFrameworkVersion is not null, "Target framework version is null.");
        return _targetFrameworkVersion.Contains(Constants.DotNetFrameWorkStringPrefix)
            ? Path.GetDirectoryName(_sourceFileName) ?? Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location)
            : Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location);
    }

    internal virtual string GetTargetFrameworkVersionString(string sourceFileName)
        => AppDomainUtilities.GetTargetFrameworkVersionString(sourceFileName);

    private static string? GetConfigFileForTestSource(string sourceFileName)
        => new DeploymentUtility().GetConfigFile(sourceFileName);
#endif

#if NETFRAMEWORK || NET
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
        List<string> resolutionPaths =
        [

            // Add path of test assembly in resolution path. Mostly will be used for resolving winmd.
            Path.GetDirectoryName(sourceFileName)!,
        ];

        if (!isPortableMode)
        {
            EqtTrace.Info("DesktopTestSourceHost.GetResolutionPaths(): Not running in portable mode");

#if NETFRAMEWORK
            string? pathToPublicAssemblies = VSInstallationUtilities.PathToPublicAssemblies;
            if (!StringEx.IsNullOrWhiteSpace(pathToPublicAssemblies))
            {
                resolutionPaths.Add(pathToPublicAssemblies);
            }

            string? pathToPrivateAssemblies = VSInstallationUtilities.PathToPrivateAssemblies;
            if (!StringEx.IsNullOrWhiteSpace(pathToPrivateAssemblies))
            {
                resolutionPaths.Add(pathToPrivateAssemblies);
            }
#endif
        }

        // We check for the empty path, and in single file mode, or on source gen mode we don't allow
        // loading dependencies than from the current folder, which is what the default loader handles by itself.
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
        if (!string.IsNullOrEmpty(typeof(TestSourceHost).Assembly.Location))
        {
            // Adding adapter folder to resolution paths
            if (!resolutionPaths.Contains(Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location)!))
            {
                resolutionPaths.Add(Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location)!);
            }
        }

        if (!string.IsNullOrEmpty(typeof(AssemblyHelper).Assembly.Location))
        {
            // Adding TestPlatform folder to resolution paths
            if (!resolutionPaths.Contains(Path.GetDirectoryName(typeof(AssemblyHelper).Assembly.Location)!))
            {
                resolutionPaths.Add(Path.GetDirectoryName(typeof(AssemblyHelper).Assembly.Location)!);
            }
        }
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file

        return resolutionPaths;
    }

    private static bool TryAddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver(AssemblyResolver assemblyResolver, string baseDirectory)
    {
        // Check if user specified any adapter settings
        MSTestAdapterSettings adapterSettings = MSTestSettingsProvider.Settings;
        if (adapterSettings == null)
        {
            return false;
        }

        try
        {
            List<RecursiveDirectoryPath> additionalSearchDirectories = adapterSettings.GetDirectoryListWithRecursiveProperty(baseDirectory);
            if (additionalSearchDirectories.Count > 0)
            {
                assemblyResolver.AddSearchDirectoriesFromRunSetting(additionalSearchDirectories);
                return true;
            }
        }
        catch (Exception exception)
        {
            EqtTrace.Error(
                "DesktopTestSourceHost.AddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver(): Exception hit while trying to set assembly resolver for domain. Exception : {0} \n Message : {1}",
                exception,
                exception.Message);
        }

        return false;
    }
#endif
}
