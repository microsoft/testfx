// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestFixture(TestFixtureSharingStrategy.PerTestApplication)]
public sealed class AcceptanceFixture : IDisposable
{
    public TempDirectory NuGetGlobalPackagesFolder { get; } = new(".packages");

    public void Dispose()
        => NuGetGlobalPackagesFolder.Dispose();
}
