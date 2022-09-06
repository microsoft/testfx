// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

/// <summary>
/// AsyncContext aware, thread safe string writer that allows output writes from different threads to end up in the same async local context.
/// </summary>
public class ThreadSafeStringWriter : StringWriter
{
#if DEBUG
    private static readonly ThreadSafeStringBuilder AllOutput = new();
#endif
    private static readonly AsyncLocal<Dictionary<string, ThreadSafeStringBuilder>> State = new();

    // This static lock guards access to the state and getting values from dictionary. There can be multiple different instances of ThreadSafeStringWriter
    // accessing the state at the same time, and we need to give them the correct state for their async context. Non-concurrent dictionary is used to store the
    // state because we need to lock around it anyway, to ensure that the State is populated, but not overwritten by every new instance of ThreadSafeStringWriter.
    private static readonly object StaticLockObject = new();
    private readonly string _outputType;

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
        _outputType = outputType;

        // Ensure that State.Value is populated, so we can inherit it to the child
        // async flow, and also keep reference to it here in the parent flow.
        // otherwise if there is `async Task` test method, the method will run as child async flow
        // populate it but the parent will remain null, because the changes to context only flow downwards
        // and not upwards.
        GetOrAddStringBuilder();
    }

    public override StringBuilder GetStringBuilder()
    {
        throw new NotSupportedException("GetStringBuilder is not supported, because it does not allow us to clean the string builder in thread safe way.");
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        try
        {
            return GetStringBuilderOrNull()?.ToString();
        }
        catch (ObjectDisposedException)
        {
            return default;
        }
    }

    public string ToStringAndClear()
    {
        try
        {
            return GetStringBuilderOrNull()?.ToStringAndClear();
        }
        catch (ObjectDisposedException)
        {
            return default;
        }
    }

    /// <inheritdoc/>
    public override void Write(char value)
    {
#if DEBUG
        AllOutput.Append(value);
#endif
        GetOrAddStringBuilder().Append(value);
    }

    /// <inheritdoc/>
    public override void Write(string value)
    {
#if DEBUG
        AllOutput.Append(value);
#endif
        GetOrAddStringBuilder().Append(value);
    }

    public override void WriteLine(string value)
    {
#if DEBUG
        AllOutput.AppendLine(value);
#endif
        GetOrAddStringBuilder().AppendLine(value);
    }

    /// <inheritdoc/>
    public override void Write(char[] buffer, int index, int count)
    {
#if DEBUG
        AllOutput.Append(buffer, index, count);
#endif
        GetOrAddStringBuilder().Append(buffer, index, count);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        lock (StaticLockObject)
        {
            ThreadSafeStringWriter.State?.Value?.Remove(_outputType);
            try
            {
                base.Dispose(disposing);
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    // Avoiding name GetStringBuilder because it is already present on the base class.
    private ThreadSafeStringBuilder GetStringBuilderOrNull()
    {
        lock (StaticLockObject)
        {
            if (State.Value == null)
            {
                return null;
            }
            else if (!State.Value.TryGetValue(_outputType, out var stringBuilder))
            {
                return null;
            }
            else
            {
                return stringBuilder;
            }
        }
    }

    private ThreadSafeStringBuilder GetOrAddStringBuilder()
    {
        lock (StaticLockObject)
        {
            if (State.Value == null)
            {
                // The storage for the current async operation is empty
                // create the dictionary and appropriate stringbuilder.
                // Avoid looking up the value after we add it to the dictionary.
                var sb = new ThreadSafeStringBuilder();
                State.Value = new Dictionary<string, ThreadSafeStringBuilder> { [_outputType] = sb };
                return sb;
            }
            else if (!State.Value.TryGetValue(_outputType, out var stringBuilder))
            {
                // The storage for the current async operation has the dictionary, but not the key
                // for the output type, add it, and avoid looking up the value again.
                var sb = new ThreadSafeStringBuilder();
                State.Value.Add(_outputType, sb);
                return sb;
            }
            else
            {
                // The storage for the current async operation has the dictionary, and the key
                // for the output type, just return it.
                return stringBuilder;
            }
        }
    }

    /// <summary>
    /// This StringBuilder puts locks around all the methods to avoid conflicts when writing or reading from multiple threads.
    /// </summary>
    private class ThreadSafeStringBuilder
    {
        private readonly StringBuilder _stringBuilder = new();
        private readonly object _instanceLockObject = new();

        public void Append(string value)
        {
            lock (_instanceLockObject)
            {
                _stringBuilder.Append(value);
            }
        }

        public void Append(char[] buffer, int index, int count)
        {
            lock (_instanceLockObject)
            {
                _stringBuilder.Append(buffer, index, count);
            }
        }

        public void Append(char value)
        {
            lock (_instanceLockObject)
            {
                _stringBuilder.Append(value);
            }
        }

        public void AppendLine(string value)
        {
            lock (_instanceLockObject)
            {
                _stringBuilder.AppendLine(value);
            }
        }

        public void Clear()
        {
            lock (_instanceLockObject)
            {
                _stringBuilder.Clear();
            }
        }

        public override string ToString()
        {
            lock (_instanceLockObject)
            {
                return _stringBuilder.ToString();
            }
        }

        internal string ToStringAndClear()
        {
            lock (_instanceLockObject)
            {
                var output = _stringBuilder.ToString();
                _stringBuilder.Clear();
                return output;
            }
        }
    }
}
