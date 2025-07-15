// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers;

internal static class DiagnosticDescriptorHelper
{
    public const string CannotFixPropertyKey = "CannotFix";
    public static readonly ImmutableDictionary<string, string?> CannotFixProperties
        = ImmutableDictionary<string, string?>.Empty.Add(CannotFixPropertyKey, null);

    public static DiagnosticDescriptor Create(
        string id,
        LocalizableString title,
        LocalizableString messageFormat,
        LocalizableString? description,
        Category category,
        DiagnosticSeverity defaultSeverity,
        bool isEnabledByDefault,
        bool isReportedAtCompilationEnd = false,
        bool escalateToErrorInRecommended = false,
        bool disableInAllMode = false,
        params string[] customTags)
        => new(id, title, messageFormat, category.ToString(), defaultSeverity, isEnabledByDefault, description,
            $"https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/{id.ToLowerInvariant()}",
            CreateCustomTags(isReportedAtCompilationEnd, escalateToErrorInRecommended, disableInAllMode, customTags));

    public static DiagnosticDescriptor WithMessage(this DiagnosticDescriptor diagnosticDescriptor, LocalizableResourceString messageFormat)
        => new(diagnosticDescriptor.Id, diagnosticDescriptor.Title, messageFormat, diagnosticDescriptor.Category, diagnosticDescriptor.DefaultSeverity,
            diagnosticDescriptor.IsEnabledByDefault, diagnosticDescriptor.Description, diagnosticDescriptor.HelpLinkUri, [.. diagnosticDescriptor.CustomTags]);

    private static string[] CreateCustomTags(bool isReportedAtCompilationEnd, bool escalateToErrorInRecommended, bool disableInAllMode, string[] customTags)
    {
        int extraTagsCount = 0;
        if (isReportedAtCompilationEnd)
        {
            extraTagsCount++;
        }

        if (escalateToErrorInRecommended)
        {
            extraTagsCount++;
        }

        if (disableInAllMode)
        {
            extraTagsCount++;
        }

        if (extraTagsCount == 0)
        {
            return customTags;
        }

        string[] tags = new string[customTags.Length + extraTagsCount];
        if (customTags.Length > 0)
        {
            customTags.CopyTo(tags, 0);
        }

        int index = customTags.Length;
        if (isReportedAtCompilationEnd)
        {
            tags[index++] = WellKnownCustomTags.CompilationEnd;
        }

        if (escalateToErrorInRecommended)
        {
            tags[index++] = WellKnownCustomTags.EscalateToErrorInRecommended;
        }

        if (disableInAllMode)
        {
            tags[index] = WellKnownCustomTags.DisableInAllMode;
        }

        return tags;
    }
}
