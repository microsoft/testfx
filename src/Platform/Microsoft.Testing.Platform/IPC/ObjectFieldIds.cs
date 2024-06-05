// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

// WARNING: Please note this file needs to be kept aligned with the one in the dotnet sdk.
// The protocol follows the concept of optional properties.
// The id is used to identify the property in the stream and it will be skipped if it's not recognized.
// We can add new properties with new ids, but we CANNOT change the existing ids (to support backwards compatibility).
internal static class CommandLineOptionMessagesFieldsId
{
    internal const int ModuleName = 1;
    internal const int CommandLineOptionMessageList = 2;
}

internal static class CommandLineOptionMessageFieldsId
{
    internal const int Name = 1;
    internal const int Description = 2;
    internal const int IsHidden = 3;
    internal const int IsBuiltIn = 4;
}
