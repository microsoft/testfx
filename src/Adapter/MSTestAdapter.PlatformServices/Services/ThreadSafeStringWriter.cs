// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// AsyncContext aware, thread safe string writer that allows output writes from different threads to end up in the same async local context.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class ThreadSafeStringWriter : StringWriter
{
#if DEBUG
    private static readonly ThreadSafeStringBuilder AllOutput = new();
#endif
    private static readonly AsyncLocal<Dictionary<string, ThreadSafeStringBuilder>> State = new();

    // This static lock guards access to the state and getting values from dictionary. There can be multiple different instances of ThreadSafeStringWriter
    // accessing the state at the same time, and we need to give them the correct state for their async context. Non-concurrent dictionary is used to store the
    // state because we need to lock around it anyway, to ensure that the State is populated, but not overwritten by every new instance of ThreadSafeStringWriter.
    private static readonly Lock StaticLockObject = new();
    private readonly string _outputType;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThreadSafeStringWriter"/> class.
    /// /!\/!\/!\ Be careful about where you use this for the first time. Because from that Task scope down we will start inheriting the
    /// static AsyncLocal State above. This state is used to differentiate output from tests even when they run in parallel. This works because
    /// we initiate this AsyncLocal in a place that is a separate Task for each test, and so any output that is written into a common stream
    /// (e.g. via Console.WriteLine - which is static, and hence common), will them be multiplexed by the appropriate AsyncLocal, and in effect
    /// we will get outputs splits for each test even if two tests run at the same time and write into console.
    /// See https://github.com/microsoft/testfx/pull/1705 for a fix of a related bug, in that bug we initialized the state too early, and it then inherited
    /// the same state to every Task (for every test) that we were running, and it all broke.
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

    public override StringBuilder GetStringBuilder() => throw new NotSupportedException("GetStringBuilder is not supported, because it does not allow us to clean the string builder in thread safe way.");

    /// <inheritdoc/>
    public override string ToString()
    {
        try
        {
            return GetStringBuilderOrNull()?.ToString()!;
        }
        catch (ObjectDisposedException)
        {
            return default!;
        }
    }

    public string? ToStringAndClear()
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
    public override void Write(string? value)
    {
#if DEBUG
        AllOutput.Append(value);
#endif
        GetOrAddStringBuilder().Append(value);
    }

    public override void WriteLine(string? value)
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
            State.Value?.Remove(_outputType);
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
    private ThreadSafeStringBuilder? GetStringBuilderOrNull()
    {
        lock (StaticLockObject)
        {
            return State.Value == null
                ? null
                : !State.Value.TryGetValue(_outputType, out ThreadSafeStringBuilder? stringBuilder) ? null : stringBuilder;
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
            else if (!State.Value.TryGetValue(_outputType, out ThreadSafeStringBuilder? stringBuilder))
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
    private sealed class ThreadSafeStringBuilder
    {
        private readonly StringBuilder _stringBuilder = new();
        private readonly Lock _instanceLockObject = new();

        public void Append(string? value)
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

        public void AppendLine(string? value)
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
                string output = _stringBuilder.ToString();
                _stringBuilder.Clear();
                return output;
            }
        }
    }
}
