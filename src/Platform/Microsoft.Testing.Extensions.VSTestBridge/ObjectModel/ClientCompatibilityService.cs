// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

internal readonly struct ClientCompatibilityService
{
    private readonly string _clientName;
    private readonly Version? _clientVersion;

    // Visual Studio Test Explorer used to use the location.* properties (location.file/location.line-start and
    // location.type/location.method) only for non-vstestProvider. And for vstestProvider, it used the legacy
    // vstest.TestCase.* key-value-pair properties (vstest.TestCase.CodeFilePath/LineNumber and
    // vstest.TestCase.ManagedType/ManagedMethod).
    // This behavior changed and we now always support location.* both for vstestProvider and non-vstestProvider.
    // However, we still want to send the vstest.TestCase.* if the client doesn't respect location.*
    // Note that this refers to VS 2026, as the fix here was not backported to VS 2022.
    // Internal VS PR: https://devdiv.visualstudio.com/DevDiv/_git/VSUnitTesting/pullrequest/630376
    private static readonly Version VersionRespectingLocationForVSTestProvider = new("1.0.1");

    public ClientCompatibilityService(IClientInfo clientInfo)
    {
        _clientName = clientInfo.Id;
        _ = Version.TryParse(clientInfo.Version, out _clientVersion);
    }

    public bool UseVSTestTestCaseLocationProperties
        => _clientName == WellKnownClients.VisualStudio &&
            _clientVersion is not null &&
            _clientVersion < VersionRespectingLocationForVSTestProvider;
}
