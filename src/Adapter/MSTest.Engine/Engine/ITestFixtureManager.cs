// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

internal interface ITestFixtureManager
{
    public void RegisterFixture<TFixture>(string fixtureId, Func<TFixture> factory)
        where TFixture : notnull;

    public void RegisterFixture<TFixture>(string fixtureId, Func<Task<TFixture>> asyncFactory)
        where TFixture : notnull;

    public void TryRegisterFixture<TFixture>(string fixtureId, Func<TFixture> factory)
        where TFixture : notnull;

    public void TryRegisterFixture<TFixture>(string fixtureId, Func<Task<TFixture>> asyncFactory)
        where TFixture : notnull;

    Task<TFixture> GetFixtureAsync<TFixture>(string fixtureId)
        where TFixture : notnull;
}
