// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

/// <summary>
/// Represents an exception to report in the TRX file.
/// </summary>
public sealed class TrxExceptionProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TrxExceptionProperty"/> class with the specified message and stack trace.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="stackTrace">The exception stack trace.</param>
    public TrxExceptionProperty(string message, string stackTrace)
        => (Message, StackTrace) = (message, stackTrace);

    /// <summary>
    /// Gets the exception message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the exception stack trace.
    /// </summary>
    public string StackTrace { get; }
}

/// <summary>
/// A property that represents the fully qualified type name to be reported in the TRX file.
/// </summary>
public sealed class TrxFullyQualifiedTypeNameProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TrxFullyQualifiedTypeNameProperty"/> class.
    /// </summary>
    /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
    public TrxFullyQualifiedTypeNameProperty(string fullyQualifiedTypeName)
        => FullyQualifiedTypeName = fullyQualifiedTypeName;

    /// <summary>
    /// Gets the fully qualified type name.
    /// </summary>
    public string FullyQualifiedTypeName { get; }
}

/// <summary>
/// A property that represents a message to be reported in the TRX file.
/// </summary>
public abstract class TrxMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TrxMessage"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    protected TrxMessage(string? message)
        => Message = message;

    /// <summary>
    /// Gets the message.
    /// </summary>
    public string? Message { get; }
}

/// <summary>
/// A property that represents the standard error message to be reported in the TRX file.
/// </summary>
public sealed class StandardErrorTrxMessage : TrxMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StandardErrorTrxMessage"/> class.
    /// </summary>
    /// <param name="message">The standard error message.</param>
    public StandardErrorTrxMessage(string? message)
        : base(message)
    {
    }
}

/// <summary>
/// A property that represents the standard output message to be reported in the TRX file.
/// </summary>
public sealed class StandardOutputTrxMessage : TrxMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StandardOutputTrxMessage"/> class.
    /// </summary>
    /// <param name="message">The standard output message.</param>
    public StandardOutputTrxMessage(string? message)
        : base(message)
    {
    }
}

/// <summary>
/// A property that represents a debug or trace message to be reported in the TRX file.
/// </summary>
public sealed class DebugOrTraceTrxMessage : TrxMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DebugOrTraceTrxMessage"/> class.
    /// </summary>
    /// <param name="message">The debug or trace message.</param>
    public DebugOrTraceTrxMessage(string? message)
        : base(message)
    {
    }
}

/// <summary>
/// A property that represents the messages to be reported in the TRX file.
/// </summary>
public sealed class TrxMessagesProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TrxMessagesProperty"/> class.
    /// </summary>
    /// <param name="messages">The TRX message properties.</param>
    public TrxMessagesProperty(TrxMessage[] messages)
        => Messages = messages;

    /// <summary>
    /// Gets the TRX message properties.
    /// </summary>
    public TrxMessage[] Messages { get; }

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
public sealed class TrxCategoriesProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TrxCategoriesProperty"/> class.
    /// </summary>
    /// <param name="categories">The categories.</param>
    public TrxCategoriesProperty(string[] categories)
        => Categories = categories;

    /// <summary>
    /// Gets the categories.
    /// </summary>
    public string[] Categories { get; }
}
