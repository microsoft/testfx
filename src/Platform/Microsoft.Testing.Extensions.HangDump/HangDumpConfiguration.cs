// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class HangDumpConfiguration
{
    // HangDump extension sets to environment variables.
    // 1. TESTINGPLATFORM_HANGDUMP_NAMEDPIPE_NAME_SUFFIX
    // 2. TESTINGPLATFORM_HANGDUMP_PIPENAME_<hash>_<namedPipeSuffix> (hash is dependent on application path, namedPipeSuffix is a random guid and is the same as the value of TESTINGPLATFORM_HANGDUMP_NAMEDPIPE_NAME_SUFFIX)
    // The value of TESTINGPLATFORM_HANGDUMP_PIPENAME_<hash>_<namedPipeSuffix> represents the name of the named pipe.
    public const string PipeNameEnvironmentVariableNamePrefix = "TESTINGPLATFORM_HANGDUMP_PIPENAME";
    public const string NamedPipeNameSuffixEnvironmentVariable = "TESTINGPLATFORM_HANGDUMP_NAMEDPIPE_NAME_SUFFIX";

    public HangDumpConfiguration(ITestApplicationModuleInfo testApplicationModuleInfo, PipeNameDescription pipeNameDescription, string namedPipeSuffix)
    {
        PipeNameValue = pipeNameDescription.Name;
        PipeNameKey = $"{PipeNameEnvironmentVariableNamePrefix}_{FNV_1aHashHelper.ComputeStringHash(testApplicationModuleInfo.GetCurrentTestApplicationFullPath())}_{namedPipeSuffix}";
        NamedPipeSuffix = namedPipeSuffix;
    }

    public string PipeNameKey { get; }

    public string PipeNameValue { get; }

    public string NamedPipeSuffix { get; }
}
