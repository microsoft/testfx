// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Extensions.CommandLine;

/// <summary>
/// Represents a command line option.
/// </summary>
[DebuggerDisplay("Name = {Name} Arity = {Arity} IsHidden = {IsHidden} IsBuiltIn = {IsBuiltIn}")]
public sealed class CommandLineOption : IEquatable<CommandLineOption>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLineOption"/> class.
    /// </summary>
    /// <param name="name">The name of the command line option.</param>
    /// <param name="description">The description of the command line option.</param>
    /// <param name="arity">The arity of the command line option.</param>
    /// <param name="isHidden">Indicates whether the command line option is hidden.</param>
    /// <param name="isBuiltIn">Indicates whether the command line option is built-in.</param>
    internal CommandLineOption(string name, string description, ArgumentArity arity, bool isHidden, bool isBuiltIn)
    {
        Guard.NotNullOrWhiteSpace(name);
        Guard.NotNullOrWhiteSpace(description);
        ArgumentGuard.Ensure(arity.Max >= arity.Min, nameof(arity), PlatformResources.CommandLineInvalidArityErrorMessage);

        string errorMessage = string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineInvalidOptionName, name);
        for (int i = 0; i < name.Length; i++)
        {
            ArgumentGuard.Ensure(char.IsLetterOrDigit(name[i]) || name[i] == '-' || name[i] == '?', nameof(name), errorMessage);
        }

        Name = name;
        Description = description;
        Arity = arity;
        IsHidden = isHidden;
        IsBuiltIn = isBuiltIn;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLineOption"/> class.
    /// </summary>
    /// <param name="name">The name of the command line option.</param>
    /// <param name="description">The description of the command line option.</param>
    /// <param name="arity">The arity of the command line option.</param>
    /// <param name="isHidden">Indicates whether the command line option is hidden.</param>
    /// <remarks>
    /// This ctor is public and used by non built-in extension, we need to know if the extension is built-in or not
    /// to correctly handle the --internal- prefix.
    /// </remarks>
    public CommandLineOption(string name, string description, ArgumentArity arity, bool isHidden)
        : this(name, description, arity, isHidden, isBuiltIn: false)
    {
    }

    /// <summary>
    /// Gets the name of the command line option.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the description of the command line option.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the arity of the command line option.
    /// </summary>
    public ArgumentArity Arity { get; }

    /// <summary>
    /// Gets a value indicating whether the command line option is hidden.
    /// </summary>
    public bool IsHidden { get; }

    internal bool IsBuiltIn { get; }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CommandLineOption);

    /// <inheritdoc/>
    public bool Equals(CommandLineOption? other)
        => other != null &&
            Name == other.Name &&
            Description == other.Description &&
            Arity == other.Arity &&
            IsHidden == other.IsHidden;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hc = default;
        hc.Add(Name);
        hc.Add(Description);
        hc.Add(Arity.GetHashCode());
        hc.Add(IsHidden);
        return hc.ToHashCode();
    }
}
