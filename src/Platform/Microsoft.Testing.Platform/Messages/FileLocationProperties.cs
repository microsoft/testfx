// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Line position in a file.
/// </summary>
public readonly struct LinePosition : IEquatable<LinePosition>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LinePosition"/> struct.
    /// </summary>
    /// <param name="line">Line number.</param>
    /// <param name="column">Column number.</param>
    public LinePosition(int line, int column)
    {
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Gets the line number.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the column number.
    /// </summary>
    public int Column { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(LinePosition));
        builder.Append(" { ");
        builder.Append($"{nameof(Line)} = ");
        builder.Append(Line);
        builder.Append($", {nameof(Column)} = ");
        builder.Append(Column);
        builder.Append(' ');
        builder.Append('}');
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is LinePosition other && Equals(other);

    /// <inheritdoc />
    public bool Equals(LinePosition other)
        => Line == other.Line && Column == other.Column;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Line, Column);

    /// <summary>
    /// Implementation of the non-equality operator.
    /// </summary>
    public static bool operator !=(LinePosition left, LinePosition right)
        => !(left == right);

    /// <summary>
    /// Implementation of the equality operator.
    /// </summary>
    public static bool operator ==(LinePosition left, LinePosition right)
        => left.Equals(right);
}

/// <summary>
/// Line position span in a file.
/// </summary>
public readonly struct LinePositionSpan : IEquatable<LinePositionSpan>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LinePositionSpan"/> struct.
    /// </summary>
    /// <param name="start">Start line position.</param>
    /// <param name="end">End line position.</param>
    public LinePositionSpan(LinePosition start, LinePosition end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets the start line position.
    /// </summary>
    public LinePosition Start { get; }

    /// <summary>
    /// Gets the end line position.
    /// </summary>
    public LinePosition End { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(LinePositionSpan));
        builder.Append(" { ");
        builder.Append($"{nameof(Start)} = ");
        builder.Append(Start);
        builder.Append($", {nameof(End)} = ");
        builder.Append(End);
        builder.Append(' ');
        builder.Append('}');
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is LinePositionSpan other && Equals(other);

    /// <inheritdoc />
    public bool Equals(LinePositionSpan other)
        => Start.Equals(other.Start) && End.Equals(other.End);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Start, End);

    /// <summary>
    /// Implementation of the non-equality operator.
    /// </summary>
    public static bool operator !=(LinePositionSpan left, LinePositionSpan right)
        => !(left == right);

    /// <summary>
    /// Implementation of the equality operator.
    /// </summary>
    public static bool operator ==(LinePositionSpan left, LinePositionSpan right)
        => left.Equals(right);
}

/// <summary>
/// Base property that represents a file location.
/// </summary>
public abstract class FileLocationProperty : IProperty, IEquatable<FileLocationProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileLocationProperty"/> class.
    /// </summary>
    /// <param name="filePath">File path.</param>
    /// <param name="lineSpan">Line position.</param>
    protected FileLocationProperty(string filePath, LinePositionSpan lineSpan)
    {
        FilePath = filePath;
        LineSpan = lineSpan;
    }

    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the line position span.
    /// </summary>
    public LinePositionSpan LineSpan { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(FileLocationProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    private protected virtual void PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(FilePath)} = ");
        builder.Append(FilePath);
        builder.Append($", {nameof(LineSpan)} = ");
        builder.Append(LineSpan);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as FileLocationProperty);

    /// <inheritdoc />
    public bool Equals(FileLocationProperty? other)
        => other is not null && FilePath == other.FilePath && LineSpan.Equals(other.LineSpan);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(FilePath, LineSpan);
}

/// <summary>
/// Property that represents a file location for a test node.
/// </summary>
public sealed class TestFileLocationProperty : FileLocationProperty, IEquatable<TestFileLocationProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestFileLocationProperty"/> class.
    /// </summary>
    /// <param name="filePath">File path.</param>
    /// <param name="lineSpan">Line position.</param>
    public TestFileLocationProperty(string filePath, LinePositionSpan lineSpan)
        : base(filePath, lineSpan)
    {
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(TestFileLocationProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TestFileLocationProperty);

    /// <inheritdoc />
    public bool Equals(TestFileLocationProperty? other)
        => other is not null && FilePath == other.FilePath && LineSpan.Equals(other.LineSpan);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(FilePath, LineSpan);
}
