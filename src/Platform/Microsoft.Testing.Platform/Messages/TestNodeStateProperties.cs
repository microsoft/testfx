// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Base class for test node state properties.
/// </summary>
public abstract class TestNodeStateProperty : IProperty, IEquatable<TestNodeStateProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Textual explanation of the node state.</param>
    protected TestNodeStateProperty(string? explanation)
        => Explanation = explanation;

    /// <summary>
    /// Gets the textual explanation of the node state.
    /// </summary>
    public string? Explanation { get; }

    private protected virtual void PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Explanation)} = ");
        builder.Append(Explanation);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(TestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(TestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation;

    /// <inheritdoc />
    public override int GetHashCode()
        => Explanation?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents test node that has been discovered.
/// </summary>
public sealed class DiscoveredTestNodeStateProperty : TestNodeStateProperty, IEquatable<DiscoveredTestNodeStateProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveredTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Textual explanation of the node.</param>
    public DiscoveredTestNodeStateProperty(string? explanation = null)
        : base(explanation)
    {
    }

    /// <summary>
    /// Gets cached instance of the <see cref="DiscoveredTestNodeStateProperty"/>.
    /// </summary>
    public static DiscoveredTestNodeStateProperty CachedInstance { get; } = new DiscoveredTestNodeStateProperty();

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(DiscoveredTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as DiscoveredTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(DiscoveredTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation;

    /// <inheritdoc />
    public override int GetHashCode()
        => Explanation?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents a test node that is being executed.
/// </summary>
public sealed class InProgressTestNodeStateProperty : TestNodeStateProperty, IEquatable<InProgressTestNodeStateProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InProgressTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Textual explanation of the node.</param>
    public InProgressTestNodeStateProperty(string? explanation = null)
        : base(explanation)
    {
    }

    /// <summary>
    /// Gets cached instance of the <see cref="InProgressTestNodeStateProperty"/>.
    /// </summary>
    public static InProgressTestNodeStateProperty CachedInstance { get; } = new InProgressTestNodeStateProperty();

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(InProgressTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as InProgressTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(InProgressTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation;

    /// <inheritdoc />
    public override int GetHashCode()
        => Explanation?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents a test node that has been executed and passed.
/// </summary>
public sealed class PassedTestNodeStateProperty : TestNodeStateProperty, IEquatable<PassedTestNodeStateProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PassedTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Textual explanation of the node.</param>
    public PassedTestNodeStateProperty(string? explanation = null)
        : base(explanation)
    {
    }

    /// <summary>
    /// Gets the cached instance of the <see cref="PassedTestNodeStateProperty"/> property.
    /// </summary>
    public static PassedTestNodeStateProperty CachedInstance { get; } = new PassedTestNodeStateProperty();

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(PassedTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as PassedTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(PassedTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation;

    /// <inheritdoc />
    public override int GetHashCode()
        => Explanation?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents a test node that has been skipped.
/// </summary>
public sealed class SkippedTestNodeStateProperty : TestNodeStateProperty, IEquatable<SkippedTestNodeStateProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkippedTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Textual explanation of the node.</param>
    public SkippedTestNodeStateProperty(string? explanation = null)
        : base(explanation)
    {
    }

    /// <summary>
    /// Gets the cached instance of the <see cref="SkippedTestNodeStateProperty"/> property.
    /// </summary>
    public static SkippedTestNodeStateProperty CachedInstance { get; } = new SkippedTestNodeStateProperty();

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(SkippedTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as SkippedTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(SkippedTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation;

    /// <inheritdoc />
    public override int GetHashCode()
        => Explanation?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents a test node that has been failed.
/// </summary>
public sealed class FailedTestNodeStateProperty : TestNodeStateProperty, IEquatable<FailedTestNodeStateProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FailedTestNodeStateProperty"/> class.
    /// </summary>
    public FailedTestNodeStateProperty()
        : base(default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FailedTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Failure explanation.</param>
    public FailedTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FailedTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="exception">Failure exception.</param>
    /// <param name="explanation">Failure explanation.</param>
    public FailedTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation) => Exception = exception;

    /// <summary>
    /// Gets the failure exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(FailedTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    private protected override void PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append(", ");
        builder.Append($"{nameof(Exception)} = ");
        builder.Append(Exception);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as FailedTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(FailedTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation && Equals(Exception, other.Exception);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Explanation, Exception);
}

/// <summary>
/// Property that represents an eventual error in the test node.
/// </summary>
public sealed class ErrorTestNodeStateProperty : TestNodeStateProperty, IEquatable<ErrorTestNodeStateProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorTestNodeStateProperty"/> class.
    /// </summary>
    public ErrorTestNodeStateProperty()
        : base(default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Error explanation.</param>
    public ErrorTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="exception">Error exception.</param>
    /// <param name="explanation">Error explanation.</param>
    public ErrorTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation) => Exception = exception;

    /// <summary>
    /// Gets the error exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(ErrorTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    private protected override void PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append(", ");
        builder.Append($"{nameof(Exception)} = ");
        builder.Append(Exception);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as ErrorTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(ErrorTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation && Equals(Exception, other.Exception);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Explanation, Exception);
}

/// <summary>
/// Property that represents an eventual timeout in the test node.
/// </summary>
public sealed class TimeoutTestNodeStateProperty : TestNodeStateProperty, IEquatable<TimeoutTestNodeStateProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutTestNodeStateProperty"/> class.
    /// </summary>
    public TimeoutTestNodeStateProperty()
        : base(default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Timeout explanation.</param>
    public TimeoutTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="exception">Timeout exception.</param>
    /// <param name="explanation">Timeout explanation.</param>
    public TimeoutTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation) => Exception = exception;

    /// <summary>
    /// Gets the timeout exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the timeout TimeSpan.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(TimeoutTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    private protected override void PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append($", {nameof(Exception)} = ");
        builder.Append(Exception);
        builder.Append($", {nameof(Timeout)} = ");
        builder.Append(Timeout);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TimeoutTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(TimeoutTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation && Equals(Exception, other.Exception) && Timeout == other.Timeout;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Explanation, Exception, Timeout);
}

/// <summary>
/// Property that represents an eventual cancellation of a test node.
/// </summary>
[Obsolete("CancelledTestNodeStateProperty is obsolete. Test frameworks should throw OperationCanceledException using the cancellation token passed by Microsoft.Testing.Platform instead of using this state.")]
public sealed class CancelledTestNodeStateProperty : TestNodeStateProperty, IEquatable<CancelledTestNodeStateProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CancelledTestNodeStateProperty"/> class.
    /// </summary>
    public CancelledTestNodeStateProperty()
        : base(default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelledTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Cancellation explanation.</param>
    public CancelledTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelledTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="exception">Cancellation exception.</param>
    /// <param name="explanation">Cancellation explanation.</param>
    public CancelledTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation) => Exception = exception;

    /// <summary>
    /// Gets the cancellation exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(CancelledTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    private protected override void PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append(", ");
        builder.Append($"{nameof(Exception)} = ");
        builder.Append(Exception);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as CancelledTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(CancelledTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation && Equals(Exception, other.Exception);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Explanation, Exception);
}
