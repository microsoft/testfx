// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

public sealed record class TrxExceptionProperty(string? Message, string? StackTrace) : IProperty;

public sealed record class TrxFullyQualifiedTypeNameProperty(string FullyQualifiedTypeName) : IProperty;

public record class TrxMessage(string? Message);

public sealed record class StandardErrorTrxMessage(string? Message) : TrxMessage(Message);

public sealed record class StandardOutputTrxMessage(string? Message) : TrxMessage(Message);

public sealed record class DebugOrTraceTrxMessage(string? Message) : TrxMessage(Message);

public sealed record class TrxMessagesProperty(TrxMessage[] Messages) : IProperty;

public sealed record class TrxCategoriesProperty(string[] Categories) : IProperty;
