// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

/// <summary>
/// An optional test framework capability that allows the test framework to provide a custom help message to the platform.
/// If the message is null or if the capability is not present, the platform will use its default help message.
///
/// This capability implementation allows to abstract away the various conditions that the test framework may need to consider
/// to decide whether or not the custom help message should be displayed.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IHelpMessageOwnerCapability : ITestFrameworkCapability
{
    /// <summary>
    /// Process the help message and return the message to be displayed.
    /// </summary>
    /// <returns>
    /// The help message to be displayed or <c>null</c> to use the default help message.
    /// </returns>
    Task<string?> GetHelpMessageAsync();
}