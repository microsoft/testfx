// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers;

internal static class DiagnosticDescriptorHelper
{
    public static DiagnosticDescriptor Create(
        string id,
        LocalizableString title,
        LocalizableString messageFormat,
        LocalizableString? description,
        Category category,
        DiagnosticSeverity defaultSeverity,
        bool isEnabledByDefault,
        bool isReportedAtCompilationEnd = false,
        params string[] customTags)
        => new(id, title, messageFormat, category.ToString(), defaultSeverity, isEnabledByDefault, description,
            $"https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/{id.ToLowerInvariant()}",
            CreateCustomTags(isReportedAtCompilationEnd, customTags));

    public static DiagnosticDescriptor WithMessage(this DiagnosticDescriptor diagnosticDescriptor, LocalizableResourceString messageFormat)
        => new(diagnosticDescriptor.Id, diagnosticDescriptor.Title, messageFormat, diagnosticDescriptor.Category, diagnosticDescriptor.DefaultSeverity,
            diagnosticDescriptor.IsEnabledByDefault, diagnosticDescriptor.Description, diagnosticDescriptor.HelpLinkUri, diagnosticDescriptor.CustomTags.ToArray());

    private static string[] CreateCustomTags(bool isReportedAtCompilationEnd, string[] customTags)
    {
        if (!isReportedAtCompilationEnd)
        {
            return customTags;
        }

        string[] tags = new string[customTags.Length + 1];
        if (customTags.Length > 0)
        {
            customTags.CopyTo(tags, 0);
        }

        tags[^1] = WellKnownCustomTags.CompilationEnd;

        return tags;
    }
}
