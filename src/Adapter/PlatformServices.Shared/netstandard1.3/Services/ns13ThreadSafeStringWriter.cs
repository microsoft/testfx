// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// StringWriter which has thread safe ToString().
    /// </summary>
    public class ThreadSafeStringWriter : StringWriter
    {
#if DEBUG
        private static readonly StringBuilder AllOutput = new StringBuilder();
#endif
        private static readonly AsyncLocal<Dictionary<string, StringBuilder>> State = new AsyncLocal<Dictionary<string, StringBuilder>>();
        private readonly string outputType;
        private readonly object lockObject = new object();

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
            this.outputType = outputType;

            lock (this.lockObject)
            {
                    // Ensure that State.Value is populated, so we can inherit it to the child
                    // async flow, and also keep reference to it here in the parent flow.
                    // otherwise if there is `async Task` test method, the method will run as child async flow
                    // populate it but the parent will remain null, because the changes to context only flow downwards
                    // and not upwards.
                    this.GetOrAddStringBuilder();
            }
        }

        public override StringBuilder GetStringBuilder()
        {
            throw new NotSupportedException("GetStringBuilder is not supported, because it does not allow us to clean the string builder in thread safe way.");
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            lock (this.lockObject)
            {
                try
                {
                    return this.GetStringBuilderOrNull()?.ToString();
                }
                catch (ObjectDisposedException)
                {
                    return default(string);
                }
            }
        }

        public string ToStringAndClear()
        {
            lock (this.lockObject)
            {
                try
                {
                    var sb = this.GetStringBuilderOrNull();

                    if (sb == null)
                    {
                        return default(string);
                    }

                    var output = sb.ToString();
                    sb.Clear();
                    return output;
                }
                catch (ObjectDisposedException)
                {
                    return default(string);
                }
            }
        }

        /// <inheritdoc/>
        public override void Write(char value)
        {
            lock (this.lockObject)
            {
#if DEBUG
                AllOutput.Append(value);
#endif
                this.GetOrAddStringBuilder().Append(value);
            }
        }

        /// <inheritdoc/>
        public override void Write(string value)
        {
            lock (this.lockObject)
            {
#if DEBUG
                AllOutput.Append(value);
#endif
                this.GetOrAddStringBuilder().Append(value);
            }
        }

        public override void WriteLine(string value)
        {
            lock (this.lockObject)
            {
#if DEBUG
                AllOutput.AppendLine(value);
#endif
                this.GetOrAddStringBuilder().AppendLine(value);
            }
        }

        /// <inheritdoc/>
        public override void Write(char[] buffer, int index, int count)
        {
            lock (this.lockObject)
            {
#if DEBUG
                AllOutput.Append(buffer, index, count);
#endif
                this.GetOrAddStringBuilder().Append(buffer, index, count);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            lock (this.lockObject)
            {
                ThreadSafeStringWriter.State?.Value?.Remove(this.outputType);
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

        // Avoiding name GetStringBuilder because it is already present on the base class.
        private StringBuilder GetStringBuilderOrNull()
        {
            if (State.Value == null)
            {
                return null;
            }
            else if (!State.Value.TryGetValue(this.outputType, out var stringBuilder))
            {
                return null;
            }
            else
            {
                return stringBuilder;
            }
        }

        private StringBuilder GetOrAddStringBuilder()
        {
            if (State.Value == null)
            {
                // The storage for the current async operation is empty
                // create the array and appropriate stringbuilder.
                // Avoid looking up the value after we add it to the dictionary.
                var sb = new StringBuilder();
                State.Value = new Dictionary<string, StringBuilder> { [this.outputType] = sb };
                return sb;
            }
            else if (!State.Value.TryGetValue(this.outputType, out var stringBuilder))
            {
                // The storage for the current async operation has the dictionary, but not the key
                // for the output type, add it, and avoid looking up the value again.
                var sb = new StringBuilder();
                State.Value.Add(this.outputType, sb);
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
}