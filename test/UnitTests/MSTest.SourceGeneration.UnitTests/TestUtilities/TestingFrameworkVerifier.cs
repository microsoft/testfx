// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.TestUtilities;

internal sealed class TestingFrameworkVerifier : IVerifier
{
    public TestingFrameworkVerifier()
        : this([])
    {
    }

    internal TestingFrameworkVerifier(ImmutableStack<string> context)
        => Context = context ?? throw new ArgumentNullException(nameof(context));

    public ImmutableStack<string> Context { get; }

    public void Empty<T>(string collectionName, IEnumerable<T> collection) => Assert.IsFalse(collection?.Any() == true, CreateMessage($"expected '{collectionName}' to be empty, contains '{collection?.Count()}' elements"));

    public void Equal<T>(T expected, T actual, string? message = null)
    {
        if (message is null && Context.IsEmpty)
        {
            Assert.AreEqual(expected, actual);
        }
        else
        {
            Assert.AreEqual(expected, actual, CreateMessage(message));
        }
    }

    [DoesNotReturn]
    public void Fail(string? message = null)
    {
        if (message is null && Context.IsEmpty)
        {
            Assert.Fail();
        }
        else
        {
            Assert.Fail(CreateMessage(message));
        }

        throw new InvalidOperationException("This program location is thought to be unreachable.");
    }

    public void False([DoesNotReturnIf(true)] bool assert, string? message = null)
    {
        if (message is null && Context.IsEmpty)
        {
            Assert.IsFalse(assert);
        }
        else
        {
            Assert.IsFalse(assert, CreateMessage(message));
        }
    }

    public void LanguageIsSupported(string language) => Assert.IsFalse(language is not LanguageNames.CSharp and not LanguageNames.VisualBasic, CreateMessage($"Unsupported Language: '{language}'"));

    public void NotEmpty<T>(string collectionName, IEnumerable<T> collection) => Assert.IsTrue(collection?.Any() == true, CreateMessage($"expected '{collectionName}' to be non-empty, contains"));

    public IVerifier PushContext(string context)
    {
        Assert.AreEqual(typeof(TestingFrameworkVerifier), GetType());
        return new TestingFrameworkVerifier(Context.Push(context));
    }

    public void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, IEqualityComparer<T>? equalityComparer = null, string? message = null)
    {
        var comparer = new SequenceEqualEnumerableEqualityComparer<T>(equalityComparer);
        bool areEqual = comparer.Equals(expected, actual);
        if (!areEqual)
        {
            Assert.Fail(CreateMessage(message));
        }
    }

    public void True([DoesNotReturnIf(false)] bool assert, string? message = null)
    {
        if (message is null && Context.IsEmpty)
        {
            Assert.IsTrue(assert);
        }
        else
        {
            Assert.IsTrue(assert, CreateMessage(message));
        }
    }

    private string CreateMessage(string? message)
    {
        foreach (string frame in Context)
        {
            message = "Context: " + frame + Environment.NewLine + message;
        }

        return message ?? string.Empty;
    }

    private sealed class SequenceEqualEnumerableEqualityComparer<T> : IEqualityComparer<IEnumerable<T>?>
    {
        private readonly IEqualityComparer<T> _itemEqualityComparer;

        public SequenceEqualEnumerableEqualityComparer(IEqualityComparer<T>? itemEqualityComparer)
        {
            _itemEqualityComparer = itemEqualityComparer ?? EqualityComparer<T>.Default;
        }

        public bool Equals(IEnumerable<T>? x, IEnumerable<T>? y)
            => ReferenceEquals(x, y)
            || (x is not null && y is not null && x.SequenceEqual(y, _itemEqualityComparer));

        public int GetHashCode(IEnumerable<T>? obj)
        {
            if (obj is null)
            {
                return 0;
            }

            // From System.Tuple
            //
            // The suppression is required due to an invalid contract in IEqualityComparer<T>
            // https://github.com/dotnet/runtime/issues/30998
            return obj
                .Select(item => _itemEqualityComparer.GetHashCode(item!))
                .Aggregate(
                    0,
                    (aggHash, nextHash) => ((aggHash << 5) + aggHash) ^ nextHash);
        }
    }
}
