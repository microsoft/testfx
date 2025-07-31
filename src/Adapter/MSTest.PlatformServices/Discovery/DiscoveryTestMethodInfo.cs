// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MSTest.PlatformServices.Execution;

namespace MSTest.PlatformServices.Discovery;

internal readonly struct DiscoveryTestMethodInfo
{
    public DiscoveryTestMethodInfo(MethodInfo methodInfo, TestClassInfo parent)
    {
        MethodInfo = methodInfo;
        Parent = parent;
    }

    public MethodInfo MethodInfo { get; }

    public TestClassInfo Parent { get; }
}
