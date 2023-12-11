// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.Testing.Framework;

namespace Microsoft.Testing.TestInfrastructure;

public static class TestExtensions
{
    public static string ToTargetFrameworksElementContent(this TestArgumentsEntry<string>[] tmfs)
    {
        return tmfs.Select(x => x.Arguments).Aggregate((a, b) => $"{a};{b}");
    }

    public static string PatchCodeWithRegularExpression(this string code, string pattern, string value)
    {
        return Regex.Replace(code, pattern, value);
    }
}
