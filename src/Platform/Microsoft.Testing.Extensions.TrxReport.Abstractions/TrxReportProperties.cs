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

public sealed record TrxMessagesProperty(TrxMessage[] Messages) : IProperty;

public sealed record TrxCategoriesProperty(string[] Categories) : IProperty;
