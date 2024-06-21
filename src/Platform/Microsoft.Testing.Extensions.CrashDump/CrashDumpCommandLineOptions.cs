// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if USE_TRX_NAMESPACE
namespace Microsoft.Testing.Extensions.TrxReport;
#else
namespace Microsoft.Testing.Extensions.Diagnostics;
#endif

internal static class CrashDumpCommandLineOptions
{
    public const string CrashDumpOptionName = "crashdump";
    public const string CrashDumpFileNameOptionName = "crashdump-filename";
    public const string CrashDumpTypeOptionName = "crashdump-type";
}
