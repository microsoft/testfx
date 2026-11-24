// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

internal sealed record CommandLineOptionMessage(
    string? Name,
    string? Description,
    bool? IsHidden,
    bool? IsBuiltIn)
{
    public CommandLineOptionMessage(
        string? name,
        string? description,
        bool? isHidden,
        bool? isBuiltIn,
        string? providerUid,
        int? minimumArity,
        int? maximumArity)
        : this(name, description, isHidden, isBuiltIn)
    {
        ProviderUid = providerUid;
        MinimumArity = minimumArity;
        MaximumArity = maximumArity;
    }

    public string? ProviderUid { get; init; }

    public int? MinimumArity { get; init; }

    public int? MaximumArity { get; init; }
}

internal sealed record CommandLineOptionMessages(string? ModulePath, CommandLineOptionMessage[]? CommandLineOptionMessageList) : IRequest;
