// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Implementation for the Invoker which invokes engine in a new AppDomain
/// Type of the engine must be a marshalable object for app domain calls and also must have a parameterless constructor.
/// </summary>
internal sealed class AppDomainEngineInvoker : IDisposable
{
    private const string XmlNamespace = "urn:schemas-microsoft-com:asm.v1";

    private readonly AppDomain _appDomain;

    private string? _mergedTempConfigFile;

    public AppDomainEngineInvoker(string testSourcePath)
        => _appDomain = CreateNewAppDomain(testSourcePath);

    /// <summary>
    /// Invokes the Engine.
    /// </summary>
    public void Dispose()
    {
        try
        {
            // if(AppDomain != null)
            // {
            //     // Do not unload appdomain as there are lot is issues reported against appdomain unload
            //     // any ways the process is going to die off.
            //     AppDomain.Unload(AppDomain);
            // }
            if (!string.IsNullOrWhiteSpace(_mergedTempConfigFile) && File.Exists(_mergedTempConfigFile))
            {
                File.Delete(_mergedTempConfigFile);
            }
        }
        catch
        {
            // ignore
        }
    }

    private AppDomain CreateNewAppDomain(string testSourcePath)
    {
        var appDomainSetup = new AppDomainSetup();
        string testSourceFolder = Path.GetDirectoryName(testSourcePath);

        // Set AppBase to TestAssembly location
        appDomainSetup.ApplicationBase = testSourceFolder;
        appDomainSetup.LoaderOptimization = LoaderOptimization.MultiDomainHost;

        // Set User Config file as app domain config
        SetConfigurationFile(appDomainSetup, testSourcePath, testSourceFolder);

        // Create new AppDomain
        var appDomain = AppDomain.CreateDomain("TestHostAppDomainByMSTest", null, appDomainSetup);

        return appDomain;
    }

    /// <summary>
    /// Create the Engine Invoker in new AppDomain based on test source path.
    /// </summary>
    /// <typeparam name="T">The type to create in the app domain</typeparam>
    /// <returns>The engine invoker in AppDomain.</returns>
    internal T CreateInvokerInAppDomain<T>()
        where T : new()
    {
        // Create CustomAssembly setup that sets a custom assembly resolver to be able to resolve TestPlatform assemblies
        // and also sets the correct UI culture to propagate the dotnet or VS culture to the adapters running in the app domain
        _appDomain.CreateInstanceFromAndUnwrap(
            typeof(CustomAssemblySetup).Assembly.Location,
            typeof(CustomAssemblySetup).FullName,
            false,
            BindingFlags.Default,
            null,
            [CultureInfo.DefaultThreadCurrentUICulture?.Name, Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)],
            null,
            null);

        // Create Invoker object in new appdomain
        Type invokerType = typeof(T);
        return (T)_appDomain.CreateInstanceFromAndUnwrap(
            invokerType.Assembly.Location,
            invokerType.FullName,
            false,
            BindingFlags.Default,
            null,
            null,
            null,
            null);
    }

    private void SetConfigurationFile(AppDomainSetup appDomainSetup, string testSource, string testSourceFolder)
    {
        string? userConfigFile = GetConfigFile(testSource, testSourceFolder);
        string testHostAppConfigFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

        if (!string.IsNullOrEmpty(userConfigFile))
        {
            var userConfigDoc = XDocument.Load(userConfigFile);
            var testHostConfigDoc = XDocument.Load(testHostAppConfigFile);

            // Merge user's config file and testHost config file and use merged one
            XDocument mergedConfigDocument = MergeApplicationConfigFiles(userConfigDoc, testHostConfigDoc);

            // Create a temp file with config
            _mergedTempConfigFile = Path.GetTempFileName();
            mergedConfigDocument.Save(_mergedTempConfigFile);

            // Set config file to merged one
            appDomainSetup.ConfigurationFile = _mergedTempConfigFile;
        }
        else
        {
            // Use the current domains configuration setting.
            appDomainSetup.ConfigurationFile = testHostAppConfigFile;
        }
    }

    private static string? GetConfigFile(string testSource, string testSourceFolder)
    {
        string? configFile = null;

        if (File.Exists(testSource + ".config"))
        {
            // Path to config file cannot be bad: storage is already checked, and extension is valid.
            configFile = testSource + ".config";
        }
        else
        {
            string netAppConfigFile = Path.Combine(testSourceFolder, "App.Config");
            if (File.Exists(netAppConfigFile))
            {
                configFile = netAppConfigFile;
            }
        }

        return configFile;
    }

    private static XDocument MergeApplicationConfigFiles(XDocument userConfigDoc, XDocument testHostConfigDoc)
    {
        // Start with User's config file as the base
        var mergedDoc = new XDocument(userConfigDoc);

        // Take testhost.exe Startup node
        XElement? startupNode = testHostConfigDoc.Descendants("startup")?.FirstOrDefault();
        if (startupNode != null)
        {
            // Remove user's startup and add ours which supports NET35
            mergedDoc.Descendants("startup")?.Remove();
            mergedDoc.Root.Add(startupNode);
        }

        // Runtime node must be merged which contains assembly redirections
        XElement? runtimeTestHostNode = testHostConfigDoc.Descendants("runtime")?.FirstOrDefault();
        if (runtimeTestHostNode != null)
        {
            XElement? runTimeNode = mergedDoc.Descendants("runtime")?.FirstOrDefault();
            if (runTimeNode == null)
            {
                // remove test host relative probing paths' element
                // TestHost Probing Paths do not make sense since we are setting "AppBase" to user's test assembly location
                runtimeTestHostNode.Descendants().Where((element) => string.Equals(element.Name.LocalName, "probing", StringComparison.Ordinal)).Remove();

                // no runtime node exists in user's config - just add ours entirely
                mergedDoc.Root.Add(runtimeTestHostNode);
            }
            else
            {
                var assemblyBindingXName = XName.Get("assemblyBinding", XmlNamespace);
                XElement? mergedDocAssemblyBindingNode = mergedDoc.Descendants(assemblyBindingXName)?.FirstOrDefault();
                XElement? testHostAssemblyBindingNode = runtimeTestHostNode.Descendants(assemblyBindingXName)?.FirstOrDefault();

                if (testHostAssemblyBindingNode != null)
                {
                    if (mergedDocAssemblyBindingNode == null)
                    {
                        // add another assemblyBinding element as none exists in user's config
                        runTimeNode.Add(testHostAssemblyBindingNode);
                    }
                    else
                    {
                        var dependentAssemblyXName = XName.Get("dependentAssembly", XmlNamespace);
                        IEnumerable<XElement> redirections = testHostAssemblyBindingNode.Descendants(dependentAssemblyXName);

                        if (redirections != null)
                        {
                            mergedDocAssemblyBindingNode.Add(redirections);
                        }
                    }
                }
            }
        }

        return mergedDoc;
    }
}

/// <summary>
/// Custom domain setup that sets UICulture and an Assembly resolver for child app domain to resolve testplatform assemblies.
/// </summary>
// The normal AppDomainInitializer api was not used to do this because it cannot load the assemblies for testhost. --JJR
internal sealed class CustomAssemblySetup : MarshalByRefObject
{
    private readonly Dictionary<string, Assembly?> _resolvedAssemblies;

    private readonly string[] _resolverPaths;

    public CustomAssemblySetup(string uiCulture, string testPlatformPath)
    {
        if (uiCulture != null)
        {
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CreateSpecificCulture(uiCulture);
        }

        _resolverPaths = [testPlatformPath, Path.Combine(testPlatformPath, "Extensions")];
        _resolvedAssemblies = [];
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    }

    private Assembly? CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name);

        Assembly? assembly = null;
        lock (_resolvedAssemblies)
        {
            try
            {
                EqtTrace.Verbose("CurrentDomain_AssemblyResolve: Resolving assembly '{0}'.", args.Name);

                if (_resolvedAssemblies.TryGetValue(args.Name, out assembly))
                {
                    return assembly;
                }

                // Put it in the resolved assembly so that if below Assembly.Load call
                // triggers another assembly resolution, then we don't end up in stack overflow
                _resolvedAssemblies[args.Name] = null;

                foreach (string path in _resolverPaths)
                {
                    string testPlatformFilePath = Path.Combine(path, assemblyName.Name) + ".dll";
                    if (File.Exists(testPlatformFilePath))
                    {
                        try
                        {
                            assembly = Assembly.LoadFrom(testPlatformFilePath);
                            break;
                        }
                        catch (Exception)
                        {
                            // ignore
                        }
                    }
                }

                // Replace the value with the loaded assembly
                _resolvedAssemblies[args.Name] = assembly;

                return assembly;
            }
            finally
            {
                if (assembly == null)
                {
                    EqtTrace.Verbose("CurrentDomainAssemblyResolve: Failed to resolve assembly '{0}'.", args.Name);
                }
            }
        }
    }
}
#endif
