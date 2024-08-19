// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class MessageFormatException : Exception
{
    public MessageFormatException()
        : base()
    {
    }

    public MessageFormatException(string err)
        : base(err)
    {
    }
}
