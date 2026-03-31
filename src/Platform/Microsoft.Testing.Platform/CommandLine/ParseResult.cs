// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

/// <summary>
/// Represents the result of parsing a command line.
/// </summary>
/// <param name="toolName">The name of the tool.</param>
/// <param name="options">The collection of parsed options.</param>
/// <param name="errors">The collection of errors associated to the parsing.</param>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class CommandLineParseResult(string? toolName, IReadOnlyList<CommandLineParseOption> options, IReadOnlyList<string> errors) : IEquatable<CommandLineParseResult>
{
    /// <summary>
    /// The prefix for options.
    /// </summary>
    public const char OptionPrefix = '-';

    /// <summary>
    /// Gets an empty <see cref="CommandLineParseResult"/>.
    /// </summary>
    public static CommandLineParseResult Empty => new(null, [], []);

    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    public string? ToolName { get; } = toolName;

    /// <summary>
    /// Gets the collection of parsed options.
    /// </summary>
    public IReadOnlyList<CommandLineParseOption> Options { get; } = options;

    /// <summary>
    /// Gets the collection of errors associated to the parsing.
    /// </summary>
    public IReadOnlyList<string> Errors { get; } = errors;

    /// <summary>
    /// Gets a value indicating whether the parsing has errors.
    /// </summary>
    public bool HasError => Errors.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the parsing has a tool.
    /// </summary>
    public bool HasTool => ToolName is not null;

    /// <inheritdoc />
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

        IReadOnlyList<CommandLineParseOption> thisOptions = Options;
        IReadOnlyList<CommandLineParseOption> otherOptions = other.Options;
        for (int i = 0; i < thisOptions.Count; i++)
        {
            if (thisOptions[i].Name != otherOptions[i].Name)
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

    /// <summary>
    /// Determines if the specified option is set.
    /// </summary>
    /// <param name="optionName">The name of the option.</param>
    /// <returns>Returns <c>true</c> if the option is set; <c>false</c> otherwise.</returns>
    public bool IsOptionSet(string optionName)
        => Options.Any(o => o.Name.Equals(optionName.Trim(OptionPrefix), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the argument list for the specified option.
    /// </summary>
    /// <param name="optionName">The name of the option.</param>
    /// <param name="arguments">The arguments associated with the option.</param>
    /// <returns>Returns <c>true</c> if there are some arguments; <c>false</c> otherwise.</returns>
    public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments)
    {
        optionName = optionName.Trim(OptionPrefix);
        IEnumerable<CommandLineParseOption> result = Options.Where(x => x.Name == optionName);
        if (result.Any())
        {
            arguments = [.. result.SelectMany(x => x.Arguments)];
            return true;
        }

        arguments = null;
        return false;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as CommandLineParseResult);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hashCode = default;
        foreach (CommandLineParseOption option in Options)
        {
            hashCode.Add(option.Name);
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

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"ToolName: {ToolName}");

        builder.AppendLine("Errors:");
        if (Errors.Count == 0)
        {
            builder.AppendLine("    None");
        }
        else
        {
            foreach (string error in Errors)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"    {error}");
            }
        }

        builder.AppendLine("Options:");
        if (Options.Count == 0)
        {
            builder.AppendLine("    None");
        }
        else
        {
            foreach (CommandLineParseOption option in Options)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"   {option.Name}");
                foreach (string arg in option.Arguments)
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"        {arg}");
                }
            }
        }

        return builder.ToString();
    }
}
