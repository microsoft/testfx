// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestFixture(TestFixtureSharingStrategy.PerTestApplication)]
public sealed class AcceptanceFixture : IDisposable
{
    private readonly TempDirectory _nugetGlobalPackagesFolder = new("NugetCache");

    public string NugetGlobalPackagesFolder
        => _nugetGlobalPackagesFolder.DirectoryPath;

    public void Dispose()
        => _nugetGlobalPackagesFolder.Dispose();
}
