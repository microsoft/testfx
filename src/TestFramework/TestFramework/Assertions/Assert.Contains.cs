// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertSingleInterpolatedStringHandler<TItem>
    {
        private readonly StringBuilder? _builder;
        private readonly int _actualCount;
        private readonly TItem? _item;

        public AssertSingleInterpolatedStringHandler(int literalLength, int formattedCount, IEnumerable<TItem> collection, out bool shouldAppend)
        {
            _actualCount = collection.Count();
            shouldAppend = _actualCount != 1;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
            else
            {
                _item = collection.First();
            }
        }

        internal TItem ComputeAssertion(string assertionName)
        {
            if (_builder is not null)
            {
                ThrowAssertCountFailed(assertionName, 1, _actualCount, _builder.ToString());
            }

            return _item!;
        }

        public void AppendLiteral(string value)
            => _builder!.Append(value);

        public void AppendFormatted<T>(T value)
            => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value)
            => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null)
            => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format)
            => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment)
            => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value)
            => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    public static T ContainsSingle<T>(IEnumerable<T> collection)
        => ContainsSingle(collection, string.Empty, null);

    public static T ContainsSingle<T>(IEnumerable<T> collection, string? message)
        => ContainsSingle(collection, message, null);

#pragma warning disable IDE0060 // Remove unused parameter
    public static T ContainsSingle<T>(IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertSingleInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion("ContainsSingle");

    public static T ContainsSingle<T>(IEnumerable<T> collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        int actualCount = collection.Count();
        if (actualCount == 1)
        {
            return collection.First();
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertCountFailed("ContainsSingle", 1, actualCount, userMessage);

        // Unreachable code but compiler cannot work it out
        return default;
    }

    public static void Contains<T>(IEnumerable<T> collection, T expected)
        => Contains(collection, expected, string.Empty, null);

    public static void Contains<T>(IEnumerable<T> collection, T expected, string? message)
        => Contains(collection, expected, message, null);

    public static void Contains<T>(IEnumerable<T> collection, T expected, string? message, params object?[]? parameters)
    {
        if (!collection.Contains(expected))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("Contains", userMessage);
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer)
        => Contains(collection, expected, comparer, string.Empty, null);

    public static void Contains<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer, string? message)
        => Contains(collection, expected, comparer, message, null);

    public static void Contains<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer, string? message, params object?[]? parameters)
    {
        if (!collection.Contains(expected, comparer))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("Contains", userMessage);
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate)
        => Contains(collection, predicate, string.Empty, null);

    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message)
        => Contains(collection, predicate, message, null);

    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message, params object?[]? parameters)
    {
        if (!collection.Any(predicate))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("Contains", userMessage);
        }
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected)
        => DoesNotContain(collection, expected, string.Empty, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected, string? message)
        => DoesNotContain(collection, expected, message, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected, string? message, params object?[]? parameters)
    {
        if (collection.Contains(expected))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("DoesNotContain", userMessage);
        }
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer)
        => DoesNotContain(collection, expected, comparer, string.Empty, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer, string? message)
        => DoesNotContain(collection, expected, comparer, message, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer, string? message, params object?[]? parameters)
    {
        if (collection.Contains(expected, comparer))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("DoesNotContain", userMessage);
        }
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, Func<T, bool> predicate)
        => DoesNotContain(collection, predicate, string.Empty, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message)
        => DoesNotContain(collection, predicate, message, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message, params object?[]? parameters)
    {
        if (collection.Any(predicate))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("DoesNotContain", userMessage);
        }
    }
}
