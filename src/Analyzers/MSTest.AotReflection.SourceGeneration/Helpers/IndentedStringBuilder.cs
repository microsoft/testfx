// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace MSTest.AotReflection.SourceGeneration.Helpers;

/// <summary>
/// A small <see cref="StringBuilder"/> wrapper that tracks an indentation level so the
/// generated code stays readable. Kept private to this PoC to avoid pulling in the
/// equivalent helper that lives in <c>MSTest.SourceGeneration</c>.
/// </summary>
internal sealed class IndentedStringBuilder
{
    private const string IndentUnit = "    ";
    private readonly StringBuilder _builder = new();
    private int _indent;
    private bool _needsIndent = true;

    public IndentedStringBuilder AppendLine(string value)
    {
        EnsureIndent();
        _builder.AppendLine(value);
        _needsIndent = true;
        return this;
    }

    public IndentedStringBuilder AppendLine()
    {
        _builder.AppendLine();
        _needsIndent = true;
        return this;
    }

    public IndentedStringBuilder Append(string value)
    {
        EnsureIndent();
        _builder.Append(value);
        return this;
    }

    public IDisposable Block(string? header = null)
    {
        if (header is not null)
        {
            AppendLine(header);
        }

        AppendLine("{");
        _indent++;
        return new BlockScope(this);
    }

    public override string ToString() => _builder.ToString();

    private void EnsureIndent()
    {
        if (!_needsIndent)
        {
            return;
        }

        for (int i = 0; i < _indent; i++)
        {
            _builder.Append(IndentUnit);
        }

        _needsIndent = false;
    }

    private sealed class BlockScope : IDisposable
    {
        private readonly IndentedStringBuilder _owner;
        private bool _closed;

        public BlockScope(IndentedStringBuilder owner) => _owner = owner;

        public void Dispose()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _owner._indent--;
            _owner.AppendLine("}");
        }
    }
}
