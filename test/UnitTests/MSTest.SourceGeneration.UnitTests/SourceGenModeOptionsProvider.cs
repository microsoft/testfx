// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

/// <summary>
/// An <see cref="AnalyzerConfigOptionsProvider"/> whose global options expose a fixed MSTest source generation
/// mode, or no mode at all when it is <see langword="null"/>.
/// </summary>
internal sealed class SourceGenModeOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly SourceGenModeOptions _options;

    public SourceGenModeOptionsProvider(string? mode)
        => _options = new SourceGenModeOptions(mode);

    public override AnalyzerConfigOptions GlobalOptions => _options;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;

    private sealed class SourceGenModeOptions : AnalyzerConfigOptions
    {
        private readonly string? _mode;

        public SourceGenModeOptions(string? mode)
            => _mode = mode;

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            value = key == "build_property.MSTestSourceGenMode" ? _mode : null;
            return value is not null;
        }
    }
}
