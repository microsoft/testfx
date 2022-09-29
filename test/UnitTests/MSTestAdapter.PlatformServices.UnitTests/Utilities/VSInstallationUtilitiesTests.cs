// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
namespace MSTestAdapter.PlatformServices.UnitTests;

using System.Collections.Generic;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

public class VSInstallationUtilitiesTests : TestContainer
{
    public void CheckResolutionPathsDoNotContainPrivateAssembliesPathTest()
    {
        TestSourceHost isolatedHost = new(null, null, null);
        List<string> paths = isolatedHost.GetResolutionPaths(Assembly.GetExecutingAssembly().FullName, true);
        Verify(!paths.Contains(Constants.PublicAssemblies) || paths.Contains(Constants.PrivateAssemblies));
    }
}
#endif
