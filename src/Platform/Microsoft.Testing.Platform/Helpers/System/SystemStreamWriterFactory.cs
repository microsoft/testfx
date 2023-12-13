// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemStreamWriterFactory : IStreamWriterFactory
{
    public IStreamWriter CreateStreamWriter(IFileStream stream, Encoding encoding, bool autoFlush)
        => new SystemStreamWriter(stream, encoding)
        {
            AutoFlush = autoFlush,
        };
}
