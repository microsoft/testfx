// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Bazel.ReferenceAliasFixtures;

public static class SharedType
{
    public static string Value => "second";

    internal static string InternalValue => "second-internal";
}
