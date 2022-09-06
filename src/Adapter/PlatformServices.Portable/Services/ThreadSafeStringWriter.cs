// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using System;
using System.IO;

/// <summary>
/// StringWriter which has thread safe ToString().
/// </summary>
public class ThreadSafeStringWriter : StringWriter
{
    private readonly object _lockObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ThreadSafeStringWriter"/> class.
    /// </summary>
    /// <param name="formatProvider">
    /// The format provider.
    /// </param>
    /// <param name="outputType">
    /// Id of the session.
    /// </param>
    public ThreadSafeStringWriter(IFormatProvider formatProvider, string outputType)
        : base(formatProvider)
    {
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        lock (_lockObject)
        {
            try
            {
                return base.ToString();
            }
            catch (ObjectDisposedException)
            {
                return default;
            }
        }
    }

    public string ToStringAndClear()
    {
        lock (_lockObject)
        {
            try
            {
                var sb = GetStringBuilder();
                var output = sb.ToString();
                sb.Clear();
                return output;
            }
            catch (ObjectDisposedException)
            {
                return default;
            }
        }
    }

    /// <inheritdoc/>
    public override void Write(char value)
    {
        lock (_lockObject)
        {
            InvokeBaseClass(() => base.Write(value));
        }
    }

    /// <inheritdoc/>
    public override void Write(string value)
    {
        lock (_lockObject)
        {
            InvokeBaseClass(() => base.Write(value));
        }
    }

    /// <inheritdoc/>
    public override void Write(char[] buffer, int index, int count)
    {
        lock (_lockObject)
        {
            InvokeBaseClass(() => base.Write(buffer, index, count));
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        lock (_lockObject)
        {
            InvokeBaseClass(() => base.Dispose(disposing));
        }
    }

    private static void InvokeBaseClass(Action action)
    {
        try
        {
            action();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
