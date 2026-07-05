// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// Thrown when the runsettings XML is structurally invalid (for example a bad attribute, an unexpected element,
/// or a wrong root node). This is the platform-agnostic replacement for the settings exception the platform
/// services layer historically surfaced from the VSTest object model. It is intentionally distinct from
/// <see cref="AdapterSettingsException"/>: <see cref="AdapterSettingsException"/> is caught by the discovery
/// initialization path (reported and treated as "no tests"), whereas a structural runsettings error propagates
/// to the host, preserving the original behavior.
/// </summary>
internal sealed class InvalidRunSettingsException : Exception
{
    internal InvalidRunSettingsException(string? message)
        : base(message)
    {
    }
}
