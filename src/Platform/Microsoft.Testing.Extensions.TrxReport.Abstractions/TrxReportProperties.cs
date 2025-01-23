// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

public sealed record TrxExceptionProperty(string? Message, string? StackTrace) : IProperty;

public sealed record TrxFullyQualifiedTypeNameProperty(string FullyQualifiedTypeName) : IProperty;

public record TrxMessage(string? Message);

public sealed record StandardErrorTrxMessage(string? Message) : TrxMessage(Message);

public sealed record StandardOutputTrxMessage(string? Message) : TrxMessage(Message);

public sealed record DebugOrTraceTrxMessage(string? Message) : TrxMessage(Message);

public sealed record TrxMessagesProperty(TrxMessage[] Messages) : IProperty
{
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "https://github.com/dotnet/roslyn/issues/52421")]
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Messages = [");
        builder.Append(string.Join(", ", Messages.Select(x => x.ToString())));
        builder.Append(']');
        return true;
    }
}

public sealed record TrxCategoriesProperty(string[] Categories) : IProperty
{
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "https://github.com/dotnet/roslyn/issues/52421")]
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Categories = [");
        builder.Append(string.Join(", ", Categories));
        builder.Append(']');
        return true;
    }
}
