// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// All the properties of this class should be non static.
/// At the moment are static because we need to share them between perclass/id fixtures and
/// it's not supported at the moment.
/// </summary>
public abstract class BaseAcceptanceTests : TestBase
{
    protected BaseAcceptanceTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        AcceptanceFixture = acceptanceFixture;
    }

    public AcceptanceFixture AcceptanceFixture { get; }

    public static async Task<AssetManager> CopyAssetAsync(string assetName)
    {
        var assetManager = new AssetManager(assetName);
        await assetManager.CopyAssets();
        return assetManager;
    }
}
