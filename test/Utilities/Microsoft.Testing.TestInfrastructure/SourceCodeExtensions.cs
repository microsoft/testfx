// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Microsoft.Testing.TestInfrastructure;

public static class SourceCodeExtensions
{
    public static string PatchCodeWithRegularExpression(this string code, string pattern, string value)
        => Regex.Replace(code, pattern, value);
}
