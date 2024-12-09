// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
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
    internal static XmlUtilities XmlUtilities
    {
        get => field ??= new XmlUtilities();
        set;
    }

    /// <summary>
    /// Set the target framework for app domain setup if target framework of dll is > 4.5.
    /// </summary>
    /// <param name="setup">AppdomainSetup for app domain creation.</param>
    /// <param name="frameworkVersionString">The target framework version of the test source.</param>
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here.")]
    internal static void SetAppDomainFrameworkVersionBasedOnTestSource(AppDomainSetup setup, string frameworkVersionString)
    {
        if (GetTargetFrameworkVersionFromVersionString(frameworkVersionString).CompareTo(Version45) > 0)
        {
            PropertyInfo pInfo = typeof(AppDomainSetup).GetProperty(Constants.TargetFrameworkName);
            pInfo?.SetValue(setup, frameworkVersionString, null);
        }
    }

    /// <summary>
    /// Get target framework version string from the given dll.
    /// </summary>
    /// <param name="testSourcePath">
    /// The path of the dll.
    /// </param>
    /// <returns>
    /// Framework string
    /// TODO: Need to add components/E2E tests to cover these scenarios.
    /// </returns>
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
    internal static string GetTargetFrameworkVersionString(string testSourcePath)
    {
        AppDomainSetup appDomainSetup = new()
        {
            LoaderOptimization = LoaderOptimization.MultiDomainHost,
        };

        SetConfigurationFile(appDomainSetup, new DeploymentUtility().GetConfigFile(testSourcePath));

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
                null);

            string targetFramework = assemblyLoadWorker.GetTargetFrameworkVersionStringFromPath(testSourcePath, out string? errorMessage);

            if (errorMessage is not null)
            {
                EqtTrace.Error(errorMessage);
            }

            return targetFramework;
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

        return string.Empty;
    }

    /// <summary>
    /// Set configuration file on the parameter appDomain.
    /// </summary>
    /// <param name="appDomainSetup"> The app Domain Setup. </param>
    /// <param name="testSourceConfigFile"> The test Source Config File. </param>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    internal static void SetConfigurationFile(AppDomainSetup appDomainSetup, string? testSourceConfigFile)
    {
        if (StringEx.IsNullOrEmpty(testSourceConfigFile))
        {
            // Use the current domains configuration setting.
            appDomainSetup.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            return;
        }

        if (EqtTrace.IsInfoEnabled)
        {
            EqtTrace.Info("UnitTestAdapter: Using configuration file {0} to setup appdomain for test source {1}.", testSourceConfigFile, Path.GetFileNameWithoutExtension(testSourceConfigFile));
        }

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
            if (EqtTrace.IsErrorEnabled)
            {
                EqtTrace.Error("Exception hit while adding binding redirects to test source config file. Exception : {0}", ex);
            }
        }
    }

    internal static object CreateInstance(AppDomain appDomain, Type type, object?[]? arguments)
    {
        string? typeAssemblyLocation = type.Assembly.Location;
        string? fullFilePath = typeAssemblyLocation == null ? null : Path.Combine(appDomain.SetupInformation.ApplicationBase, Path.GetFileName(typeAssemblyLocation));

        EnsureAppDomainUsesCorrectUICulture(appDomain, CultureInfo.DefaultThreadCurrentUICulture);

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
    /// Get the Version for the target framework version string.
    /// </summary>
    /// <param name="version">Target framework string.</param>
    /// <returns>Framework Version.</returns>
    internal static Version GetTargetFrameworkVersionFromVersionString(string version)
    {
        try
        {
            if (version.Length > Constants.DotNetFrameWorkStringPrefix.Length + 1)
            {
                string versionPart = version.Substring(Constants.DotNetFrameWorkStringPrefix.Length + 1);
                return new Version(versionPart);
            }
        }
        catch (FormatException ex)
        {
            // if the version is ".NETPortable,Version=v4.5,Profile=Profile259", then above code will throw exception.
            EqtTrace.Warning($"AppDomainUtilities.GetTargetFrameworkVersionFromVersionString: Could not create version object from version string '{version}' due to error '{ex.Message}':");
        }

        return DefaultVersion;
    }

    internal /* for testing purposes */ static void EnsureAppDomainUsesCorrectUICulture(AppDomain appDomain, CultureInfo uiCulture)
    {
        // AppDomain is not preserving the culture info. So we need to set it explicitly.
        // The overloads of CreateInstanceAndUnwrap that takes the culture info are actually not setting the culture
        // of the AppDomain but only using this culture for the cast/conversion of the arguments.
        // For the problem reported by vendors, we would only need to set the DefaultThreadCurrentUICulture as it's
        // the culture we want to use for the resx.
        Type cultureHelperType = typeof(AppDomainCultureHelper);
        var appDomainCultureHelper = appDomain.CreateInstanceFromAndUnwrap(cultureHelperType.Assembly.Location, cultureHelperType.FullName) as AppDomainCultureHelper;
        appDomainCultureHelper?.SetUICulture(uiCulture);
    }

    private sealed class AppDomainCultureHelper : MarshalByRefObject
    {
#pragma warning disable CA1822 // Mark members as static - Should not be static for our need
        public void SetUICulture(CultureInfo uiCulture) => CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
    }
}

#endif
