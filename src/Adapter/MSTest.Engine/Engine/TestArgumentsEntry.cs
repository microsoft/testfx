// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

/// <summary>
/// WARNING: This type is public, but is meant for use only by MSTest source generator. Unannounced breaking changes to this API may happen.
/// </summary>
/// <typeparam name="TArguments">Type of the input data.</typeparam>
public sealed class InternalUnsafeTestArgumentsEntry<TArguments>(TArguments arguments, string uidFragment, string? displayNameFragment = null) : ITestArgumentsEntry
{
    public TArguments Arguments { get; } = arguments;

    public string UidFragment { get; } = uidFragment;

    public string? DisplayNameFragment { get; } = displayNameFragment;

    object? ITestArgumentsEntry.Arguments => Arguments;
}
