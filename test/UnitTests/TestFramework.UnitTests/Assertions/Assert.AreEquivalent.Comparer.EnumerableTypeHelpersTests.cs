// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class AreEquivalentComparerEnumerableTypeHelpersTests : TestContainer
{
    public AreEquivalentComparerEnumerableTypeHelpersTests()
        => EquivalenceComparerCaches.Reset();

    protected override void Dispose(bool disposing)
    {
        try
        {
            EquivalenceComparerCaches.Reset();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public void AreEquivalent_EnumerableDeclaredElementType_PrecedesRuntimeElementType()
    {
        IEnumerable<BaseEquatable> expected = new DerivedEquatable[]
        {
            new("same-key", "expected-only"),
        };
        IEnumerable<BaseEquatable> actual = new DerivedEquatable[]
        {
            new("same-key", "actual-only"),
        };

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_EnumerableRuntimeElementType_IsResolvedWhenDeclaredAsObject()
    {
        object expected = new List<RuntimeEquatable>
        {
            new("same-key", "expected-only"),
        };
        object actual = new List<RuntimeEquatable>
        {
            new("same-key", "actual-only"),
        };

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_NonGenericCollections_UsesExpectedRuntimeElementType()
    {
        IEnumerable expected = new List<FallbackEquatable>
        {
            new("same-key", "expected-only"),
        };
        IEnumerable actual = new NonGenericEnumerable(new FallbackEquatable("same-key", "actual-only"));

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_NonGenericCollections_UsesActualRuntimeElementType()
    {
        IEnumerable expected = new NonGenericEnumerable(new FallbackEquatable("same-key", "expected-only"));
        IEnumerable actual = new List<FallbackEquatable>
        {
            new("same-key", "actual-only"),
        };

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_NonGenericExpectedElementOutsideActualType_FallsBackToMembers()
    {
        IEnumerable expected = new NonGenericEnumerable(new StructuralEnumerableValue("expected-key", "same-visible-value"));
        IEnumerable actual = new List<FallbackEquatable>
        {
            new("actual-key", "same-visible-value"),
        };
        Action compare = () => Assert.AreEquivalent(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("Mismatch at '[0].Key'");
        failure.ExpectedText.Should().Be("\"expected-key\"");
        failure.ActualText.Should().Be("\"actual-key\"");
    }

    public void AreEquivalent_EnumerableInterfaceSelection_PrefersSpecificElementType()
    {
        object expected = new MultiInterfaceEnumerable(new SpecificEquatable("same-key", "expected-only"));
        object actual = new MultiInterfaceEnumerable(new SpecificEquatable("same-key", "actual-only"));

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_MultidimensionalArrayMismatch_ReportsFlattenedElementIndexAndValues()
    {
        int[,] expected =
        {
            { 1, 2 },
            { 3, 4 },
        };
        int[,] actual =
        {
            { 1, 2 },
            { 9, 4 },
        };
        Action compare = () => Assert.AreEquivalent(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("Mismatch at '[2]'");
        failure.ExpectedText.Should().Be("3");
        failure.ActualText.Should().Be("9");
    }

    public void AreEquivalent_MultidimensionalArray_UsesRuntimeElementTypeWhenDeclaredAsObject()
    {
        object expected = new AlwaysEqualArrayElement[1, 1]
        {
            { new("expected") },
        };
        object actual = new AlwaysEqualArrayElement[1, 1]
        {
            { new("actual") },
        };

        Assert.AreEquivalent<object>(expected, actual);
    }

    private class BaseEquatable(string key) : IEquatable<BaseEquatable>
    {
        public string Key { get; } = key;

        public bool Equals(BaseEquatable? other)
            => other is not null && Key == other.Key;
    }

    private sealed class DerivedEquatable(string key, string derivedValue) : BaseEquatable(key)
    {
        public string DerivedValue { get; } = derivedValue;
    }

    private sealed class RuntimeEquatable(string key, string visibleValue) : IEquatable<RuntimeEquatable>
    {
        public string Key { get; } = key;

        public string VisibleValue { get; } = visibleValue;

        public bool Equals(RuntimeEquatable? other)
            => other is not null && Key == other.Key;
    }

    private sealed class SpecificEquatable(string key, string visibleValue) : IEquatable<SpecificEquatable>
    {
        public string Key { get; } = key;

        public string VisibleValue { get; } = visibleValue;

        public bool Equals(SpecificEquatable? other)
            => other is not null && Key == other.Key;
    }

    private sealed class FallbackEquatable(string key, string visibleValue) : IEquatable<FallbackEquatable>
    {
        public string Key { get; } = key;

        public string VisibleValue { get; } = visibleValue;

        public bool Equals(FallbackEquatable? other)
            => other is not null && Key == other.Key;
    }

    private sealed class StructuralEnumerableValue(string key, string visibleValue)
    {
        public string Key { get; } = key;

        public string VisibleValue { get; } = visibleValue;
    }

    private sealed class NonGenericEnumerable : IEnumerable
    {
        private readonly object[] _items;

        public NonGenericEnumerable(params object[] items)
            => _items = items;

        public IEnumerator GetEnumerator()
            => _items.GetEnumerator();
    }

    private sealed class MultiInterfaceEnumerable : IEnumerable<object>, IEnumerable<SpecificEquatable>
    {
        private readonly SpecificEquatable[] _items;

        public MultiInterfaceEnumerable(params SpecificEquatable[] items)
            => _items = items;

        IEnumerator<object> IEnumerable<object>.GetEnumerator()
        {
            foreach (SpecificEquatable item in _items)
            {
                yield return item;
            }
        }

        IEnumerator<SpecificEquatable> IEnumerable<SpecificEquatable>.GetEnumerator()
            => ((IEnumerable<SpecificEquatable>)_items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => _items.GetEnumerator();
    }

    private sealed class AlwaysEqualArrayElement(string value) : IEquatable<AlwaysEqualArrayElement>
    {
        public string Value { get; } = value;

        public bool Equals(AlwaysEqualArrayElement? other)
            => other is not null;
    }
}
