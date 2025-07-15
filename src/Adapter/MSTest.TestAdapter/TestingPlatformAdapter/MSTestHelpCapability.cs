// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Text;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Example implementation of custom help capability for MSTest.
/// This demonstrates how test frameworks can provide customized help output.
/// </summary>
internal sealed class MSTestHelpCapability : IHelpMessageOwnerCapability
{
    private readonly IPlatformInformation _platformInformation;

    public MSTestHelpCapability(IPlatformInformation platformInformation) => _platformInformation = platformInformation;

    public Task<string?> GetHelpMessageAsync()
    {
        StringBuilder helpMessage = new();
        
        // Header with MSTest branding
        helpMessage.AppendLine($"MSTest v{MSTestVersion.SemanticVersion} - Microsoft Test Framework");
        
        if (_platformInformation.BuildDate is { } buildDate)
        {
            helpMessage.AppendLine($"Build Date: {buildDate.UtcDateTime.ToShortDateString()} UTC");
        }
        
        helpMessage.AppendLine();
        helpMessage.AppendLine("Usage: dotnet test [options]");
        helpMessage.AppendLine("       <testapp>.exe [options]");
        helpMessage.AppendLine();
        helpMessage.AppendLine("Description:");
        helpMessage.AppendLine("  Execute MSTest unit tests using the Microsoft Testing Platform.");
        helpMessage.AppendLine();
        
        // Platform options
        helpMessage.AppendLine("Platform Options:");
        helpMessage.AppendLine("  --help                        Show this help message");
        helpMessage.AppendLine("  --info                        Display application information");
        helpMessage.AppendLine("  --results-directory <path>    Directory for test results");
        helpMessage.AppendLine("  --diagnostic                  Enable diagnostic logging");
        helpMessage.AppendLine("  --list-tests                  List available tests");
        helpMessage.AppendLine();
        
        // MSTest-specific options
        helpMessage.AppendLine("MSTest Options:");
        helpMessage.AppendLine("  --filter <expression>        Filter tests using test case properties");
        helpMessage.AppendLine("  --settings <file>             Run settings file (.runsettings)");
        helpMessage.AppendLine("  --test-parameter <key=value>  Override test run parameters");
        helpMessage.AppendLine();
        
        // Examples
        helpMessage.AppendLine("Examples:");
        helpMessage.AppendLine("  dotnet test --filter \"TestCategory=Unit\"");
        helpMessage.AppendLine("  dotnet test --settings my.runsettings");
        helpMessage.AppendLine("  dotnet test --test-parameter \"ConnectionString=test_db\"");
        helpMessage.AppendLine("  dotnet test --results-directory ./TestResults --diagnostic");
        helpMessage.AppendLine();
        
        // Footer
        helpMessage.AppendLine("For more information:");
        helpMessage.AppendLine("  https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-intro");
        helpMessage.AppendLine("  https://learn.microsoft.com/visualstudio/test/mstest-api-overview");

        return Task.FromResult<string?>(helpMessage.ToString());
    }
}
#endif