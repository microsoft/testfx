// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Snapshot of the well-known extension UIDs owned by Microsoft.Testing.Platform.
/// These UIDs are SHA-256-hashed for telemetry, printed verbatim in <c>--info</c> output,
/// and embedded in artifact metadata, so they must stay stable across class renames.
/// If one of these assertions fails because a class was renamed, do NOT update the literal:
/// keep the UID stable (as an explicit string constant) so telemetry and consumers keep working.
/// </summary>
[TestClass]
public sealed class WellKnownExtensionUidTests
{
    [TestMethod]
    public void PlatformCommandLineProvider_HasStableUid()
        => Assert.AreEqual("PlatformCommandLineProvider", new PlatformCommandLineProvider().Uid);

    [TestMethod]
    public void TerminalTestReporterCommandLineOptionsProvider_HasStableUid()
        => Assert.AreEqual("TerminalTestReporterCommandLineOptionsProvider", new TerminalTestReporterCommandLineOptionsProvider().Uid);
}
