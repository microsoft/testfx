// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Property that represents standard output to associate with a test node.
/// </summary>
public sealed class StandardOutputProperty : IProperty, IEquatable<StandardOutputProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StandardOutputProperty"/> class.
    /// </summary>
    /// <param name="standardOutput">The standard output.</param>
    public StandardOutputProperty(string standardOutput)
        => StandardOutput = standardOutput;

    /// <summary>
    /// Gets the standard output.
    /// </summary>
    public string StandardOutput { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(StandardOutputProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(StandardOutput)} = ");
        builder.Append(StandardOutput);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as StandardOutputProperty);

    /// <inheritdoc />
    public bool Equals(StandardOutputProperty? other)
        => other is not null && StandardOutput == other.StandardOutput;

    /// <inheritdoc />
    public override int GetHashCode()
        => StandardOutput?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents standard error to associate with a test node.
/// </summary>
public sealed class StandardErrorProperty : IProperty, IEquatable<StandardErrorProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StandardErrorProperty"/> class.
    /// </summary>
    /// <param name="standardError">The standard error.</param>
    public StandardErrorProperty(string standardError)
        => StandardError = standardError;

    /// <summary>
    /// Gets the standard error.
    /// </summary>
    public string StandardError { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(StandardErrorProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(StandardError)} = ");
        builder.Append(StandardError);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as StandardErrorProperty);

    /// <inheritdoc />
    public bool Equals(StandardErrorProperty? other)
        => other is not null && StandardError == other.StandardError;

    /// <inheritdoc />
    public override int GetHashCode()
        => StandardError?.GetHashCode() ?? 0;
}
