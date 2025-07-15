// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

/// <summary>
/// An optional test framework capability that allows the test framework to provide a custom help message to the platform.
/// If the capability is not present or if the capability chooses not to display custom help, the platform will use its default help message.
///
/// This capability implementation allows to abstract away the various conditions that the test framework may need to consider
/// to decide whether or not the custom help message should be displayed.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IHelpMessageOwnerCapability : ITestFrameworkCapability
{
    /// <summary>
    /// Display the custom help message using the provided output device and command line information.
    /// </summary>
    /// <param name="outputDevice">The output device to use for displaying help content.</param>
    /// <param name="systemCommandLineOptions">Collection of system command line options available in the platform.</param>
    /// <param name="extensionsCommandLineOptions">Collection of extension command line options available in the platform.</param>
    /// <returns>
    /// A task that represents the asynchronous display operation. Returns <c>true</c> if custom help was displayed, 
    /// <c>false</c> to use the default platform help message.
    /// </returns>
    Task<bool> DisplayHelpAsync(IOutputDevice outputDevice, 
        IReadOnlyCollection<(IExtension Extension, IReadOnlyCollection<CommandLineOption> Options)> systemCommandLineOptions,
        IReadOnlyCollection<(IExtension Extension, IReadOnlyCollection<CommandLineOption> Options)> extensionsCommandLineOptions);
}