// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

using Polyfills;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

/// <summary>
/// Represents an exception to report in the TRX file.
/// </summary>
/// <param name="Message">The exception message.</param>
/// <param name="StackTrace">The exception stack trace.</param>
public sealed record TrxExceptionProperty(string? Message, string? StackTrace) : IProperty;

/// <summary>
/// A property that represents the fully qualified type name to be reported in the TRX file.
/// </summary>
/// <param name="FullyQualifiedTypeName">The fully qualified type name.</param>
public sealed record TrxFullyQualifiedTypeNameProperty(string FullyQualifiedTypeName) : IProperty;

/// <summary>
/// A property that represents a message to be reported in the TRX file.
/// </summary>
/// <param name="Message">The message.</param>
public record TrxMessage(string? Message);

/// <summary>
/// A property that represents the standard error message to be reported in the TRX file.
/// </summary>
/// <param name="Message">The standard error message.</param>
public sealed record StandardErrorTrxMessage(string? Message) : TrxMessage(Message);

/// <summary>
/// A property that represents the standard output message to be reported in the TRX file.
/// </summary>
/// <param name="Message">The standard output message.</param>
public sealed record StandardOutputTrxMessage(string? Message) : TrxMessage(Message);

/// <summary>
/// A property that represents a debug or trace message to be reported in the TRX file.
/// </summary>
/// <param name="Message">The debug or trace message.</param>
public sealed record DebugOrTraceTrxMessage(string? Message) : TrxMessage(Message);

/// <summary>
/// A property that represents the messages to be reported in the TRX file.
/// </summary>
/// <param name="Messages">The TRX message properties.</param>
public sealed record TrxMessagesProperty(TrxMessage[] Messages) : IProperty
{
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "https://github.com/dotnet/roslyn/issues/52421")]
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Messages = [");
        builder.AppendJoin(", ", Messages.Select(x => x.ToString()));
        builder.Append(']');
        return true;
    }
}

/// <summary>
/// A property that represents the categories to be reported in the TRX file.
/// </summary>
/// <param name="Categories">The categories.</param>
public sealed record TrxCategoriesProperty(string[] Categories) : IProperty
{
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "https://github.com/dotnet/roslyn/issues/52421")]
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Categories = [");
        builder.AppendJoin(", ", Categories);
        builder.Append(']');
        return true;
    }
}
