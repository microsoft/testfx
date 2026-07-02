// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal static class GitHubActionsEscaper
{
    /// <summary>
    /// Escapes a value used as the data portion of a GitHub Actions workflow command (the text after the
    /// double-colon). See <see href="https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#example-of-a-debug-message"/>.
    /// </summary>
    public static string EscapeData(string value)
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '%':
                    result.Append("%25");
                    break;
                case '\r':
                    result.Append("%0D");
                    break;
                case '\n':
                    result.Append("%0A");
                    break;
                default:
                    result.Append(c);
                    break;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Escapes a value used as a property of a GitHub Actions workflow command (e.g. the <c>file</c>,
    /// <c>line</c>, <c>col</c> or <c>title</c> of an <c>::error</c> command). In addition to the data
    /// escapes, properties must also escape <c>:</c> and <c>,</c> because those characters delimit the
    /// command syntax. See <see href="https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#example-of-a-debug-message"/>.
    /// </summary>
    public static string EscapeProperty(string value)
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '%':
                    result.Append("%25");
                    break;
                case '\r':
                    result.Append("%0D");
                    break;
                case '\n':
                    result.Append("%0A");
                    break;
                case ':':
                    result.Append("%3A");
                    break;
                case ',':
                    result.Append("%2C");
                    break;
                default:
                    result.Append(c);
                    break;
            }
        }

        return result.ToString();
    }
}
