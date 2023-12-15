// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;

namespace Microsoft.Testing.TestInfrastructure;

public static class SourceCodeExtensions
{
    public static string PatchCodeWithReplace(this string code, string pattern, string value)
        => code.Replace(pattern, value);

    public static string PatchTargetFrameworks(this string code, params TestArgumentsEntry<string>[] targetFrameworks)
        => PatchCodeWithReplace(code, "$TargetFrameworks$", targetFrameworks.ToMSBuildTargetFrameworks());
}
