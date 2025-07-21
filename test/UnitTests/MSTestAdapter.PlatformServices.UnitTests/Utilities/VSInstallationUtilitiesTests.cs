// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public class VSInstallationUtilitiesTests : TestContainer
{
    public void CheckResolutionPathsDoNotContainPrivateAssembliesPathTest()
    {
        TestSourceHost isolatedHost = new(null!, null, null, new Mock<IAdapterTraceLogger>().Object);
        List<string> paths = isolatedHost.GetResolutionPaths(Assembly.GetExecutingAssembly().FullName, true);
        Verify(!paths.Contains(EngineConstants.PublicAssemblies) || paths.Contains(EngineConstants.PrivateAssemblies));
    }
}
#endif
