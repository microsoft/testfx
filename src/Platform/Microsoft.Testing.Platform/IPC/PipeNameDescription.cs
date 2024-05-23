// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

internal sealed class PipeNameDescription(string name, bool isDirectory) : IDisposable
{
    private bool _disposed;

    public string Name { get; } = name;

    public void Dispose() => Dispose(true);

    public void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // TODO: dispose managed state (managed objects).
        }

        if (isDirectory)
        {
            try
            {
                Directory.Delete(Path.GetDirectoryName(Name)!, true);
            }
            catch (IOException)
            {
                // This folder is created inside the temp directory and will be cleaned up eventually by the OS
            }
        }

        _disposed = true;
    }
}
