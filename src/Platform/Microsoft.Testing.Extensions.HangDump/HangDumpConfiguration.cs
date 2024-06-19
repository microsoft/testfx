// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class HangDumpConfiguration
{
    public const string PipeName = "TESTINGPLATFORM_HANGDUMP_PIPENAME";
    public const string MutexName = "TESTINGPLATFORM_HANGDUMP_MUTEXNAME";
    public const string MutexNameSuffix = "TESTINGPLATFORM_HANGDUMP_MUTEXNAME_SUFFIX";

    public HangDumpConfiguration(ITestApplicationModuleInfo testApplicationModuleInfo, PipeNameDescription pipeNameDescription, string mutexSuffix)
    {
        PipeNameValue = pipeNameDescription.Name;
        PipeNameKey = $"{PipeName}_{FNV_1aHashHelper.ComputeStringHash(testApplicationModuleInfo.GetCurrentTestApplicationFullPath())}_{mutexSuffix}";
        MutexSuffix = mutexSuffix;
    }

    public string PipeNameKey { get; private set; }

    public string PipeNameValue { get; private set; }

    public string MutexSuffix { get; private set; }
}
