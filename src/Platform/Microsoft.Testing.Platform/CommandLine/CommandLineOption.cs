// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Extensions.CommandLine;

[DebuggerDisplay("Name = {Name} Arity = {Arity} IsHidden = {IsHidden} IsBuiltIn = {IsBuiltIn}")]
public sealed class CommandLineOption : IEquatable<CommandLineOption>
{
    internal CommandLineOption(string name, string description, ArgumentArity arity, bool isHidden, bool isBuiltIn)
    {
        ArgumentGuard.IsNotNullOrWhiteSpace(name);
        ArgumentGuard.IsNotNullOrWhiteSpace(description);
        ArgumentGuard.Ensure(arity.Max >= arity.Min, nameof(arity), "Invalid arity, max must be greater than min");

        for (int i = 0; i < name.Length; i++)
        {
            ArgumentGuard.Ensure(char.IsLetter(name[i]) || name[i] == '-', nameof(name),
                $"Invalid option definition name '{name}', only letters and '-' are allowed, e.g. --my-option");
        }

        Name = name;
        Description = description;
        Arity = arity;
        IsHidden = isHidden;
        IsBuiltIn = isBuiltIn;
    }

    // This ctor is public and used by non built-in extension, we need to know if the extension is built-in or not
    // to correctly handle the --internal- prefix
    public CommandLineOption(string name, string description, ArgumentArity arity, bool isHidden)
        : this(name, description, arity, isHidden, isBuiltIn: false)
    {
    }

    public string Name { get; }

    public string Description { get; }

    public ArgumentArity Arity { get; }

    public bool IsHidden { get; }

    internal bool IsBuiltIn { get; }

    public override bool Equals(object? obj) => Equals(obj as CommandLineOption);

    public bool Equals(CommandLineOption? other)
        => other != null &&
            Name == other.Name &&
            Description == other.Description &&
            Arity == other.Arity &&
            IsHidden == other.IsHidden;

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
