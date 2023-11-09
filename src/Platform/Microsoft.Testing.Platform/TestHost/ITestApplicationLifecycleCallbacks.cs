// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHost;

public interface ITestApplicationLifecycleCallbacks : ITestHostExtension
{
    Task BeforeRunAsync(CancellationToken cancellationToken);

    Task AfterRunAsync(int exitCode, CancellationToken cancellation);
}
