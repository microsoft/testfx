// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public static class AcceptanceFixture
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
        => Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");
}
