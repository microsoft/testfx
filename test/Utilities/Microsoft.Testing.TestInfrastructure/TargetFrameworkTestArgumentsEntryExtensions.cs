// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;

namespace Microsoft.Testing.TestInfrastructure;

public static class TargetFrameworkTestArgumentsEntryExtensions
{
    public static string ToTargetFrameworksElementContent(this TestArgumentsEntry<string>[] targetFrameworksEntries)
        => string.Join(";", targetFrameworksEntries.Select(x => x.Arguments));
}
