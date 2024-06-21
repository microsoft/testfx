// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.VSTestBridge;

internal static class VSTestTestNodeProperties
{
    internal const string Prefix = "vstest.";
    public const string OriginalExecutorUriPropertyName = "vstest.original-executor-uri";

    public static class TestNode
    {
        public const string UidPropertyName = "vstest.testnode.uid";
    }
}
