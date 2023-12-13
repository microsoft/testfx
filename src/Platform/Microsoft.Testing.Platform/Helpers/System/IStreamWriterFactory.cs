// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Testing.Platform.Helpers;

internal interface IStreamWriterFactory
{
    IStreamWriter CreateStreamWriter(IFileStream stream, Encoding encoding, bool autoFlush);
}
