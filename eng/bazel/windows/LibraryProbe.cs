// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;

namespace TestFx.BazelValidation;

internal static class LibraryProbe
{
    public static void Main()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Windows library probe must execute on Windows.");
        }

        List<Assembly> sourceAssemblies = [];
        List<Assembly> runtimeAssemblies = [typeof(object).Assembly];
        string scenario;
#if WINDOWS_PACKAGED_PROBE
        scenario = "packaged";
        Assembly packagedApp = typeof(Microsoft.Testing.Extensions.PackagedAppExtensions).Assembly;
        Type deployer = packagedApp.GetType("Microsoft.Testing.Extensions.PackagedApp.PackageDeployer", throwOnError: true)!;
        bool hasWinRtRegistration = deployer.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(method => method.Name == "RegisterAsync"
                && method.GetParameters() is { Length: 4 } parameters
                && parameters[1].ParameterType == typeof(string));
        if (!hasWinRtRegistration)
        {
            throw new InvalidOperationException("The Windows PackagedApp library is missing its WinRT registration path.");
        }

        if (!Windows.Foundation.Metadata.ApiInformation.IsMethodPresent(
            "Windows.Management.Deployment.PackageManager", "RegisterPackageByUriAsync"))
        {
            throw new InvalidOperationException("The required Windows PackageManager API is not available through the loaded projection.");
        }

        sourceAssemblies.Add(packagedApp);
        sourceAssemblies.Add(typeof(Microsoft.Testing.Platform.Builder.ITestApplicationBuilder).Assembly);
        runtimeAssemblies.Add(typeof(Windows.Foundation.Metadata.ApiInformation).Assembly);
        runtimeAssemblies.Add(Assembly.Load("WinRT.Runtime"));
#elif WINDOWS_DESKTOP_PROBE
        scenario = "desktop";
        Type windowTest = typeof(Microsoft.VisualStudio.TestTools.UnitTesting.Windows.UIAutomation.WindowTest);
        Type automationElement = windowTest.GetProperty("MainWindow")!.PropertyType;
        if (automationElement != typeof(System.Windows.Automation.AutomationElement))
        {
            throw new InvalidOperationException("WindowTest.MainWindow did not bind to the real WPF UI Automation type.");
        }

        Assembly automation = automationElement.Assembly;
        if (automation.IsDefined(typeof(ReferenceAssemblyAttribute)))
        {
            throw new InvalidOperationException("A WPF reference assembly was loaded as a runtime implementation.");
        }

        sourceAssemblies.Add(windowTest.Assembly);
        sourceAssemblies.Add(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext).Assembly);
        sourceAssemblies.Add(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.Assert).Assembly);
        runtimeAssemblies.Add(automation);
#elif WINDOWS_UWP_PROBE || WINDOWS_WINUI_PROBE
#if WINDOWS_UWP_PROBE
        scenario = "uwp";
#else
        scenario = "winui";
#endif
        Assembly extensions = typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext).Assembly;
        Type uiMethod = extensions.GetType(
            "Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer.UITestMethodAttribute", throwOnError: true)!;
        if (!typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute).IsAssignableFrom(uiMethod))
        {
            throw new InvalidOperationException("The Windows flavor did not expose the UI test method attribute.");
        }

        if (extensions.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.DeploymentItemAttribute") is not null)
        {
            throw new InvalidOperationException("A portable framework extensions flavor was loaded instead of the Windows flavor.");
        }

        sourceAssemblies.Add(extensions);
        sourceAssemblies.Add(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.Assert).Assembly);
        sourceAssemblies.Add(Assembly.Load("MSTestAdapter.PlatformServices"));
        sourceAssemblies.Add(Assembly.Load("MSTest.TestAdapter"));
#else
#error A Windows library probe scenario must be selected.
#endif
        foreach (Assembly assembly in sourceAssemblies)
        {
            if (!assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Any(attribute => attribute.Key == "TestFx.BuildSystem" && attribute.Value == "Bazel"))
            {
                throw new InvalidOperationException($"'{assembly.FullName}' is not a native Bazel source build.");
            }
        }

        Console.WriteLine("BAZEL_WINDOWS_PROBE: " + JsonSerializer.Serialize(new
        {
            Scenario = scenario,
            Runtime = Environment.Version.ToString(),
            BaseDirectory = AppContext.BaseDirectory,
            SourceAssemblies = sourceAssemblies.Select(Describe),
            RuntimeAssemblies = runtimeAssemblies.Select(Describe),
        }));
    }

    private static object Describe(Assembly assembly)
        => new
        {
            Name = assembly.GetName().Name,
            Path = assembly.Location,
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
            Framework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName,
            Platform = assembly.GetCustomAttribute<TargetPlatformAttribute>()?.PlatformName,
            SupportedOS = assembly.GetCustomAttribute<SupportedOSPlatformAttribute>()?.PlatformName,
            Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            PublicKeyToken = Convert.ToHexString(assembly.GetName().GetPublicKeyToken() ?? []).ToLowerInvariant(),
        };
}
