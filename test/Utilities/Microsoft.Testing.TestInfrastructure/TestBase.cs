// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Framework;

namespace Microsoft.Testing.TestInfrastructure;

public abstract class TestBase
{
    protected TestBase(ITestExecutionContext testExecutionContext)
    {
        TestsRunWatchDog.AddTestRun(testExecutionContext.TestInfo.StableUid);
    }

    public static TestArgumentsEntry<string>[] NET_Tfms { get; } = new TestArgumentsEntry<string>[] { new("net8.0", "net8.0"), new("net7.0", "net7.0"), new("net6.0", "net6.0") };

    public static TestArgumentsEntry<string> MainNET_Tfm { get; } = NET_Tfms[0];

    public static TestArgumentsEntry<string>[] NETFramework_Tfms { get; } = new TestArgumentsEntry<string>[] { new("net462", "net462") };

    public static TestArgumentsEntry<string> MainNETFramework_Tfm => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? NETFramework_Tfms[0] : throw new InvalidOperationException(".NET Framework supported only in Windows");

    public static TestArgumentsEntry<string>[] All_Tfms => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? NET_Tfms.Concat(NETFramework_Tfms).ToArray() : NET_Tfms;
}
