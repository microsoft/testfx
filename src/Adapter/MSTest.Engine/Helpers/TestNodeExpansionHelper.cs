// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework.Helpers;

internal static class TestNodeExpansionHelper
{
    public static TestNodeUid GenerateStableUid(TestNodeUid testNodeUid, string dataId)
        => new($"{testNodeUid.Value} {dataId}");

    public static string GenerateDisplayName(string displayName, string dataId)
        => $"{displayName} {dataId}";
}
