// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// Thrown for any settings-format error while populating the adapter settings from the runsettings XML. This
/// covers both invalid MSTest settings <em>values</em> (for example an unsupported <c>&lt;Parallelize&gt;</c>
/// <c>Scope</c>/<c>Workers</c>) and <em>structural</em> runsettings errors (for example a bad attribute, an
/// unexpected element, a malformed <c>&lt;AssemblyResolution&gt;</c> block, or a wrong root node).
/// <para>
/// All settings-format errors are handled uniformly: the discovery/execution initialization path
/// (<c>MSTestDiscovererHelpers.InitializeDiscovery</c>) catches this exception, logs the message as an error,
/// and bails out gracefully (discovery reports no tests / execution does not run any test) instead of letting
/// the exception propagate to the host.
/// </para>
/// </summary>
internal sealed class AdapterSettingsException : Exception
{
    internal AdapterSettingsException(string? message)
        : base(message)
    {
    }

    internal AdapterSettingsException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
