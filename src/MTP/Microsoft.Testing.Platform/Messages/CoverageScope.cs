// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Identifies the entity a coverage measurement or threshold describes. Scope levels form
/// overlapping dimensions rather than a single parent hierarchy (a file can contain many types and a
/// partial type can span many files), so this models scope identity, not a forced tree.
/// </summary>
public readonly struct CoverageScope : IEquatable<CoverageScope>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageScope"/> struct.
    /// </summary>
    /// <param name="level">The granularity of the scope.</param>
    /// <param name="name">The scope identifier (module path, type name, file path…); must be null for <see cref="CoverageScopeLevel.Overall"/> and non-empty otherwise.</param>
    /// <param name="containerHint">Optional, non-authoritative grouping hint for UIs.</param>
    public CoverageScope(CoverageScopeLevel level, string? name = null, string? containerHint = null)
    {
        // Invariant: only Overall is unnamed; every other level requires a stable identifier.
        if (level == CoverageScopeLevel.Overall)
        {
            if (name is not null)
            {
                throw new ArgumentException("The Overall scope must not have a name.", nameof(name));
            }
        }
        else if (RoslynString.IsNullOrEmpty(name))
        {
            throw new ArgumentException($"A name is required for scope level '{level}'.", nameof(name));
        }

        Level = level;
        Name = name;
        ContainerHint = containerHint;
    }

    /// <summary>Gets the granularity of this scope.</summary>
    public CoverageScopeLevel Level { get; }

    /// <summary>Gets the scope identifier (module path, type name, file path…); null only for <see cref="CoverageScopeLevel.Overall"/>.</summary>
    public string? Name { get; }

    /// <summary>
    /// Gets an optional, non-authoritative grouping hint for UIs (e.g. the assembly of a type, or a
    /// representative file of a type). It is deliberately <b>not</b> a tree edge: a file can contain
    /// many types and a partial type can span many files, so scope levels form overlapping dimensions
    /// rather than a single parent hierarchy. Consumers that need exact containment should read the
    /// referenced report artifact (see <see cref="TestCoverageReportMessage"/>), which carries the
    /// authoritative structure.
    /// </summary>
    public string? ContainerHint { get; }

    /// <summary>Gets the whole-run scope.</summary>
    public static CoverageScope Overall => new(CoverageScopeLevel.Overall);

    /// <summary>Compares two <see cref="CoverageScope"/> values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the two values are equal.</returns>
    public static bool operator ==(CoverageScope left, CoverageScope right) => left.Equals(right);

    /// <summary>Compares two <see cref="CoverageScope"/> values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the two values are not equal.</returns>
    public static bool operator !=(CoverageScope left, CoverageScope right) => !left.Equals(right);

    /// <inheritdoc/>
    // Identity is (Level, Name) only. ContainerHint is a non-authoritative display hint and is
    // intentionally excluded so two collectors reporting the same scope with different hints are not
    // split into separate entries by the correlator.
    public bool Equals(CoverageScope other)
        => Level == other.Level
        && string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CoverageScope scope && Equals(scope);

    /// <inheritdoc/>
    public override int GetHashCode() => RoslynHashCode.Combine(Level, Name);
}
