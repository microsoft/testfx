// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.IO;

    /// <summary>
    /// StringWriter which has thread safe ToString().
    /// </summary>
    internal class ThreadSafeStringWriter : StringWriter
    {
        private readonly object lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadSafeStringWriter"/> class.
        /// </summary>
        /// <param name="formatProvider">
        /// The format provider.
        /// </param>
        public ThreadSafeStringWriter(IFormatProvider formatProvider)
            : base(formatProvider)
        {
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            lock (this.lockObject)
            {
                try
                {
                    return base.ToString();
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
                InvokeBaseClass(() => base.Write(value));
            }
        }

        /// <inheritdoc/>
        public override void Write(string value)
        {
            lock (this.lockObject)
            {
                InvokeBaseClass(() => base.Write(value));
            }
        }

        /// <inheritdoc/>
        public override void Write(char[] buffer, int index, int count)
        {
            lock (this.lockObject)
            {
                InvokeBaseClass(() => base.Write(buffer, index, count));
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            lock (this.lockObject)
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
}