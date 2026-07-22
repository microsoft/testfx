// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal sealed partial class TestContextImplementation
{
    internal sealed class SynchronizedStringBuilder
    {
        private readonly StringBuilder _builder = new();

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Append(char value)
            => _builder.Append(value);

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Append(string? value)
            => _builder.Append(value);

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Append(char[] buffer, int index, int count)
            => _builder.Append(buffer, index, count);

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void AppendLine(string? value)
            => _builder.AppendLine(value);

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Clear()
            => _builder.Clear();

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal string GetAndClear()
        {
            string result = _builder.ToString();
            _builder.Clear();

            return result;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override string ToString()
            => _builder.ToString();
    }
}
