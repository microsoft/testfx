// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.TestHost;

public interface ITestSessionLifetimeHandler : ITestHostExtension
{
    Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken);

    Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken);
}
