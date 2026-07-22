// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal static partial class CommandLineOptionsValidator
{
    private static string ToTrimmedString(this StringBuilder stringBuilder)
    {
        // Trim trailing CR/LF characters directly from the StringBuilder to avoid extra allocations
        while (stringBuilder.Length > 0 && stringBuilder[stringBuilder.Length - 1] is '\r' or '\n')
        {
            stringBuilder.Length--;
        }

        return stringBuilder.ToString();
    }

    private static ValidationResult AddCommandLine(CommandLineParseResult parseResult, ValidationResult result)
        => result.IsValid ? result : InvalidWithCommandLine(parseResult, result.ErrorMessage);

    private static ValidationResult InvalidWithCommandLine(CommandLineParseResult parseResult, string errorMessage)
    {
        errorMessage = parseResult.RedactError(errorMessage);
        if (parseResult.CommandLine.Length == 0)
        {
            return ValidationResult.Invalid(errorMessage);
        }

        var stringBuilder = new StringBuilder(errorMessage);
        stringBuilder.AppendLine();
        stringBuilder.AppendFormat(CultureInfo.InvariantCulture, PlatformResources.CommandLineValidationCommandLine, parseResult.CommandLine);
        return ValidationResult.Invalid(stringBuilder.ToString());
    }
}
