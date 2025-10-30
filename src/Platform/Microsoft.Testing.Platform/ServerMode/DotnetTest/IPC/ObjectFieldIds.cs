// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

// WARNING: Please note this file needs to be kept aligned with the one in the dotnet sdk.
// The protocol follows the concept of optional properties.
// The id is used to identify the property in the stream and it will be skipped if it's not recognized.
// We can add new properties with new ids, but we CANNOT change the existing ids (to support backwards compatibility).
internal static class VoidResponseFieldsId
{
    public const int MessagesSerializerId = 0;
}

internal static class TestHostProcessExitRequestFieldsId
{
    public const int MessagesSerializerId = 1;
}

internal static class TestHostProcessPIDRequestFieldsId
{
    public const int MessagesSerializerId = 2;
}

internal static class CommandLineOptionMessageFieldsId
{
    public const ushort Name = 1;
    public const ushort Description = 2;
    public const ushort IsHidden = 3;
    public const ushort IsBuiltIn = 4;
}

internal static class DiscoveredTestMessageFieldsId
{
    public const ushort Uid = 1;
    public const ushort DisplayName = 2;
    public const ushort FilePath = 3;
    public const ushort LineNumber = 4;
    public const ushort Namespace = 5;
    public const ushort TypeName = 6;
    public const ushort MethodName = 7;
    public const ushort Traits = 8;
    public const ushort ParameterTypeFullNames = 9;
}
