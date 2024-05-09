// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal static class KnownNonTestMethods
{
    public static HashSet<string> Methods { get; } = [nameof(object.Equals), nameof(object.GetHashCode), nameof(object.GetType), nameof(object.ReferenceEquals), nameof(object.ToString)];
}
