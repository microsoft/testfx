// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// A single declared dependency edge (from a <c>[DependsOn]</c> attribute or from <c>testconfig.json</c>)
/// carried on a <see cref="UnitTestElement"/>: which test must run first, and whether the
/// dependent still runs when that test does not pass.
/// </summary>
#if NETFRAMEWORK
[Serializable]
#endif
internal sealed class TestDependencyInfo
{
    /// <summary>
    /// The separator between the class and method parts of the encoded form. A CLR type full name and a
    /// method name can never contain a line feed, so it can never occur inside either field.
    /// </summary>
    private const char FieldSeparator = '\n';

    public TestDependencyInfo(string? targetClassFullName, string? targetMethodName, bool proceedOnFailure)
    {
        // Normalize empty to null so that the encoder's "empty means absent" convention and the
        // resolver's null checks agree, whatever produced the value (attribute, configuration, decoder).
        TargetClassFullName = string.IsNullOrEmpty(targetClassFullName) ? null : targetClassFullName;
        TargetMethodName = string.IsNullOrEmpty(targetMethodName) ? null : targetMethodName;
        ProceedOnFailure = proceedOnFailure;
    }

    /// <summary>
    /// Gets the full name of the class declaring the prerequisite, or <see langword="null"/> when the
    /// prerequisite is a method of the dependent's own class.
    /// </summary>
    public string? TargetClassFullName { get; }

    /// <summary>
    /// Gets the name of the prerequisite test method, or <see langword="null"/> when the dependency
    /// targets every test of <see cref="TargetClassFullName"/>.
    /// </summary>
    public string? TargetMethodName { get; }

    /// <summary>
    /// Gets a value indicating whether the dependent runs even when the prerequisite does not pass.
    /// Ordering is enforced either way.
    /// </summary>
    public bool ProceedOnFailure { get; }

    /// <summary>
    /// Renders the target as a human-readable reference for diagnostics, for example
    /// <c>MyNamespace.MyClass.MyMethod</c>, <c>MyNamespace.MyClass.*</c> or <c>MyMethod</c>.
    /// </summary>
    public string DescribeTarget()
        => TargetClassFullName is null
            ? TargetMethodName ?? "*"
            : $"{TargetClassFullName}.{TargetMethodName ?? "*"}";

    /// <summary>
    /// Encodes a dependency as a single string for transport across the VSTest <c>TestProperty</c>
    /// boundary: a one-character flag for <see cref="ProceedOnFailure"/> (<c>P</c> when it is set,
    /// <c>S</c> - skip - otherwise), then the class name, a line feed, and the method name. An absent
    /// class or method is written as the empty string.
    /// </summary>
    /// <remarks>
    /// The flag is a fixed-width prefix, as for <see cref="ResourceLockInfo.Encode"/>, so that a
    /// well-formed payload always splits at exactly one position whatever the names contain. The
    /// encoding is not self-describing; <see cref="Encode"/> and <see cref="Decode"/> are a matched
    /// pair over a private adapter property, so <see cref="Decode"/> only ever sees this method's
    /// output.
    /// </remarks>
    public static string Encode(TestDependencyInfo info)
        => (info.ProceedOnFailure ? "P" : "S") + info.TargetClassFullName + FieldSeparator + info.TargetMethodName;

    /// <summary>
    /// Decodes a dependency previously produced by <see cref="Encode"/>. Malformed data fails closed:
    /// an unrecognized flag decodes as "skip on failure", which is the conservative behavior, and a
    /// payload without a separator is read as a bare method name in the dependent's own class.
    /// </summary>
    public static TestDependencyInfo Decode(string encoded)
    {
        if (encoded.Length == 0)
        {
            return new TestDependencyInfo(null, null, false);
        }

        // Only consume the first character when it is a flag we actually wrote; otherwise treat the
        // whole payload as data so that an unrecognized form still names the same test rather than a
        // truncated - and therefore different - one.
        bool hasFlag = encoded[0] is 'P' or 'S';
        bool proceedOnFailure = encoded[0] == 'P';
        string payload = hasFlag ? encoded.Substring(1) : encoded;

        int separator = payload.IndexOf(FieldSeparator);
        return separator < 0
            ? new TestDependencyInfo(null, payload, proceedOnFailure)
            : new TestDependencyInfo(payload.Substring(0, separator), payload.Substring(separator + 1), proceedOnFailure);
    }
}
