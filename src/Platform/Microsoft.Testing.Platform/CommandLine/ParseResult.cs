// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class CommandLineParseResult(string? toolName, IReadOnlyList<OptionRecord> options, IReadOnlyList<string> errors, IReadOnlyList<string> originalArguments) : IEquatable<CommandLineParseResult>
{
    public const char OptionPrefix = '-';

    public static CommandLineParseResult Empty => new(null, [], [], []);

    public string? ToolName { get; } = toolName;

    public IReadOnlyList<OptionRecord> Options { get; } = options;

    public IReadOnlyList<string> Errors { get; } = errors;

    public IReadOnlyList<string> OriginalArguments { get; } = originalArguments;

    public bool HasError => Errors.Count > 0;

    public bool HasTool => ToolName is not null;

    public bool Equals(CommandLineParseResult? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other.ToolName != ToolName)
        {
            return false;
        }

        if (Errors.Count != other.Errors.Count)
        {
            return false;
        }

        for (int i = 0; i < Errors.Count; i++)
        {
            if (Errors[i] != other.Errors[i])
            {
                return false;
            }
        }

        if (Options.Count != other.Options.Count)
        {
            return false;
        }

        IReadOnlyList<OptionRecord> thisOptions = Options;
        IReadOnlyList<OptionRecord> otherOptions = other.Options;
        for (int i = 0; i < thisOptions.Count; i++)
        {
            if (thisOptions[i].Option != otherOptions[i].Option)
            {
                return false;
            }

            if (thisOptions[i].Arguments.Length != otherOptions[i].Arguments.Length)
            {
                return false;
            }

            for (int j = 0; j < thisOptions[i].Arguments.Length; j++)
            {
                if (thisOptions[i].Arguments[j] != otherOptions[i].Arguments[j])
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool IsOptionSet(string optionName)
        => Options.Any(o => o.Option.Equals(optionName.Trim(OptionPrefix), StringComparison.OrdinalIgnoreCase));

    public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments)
    {
        optionName = optionName.Trim(OptionPrefix);
        var result = Options.Where(x => x.Option == optionName);
        if (!result.Any())
        {
            arguments = result.SelectMany(x => x.Arguments).ToArray();
            return true;
        }

        arguments = null;
        return false;
    }

    public override bool Equals(object? obj) => Equals(obj as CommandLineParseResult);

    public override int GetHashCode()
    {
        HashCode hashCode = default;
        foreach (OptionRecord option in Options)
        {
            hashCode.Add(option.Option);
            foreach (string value in option.Arguments)
            {
                hashCode.Add(value);
            }
        }

        foreach (string error in Errors)
        {
            hashCode.Add(error);
        }

        return hashCode.ToHashCode();
    }
}
