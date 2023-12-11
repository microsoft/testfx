// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestFixture(TestFixtureSharingStrategy.PerTestApplication)]
public sealed class AcceptanceFixture : IDisposable
{
    private readonly TempDirectory _nuGetGlobalPackagesFolder = new("NugetCache");

    public string NuGetGlobalPackagesFolder
        => _nuGetGlobalPackagesFolder.DirectoryPath;

    public void Dispose()
        => _nuGetGlobalPackagesFolder.Dispose();
}
