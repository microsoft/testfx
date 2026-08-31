// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.Helpers;

/// <summary>
/// The string values of <c>Microsoft.VisualStudio.TestTools.UnitTesting.WellKnownResources</c>. The analyzer
/// project does not reference the test framework, so these are duplicated here to compare against the resolved
/// resource key of an existing <c>[ResourceLock]</c>. They MUST stay in sync with <c>WellKnownResources</c>.
/// </summary>
internal static class WellKnownResourceKeys
{
    /// <summary>Mirror of <c>WellKnownResources.CurrentDirectory</c>.</summary>
    public const string CurrentDirectory = "System.Environment.CurrentDirectory";

    /// <summary>Mirror of <c>WellKnownResources.EnvironmentVariables</c>.</summary>
    public const string EnvironmentVariables = "System.Environment.Variables";

    /// <summary>Mirror of <c>WellKnownResources.Console</c>.</summary>
    public const string Console = "System.Console";
}
