// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Services;

internal interface ITestSessionContext
{
    SessionUid SessionId { get; }

    CancellationToken CancellationToken { get; }
}
