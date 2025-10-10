// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public class VSInstallationUtilitiesTests : TestContainer
{
    public void CheckResolutionPathsDoNotContainPrivateAssembliesPathTest()
    {
        TestSourceHost isolatedHost = new(null!, null, null);
        List<string> paths = isolatedHost.GetResolutionPaths(Assembly.GetExecutingAssembly().FullName, true);
        (!paths.Contains(EngineConstants.PublicAssemblies) || paths.Contains(EngineConstants.PrivateAssemblies)).Should().BeTrue();
    }
}
#endif
