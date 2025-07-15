// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace TestingPlatformExplorer.TestingFramework;

/// <summary>
/// Example implementation of custom help capability for a test framework.
/// This demonstrates how test frameworks can provide customized help output.
/// </summary>
internal sealed class TestingFrameworkHelpCapability : IHelpMessageOwnerCapability
{
    private readonly IPlatformInformation _platformInformation;

    public TestingFrameworkHelpCapability(IPlatformInformation platformInformation) => _platformInformation = platformInformation;

    public async Task<bool> DisplayHelpAsync(IOutputDevice outputDevice, 
        IReadOnlyCollection<(IExtension Extension, IReadOnlyCollection<CommandLineOption> Options)> systemCommandLineOptions,
        IReadOnlyCollection<(IExtension Extension, IReadOnlyCollection<CommandLineOption> Options)> extensionsCommandLineOptions)
    {
        StringBuilder helpMessage = new();
        
        // Header with TestingFramework branding
        helpMessage.AppendLine("TestingFramework v1.0.0 - Example Test Framework");
        
        if (_platformInformation.BuildDate is { } buildDate)
        {
            helpMessage.AppendLine($"Build Date: {buildDate.UtcDateTime.ToShortDateString()} UTC");
        }
        
        helpMessage.AppendLine();
        helpMessage.AppendLine("Usage: dotnet run [options]");
        helpMessage.AppendLine("       TestingPlatformExplorer.exe [options]");
        helpMessage.AppendLine();
        helpMessage.AppendLine("Description:");
        helpMessage.AppendLine("  Execute tests using the example Testing Framework with Microsoft Testing Platform.");
        helpMessage.AppendLine();

        // Display system options
        if (systemCommandLineOptions.Any())
        {
            helpMessage.AppendLine("Platform Options:");
            foreach (var (extension, options) in systemCommandLineOptions)
            {
                foreach (var option in options.Where(o => !o.IsHidden).OrderBy(o => o.Name))
                {
                    helpMessage.AppendLine($"  --{option.Name,-25} {option.Description}");
                }
            }
            helpMessage.AppendLine();
        }

        // Display extension options
        if (extensionsCommandLineOptions.Any())
        {
            helpMessage.AppendLine("Extension Options:");
            foreach (var (extension, options) in extensionsCommandLineOptions)
            {
                foreach (var option in options.Where(o => !o.IsHidden).OrderBy(o => o.Name))
                {
                    helpMessage.AppendLine($"  --{option.Name,-25} {option.Description}");
                }
            }
            helpMessage.AppendLine();
        }
        
        // Examples
        helpMessage.AppendLine("Examples:");
        helpMessage.AppendLine("  dotnet run --custom-option value");
        helpMessage.AppendLine("  dotnet run --results-directory ./TestResults --diagnostic");
        helpMessage.AppendLine();
        
        // Footer
        helpMessage.AppendLine("For more information:");
        helpMessage.AppendLine("  https://github.com/microsoft/testfx");

        await outputDevice.DisplayAsync(this, new TextOutputDeviceData(helpMessage.ToString())).ConfigureAwait(false);
        return true; // Indicate that we displayed custom help
    }