// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

/// <summary>
/// Utilities for AppDomain.
/// </summary>
internal static class AppDomainUtilities
{
    private const string ObjectModelVersionBuiltAgainst = "11.0.0.0";

    private static readonly Version DefaultVersion = new();
    private static readonly Version Version45 = new("4.5");

    /// <summary>
    /// Gets or sets the Xml Utilities instance.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    internal static XmlUtilities XmlUtilities
    {
        get => field ??= new XmlUtilities();
        set;
    }

    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here.")]
    internal static void SetAppDomainFrameworkVersionBasedOnTestSource(AppDomainSetup setup, string frameworkVersionString, IAdapterTraceLogger logger)
    {
        if (GetTargetFrameworkVersionFromVersionString(frameworkVersionString, logger).CompareTo(Version45) > 0)
        {
            PropertyInfo? pInfo = typeof(AppDomainSetup).GetProperty(EngineConstants.TargetFrameworkName);
            pInfo?.SetValue(setup, frameworkVersionString, null);
        }
    }

    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
    internal static string GetTargetFrameworkVersionString(string testSourcePath, IAdapterTraceLogger logger)
    {
        AppDomainSetup appDomainSetup = new()
        {
            LoaderOptimization = LoaderOptimization.MultiDomainHost,
        };

        SetConfigurationFile(appDomainSetup, new DeploymentUtility(logger).GetConfigFile(testSourcePath), logger);
        if (!File.Exists(testSourcePath))
        {
            return string.Empty;
        }

        AppDomain? appDomain = null;

        try
        {
            appDomain = AppDomain.CreateDomain("Framework Version String Domain", null, appDomainSetup);

            // Wire the eqttrace logs in this domain to the current domain.
            EqtTrace.SetupRemoteEqtTraceListeners(appDomain);

            // Add an assembly resolver to resolve ObjectModel or any Test Platform dependencies.
            // Not moving to IMetaDataImport APIs because the time taken for this operation is <20 ms and
            // IMetaDataImport needs COM registration which is not a guarantee in Dev15.
            Type assemblyResolverType = typeof(AssemblyResolver);

            var resolutionPaths = new List<string>
                {
                    Path.GetDirectoryName(typeof(TestCase).Assembly.Location),
                    Path.GetDirectoryName(testSourcePath),
                };

            CreateInstance(
                appDomain,
                assemblyResolverType,
                [resolutionPaths]);

            var assemblyLoadWorker =
                (AssemblyLoadWorker)CreateInstance(
                appDomain,
                typeof(AssemblyLoadWorker),
                [logger]);

            string targetFramework = assemblyLoadWorker.GetTargetFrameworkVersionStringFromPath(testSourcePath, out string? errorMessage);
            if (errorMessage is not null)
            {
                logger.LogError(errorMessage);
            }

            return targetFramework;
        }
        catch (Exception exception)
        {
            logger.LogError(exception.ToString());
        }
        finally
        {
            if (appDomain != null)
            {
                AppDomain.Unload(appDomain);
            }
        }

        return string.Empty;
    }

    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    internal static void SetConfigurationFile(AppDomainSetup appDomainSetup, string? testSourceConfigFile, IAdapterTraceLogger logger)
    {
        if (StringEx.IsNullOrEmpty(testSourceConfigFile))
        {
            // Use the current domains configuration setting.
            appDomainSetup.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            return;
        }

        logger.LogInfo("UnitTestAdapter: Using configuration file {0} to setup appdomain for test source {1}.", testSourceConfigFile, Path.GetFileNameWithoutExtension(testSourceConfigFile));
        appDomainSetup.ConfigurationFile = Path.GetFullPath(testSourceConfigFile);

        try
        {
            // Add redirection of the built 11.0 Object Model assembly to the current version if that is not 11.0
            string currentVersionOfObjectModel = typeof(TestCase).Assembly.GetName().Version.ToString();
            if (!string.Equals(currentVersionOfObjectModel, ObjectModelVersionBuiltAgainst, StringComparison.Ordinal))
            {
                AssemblyName assemblyName = typeof(TestCase).Assembly.GetName();
                byte[] configurationBytes =
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
            logger.LogError("Exception hit while adding binding redirects to test source config file. Exception : {0}", ex);
        }
    }

    internal static object CreateInstance(AppDomain appDomain, Type type, object?[]? arguments)
    {
        string? typeAssemblyLocation = type.Assembly.Location;
        string? fullFilePath = typeAssemblyLocation == null ? null : Path.Combine(appDomain.SetupInformation.ApplicationBase, Path.GetFileName(typeAssemblyLocation));

        EnsureRelevantStaticStateIsRestored(appDomain);

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

    internal static Version GetTargetFrameworkVersionFromVersionString(string version, IAdapterTraceLogger logger)
    {
        try
        {
            if (version.Length > EngineConstants.DotNetFrameWorkStringPrefix.Length + 1)
            {
                string versionPart = version.Substring(EngineConstants.DotNetFrameWorkStringPrefix.Length + 1);
                return new Version(versionPart);
            }
        }
        catch (FormatException ex)
        {
            // if the version is ".NETPortable,Version=v4.5,Profile=Profile259", then above code will throw exception.
            logger.LogWarning($"AppDomainUtilities.GetTargetFrameworkVersionFromVersionString: Could not create version object from version string '{version}' due to error '{ex.Message}':");
        }

        return DefaultVersion;
    }

    private static void EnsureRelevantStaticStateIsRestored(AppDomain appDomain)
    {
        // AppDomain is not preserving the state static (by-design, as it's for isolation).
        // However, there is some static state that we want to preserve, so we need to set it explicitly.
        Type staticStateHelperType = typeof(StaticStateHelper);
        var staticStateHelper = appDomain.CreateInstanceFromAndUnwrap(staticStateHelperType.Assembly.Location, staticStateHelperType.FullName) as StaticStateHelper;
        staticStateHelper?.SetUICulture(CultureInfo.DefaultThreadCurrentUICulture);
    }

    private sealed class StaticStateHelper : MarshalByRefObject
    {
#pragma warning disable CA1822 // Mark members as static - Should not be static for our need
        // The overloads of CreateInstanceAndUnwrap that takes the culture info are actually not setting the culture
        // of the AppDomain but only using this culture for the cast/conversion of the arguments.
        // For the problem reported by vendors, we would only need to set the DefaultThreadCurrentUICulture as it's
        // the culture we want to use for the resx.
        public void SetUICulture(CultureInfo uiCulture) => CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
    }
}

#endif
