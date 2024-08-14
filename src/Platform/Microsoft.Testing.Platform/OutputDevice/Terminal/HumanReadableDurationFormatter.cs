// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal static class HumanReadableDurationFormatter
{
    public static void Append(ITerminal terminal, TimeSpan duration, bool wrapInParentheses = true)
    {
        bool hasParentValue = false;

        if (wrapInParentheses)
        {
            terminal.Append('(');
        }

        if (duration.Days > 0)
        {
            terminal.Append($"{duration.Days}d");
            hasParentValue = true;
        }

        if (duration.Hours > 0 || hasParentValue)
        {
            terminal.Append($"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Hours.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : duration.Hours.ToString(CultureInfo.InvariantCulture))}h");
            hasParentValue = true;
        }

        if (duration.Minutes > 0 || hasParentValue)
        {
            terminal.Append($"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : duration.Minutes.ToString(CultureInfo.InvariantCulture))}m");
            hasParentValue = true;
        }

        if (duration.Seconds > 0 || hasParentValue)
        {
            terminal.Append($"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : duration.Seconds.ToString(CultureInfo.InvariantCulture))}s");
            hasParentValue = true;
        }

        if (duration.Milliseconds >= 0 || hasParentValue)
        {
            terminal.Append($"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Milliseconds.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0') : duration.Milliseconds.ToString(CultureInfo.InvariantCulture))}ms");
        }

        if (wrapInParentheses)
        {
            terminal.Append(')');
        }
    }

    public static string Render(TimeSpan duration, bool wrapInParentheses = true, bool showMilliseconds = false)
    {
        bool hasParentValue = false;

        var stringBuilder = new StringBuilder();

        if (wrapInParentheses)
        {
            stringBuilder.Append('(');
        }

        if (duration.Days > 0)
        {
            stringBuilder.Append(CultureInfo.CurrentCulture, $"{duration.Days}d");
            hasParentValue = true;
        }

        if (duration.Hours > 0 || hasParentValue)
        {
            stringBuilder.Append(CultureInfo.CurrentCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Hours.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : duration.Hours.ToString(CultureInfo.InvariantCulture))}h");
            hasParentValue = true;
        }

        if (duration.Minutes > 0 || hasParentValue)
        {
            stringBuilder.Append(CultureInfo.CurrentCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : duration.Minutes.ToString(CultureInfo.InvariantCulture))}m");
            hasParentValue = true;
        }

        if (duration.Seconds > 0 || hasParentValue || !showMilliseconds)
        {
            stringBuilder.Append(CultureInfo.CurrentCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : duration.Seconds.ToString(CultureInfo.InvariantCulture))}s");
            hasParentValue = true;
        }

        if (showMilliseconds)
        {
            if (duration.Milliseconds >= 0 || hasParentValue)
            {
                stringBuilder.Append(CultureInfo.CurrentCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? duration.Milliseconds.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0') : duration.Milliseconds.ToString(CultureInfo.InvariantCulture))}ms");
            }
        }

        if (wrapInParentheses)
        {
            stringBuilder.Append(')');
        }

        return stringBuilder.ToString();
    }
}
