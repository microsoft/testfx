// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    /// StringWriter which has thread safe ToString().
    /// </summary>
    public class ThreadSafeStringWriter : StringWriter
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

        public static ThreadSafeStringWriter Instance { get; } = new ThreadSafeStringWriter(CultureInfo.InvariantCulture);

        public static List<StringBuilder> AdditionalOutputs { get; } = new List<StringBuilder>();

        public static StringBuilder AllOutput { get; } = new StringBuilder();

        public static void SetStringBuilder(StringBuilder stringBuilder)
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
            }
        }

        /// <inheritdoc/>
        public override void Write(string value)
        {
            lock (this.lockObject)
            {
            }
        }

        public override void WriteLine(string value)
        {
        }

        /// <inheritdoc/>
        public override void Write(char[] buffer, int index, int count)
        {
            lock (this.lockObject)
            {
                // dunno
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