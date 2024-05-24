// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

internal class CommandLineOptionMessagesFields
{
    internal const int ModuleName = 1;
    internal const int CommandLineOptionMessageList = 2;
}

internal class CommandLineOptionMessageFields
{
    internal const int Name = 1;
    internal const int Description = 2;
    internal const int Arity = 3;
    internal const int IsHidden = 4;
    internal const int IsBuiltIn = 5;
}
