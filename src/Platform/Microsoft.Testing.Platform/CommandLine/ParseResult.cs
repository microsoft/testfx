// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class CommandLineParseResult(string? toolName, OptionRecord[] options, string[] errors, string[] originalArguments) : IEquatable<CommandLineParseResult>
{
    public const char OptionPrefix = '-';

    public static CommandLineParseResult Empty => new(null, [], [], []);

    public string? ToolName { get; set; } = toolName;

    public OptionRecord[] Options { get; } = options;

    public string[] Errors { get; } = errors;

    public string[] OriginalArguments { get; } = originalArguments;

    public bool HasError => Errors.Length > 0;

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

        if (Errors.Length != other.Errors.Length)
        {
            return false;
        }

        for (int i = 0; i < Errors.Length; i++)
        {
            if (Errors[i] != other.Errors[i])
            {
                return false;
            }
        }

        if (Options.Length != other.Options.Length)
        {
            return false;
        }

        OptionRecord[] thisOptions = Options;
        OptionRecord[] otherOptions = other.Options;
        for (int i = 0; i < thisOptions.Length; i++)
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
        var result = Options.Where(x => x.Option == optionName).ToList();
        if (result.Count != 0)
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
