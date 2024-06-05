﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

internal sealed record CommandLineOptionMessage(string Name, string Description, bool IsHidden, bool IsBuiltIn) : IRequest;

internal sealed record CommandLineOptionMessages(string ModuleName, CommandLineOptionMessage[] CommandLineOptionMessageList) : IRequest;
