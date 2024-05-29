// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

/// <summary>
/// An optional test framework capability that allows the test framework to provide the banner message to the platform.
/// If the message is null or if the capability is not present, the platform will use its default banner message.
///
/// This capability implementation allows to abstract away the various conditions that the test framework may need to consider
/// to decide whether or not the banner message should be displayed.
/// </summary>
[Experimental("TAEXP001")]
public interface IBannerMessageOwnerCapability : ITestFrameworkCapability
{
    /// <summary>
    /// Process the banner message and return the message to be displayed.
    /// </summary>
    /// <returns>
    /// The banner message to be displayed or <c>null</c> to use the default banner message.
    /// </returns>
    Task<string?> GetBannerMessageAsync();
}
