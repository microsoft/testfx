// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Discovery;

public sealed class TestPartitionTests : TestContainer
{
    private const string Source = "Acme.Tests.dll";
    private const string Checkout = "Acme.CheckoutTests";
    private const string Cart = "Acme.CartTests";
    private const string Shipping = "Acme.ShippingTests";

    public void TryParse_ValidDecimalBounds_ReturnsExactIndexAndCount()
    {
        foreach ((string value, int index, int count) in new[]
        {
            ("1/1", 1, 1),
            ("1/4", 1, 4),
            ("4/4", 4, 4),
            ("0001/0004", 1, 4),
            ("1/2147483647", 1, int.MaxValue),
            ("2147483646/2147483647", int.MaxValue - 1, int.MaxValue),
            ("2147483647/2147483647", int.MaxValue, int.MaxValue),
        })
        {
            TestPartition.TryParse(value, out TestPartition? partition).Should().BeTrue();
            partition!.Index.Should().Be(index);
            partition.Count.Should().Be(count);
        }
    }

    public void TryParse_MalformedValues_ReturnsFalseAndNull()
    {
        // Raw invalid data stays in the test body, never in framework-generated display names.
        foreach ((string id, string value) in new[]
        {
            ("empty", string.Empty),
            ("missing-separator", "12"),
            ("missing-index", "/2"),
            ("missing-count", "1/"),
            ("extra-separator", "1/2/3"),
            ("empty-middle", "1//2"),
            ("leading-space", " 1/2"),
            ("trailing-space", "1/2 "),
            ("index-space", "1 /2"),
            ("count-space", "1/ 2"),
            ("plus-index", "+1/2"),
            ("plus-count", "1/+2"),
            ("negative-index", "-1/2"),
            ("negative-count", "1/-2"),
            ("decimal-point", "1.0/2"),
            ("group-separator", "1,000/1000"),
            ("hexadecimal", "0x1/2"),
            ("exponent", "1/2e1"),
            ("letters", "one/two"),
            ("arabic-index", "\u0661/2"),
            ("arabic-count", "1/\u0662"),
            ("fullwidth-index", "\uff11/2"),
            ("nul", "1/2\0"),
            ("form-feed", "1/\u000c2"),
            ("newline", "1/2\n"),
        })
        {
            TestPartition.TryParse(value, out TestPartition? partition).Should().BeFalse(id);
            partition.Should().BeNull(id);
        }
    }

    public void TryParse_ZeroOutOfRangeAndOverflow_ReturnsFalseAndNull()
    {
        foreach ((string id, string value) in new[]
        {
            ("zero-index", "0/1"),
            ("zero-count", "1/0"),
            ("both-zero", "0/0"),
            ("index-above-count", "2/1"),
            ("max-index-above-count", "2147483647/2147483646"),
            ("index-overflow", "2147483648/2147483647"),
            ("count-overflow", "1/2147483648"),
            ("both-overflow", "2147483648/2147483648"),
            ("long-integer", "999999999999999999999999999999/2"),
        })
        {
            TestPartition.TryParse(value, out TestPartition? partition).Should().BeFalse(id);
            partition.Should().BeNull(id);
        }
    }

    public void Constructor_InvalidBounds_ThrowsForCorrectArgument()
    {
        foreach ((int index, int count, string parameter) in new[]
        {
            (1, 0, "count"),
            (1, -1, "count"),
            (0, 1, "index"),
            (-1, 1, "index"),
            (2, 1, "index"),
        })
        {
            Action create = () => _ = new TestPartition(index, count);
            create.Should().ThrowExactly<ArgumentOutOfRangeException>().Which.ParamName.Should().Be(parameter);
        }
    }

    public void SelectTests_OneOfOne_PreservesCollectionReferencesOrderAndMetadata()
    {
        UnitTestElement first = Element(Cart, "Buy");
        UnitTestElement second = Element(Checkout, "Prepare");
        first.Dependencies = [new(Checkout, "Prepare", proceedOnFailure: false)];
        object handle = new();
        var uid = Guid.Parse("71e451e4-8a84-4526-ae58-ea1800667a84");
        first.HostRecordingHandle = handle;
        first.CachedTestNodeUid = uid;
        first.TestMethod.ActualData = [17, "seventeen"];
        first.TestMethod.TestCaseIndex = 3;
        TestDependencyInfo[] originalDependencies = first.Dependencies!;
        ICollection<UnitTestElement> tests = Array.AsReadOnly(new[] { first, second, first });

        ICollection<UnitTestElement> selected = new TestPartition(1, 1).SelectTests(
            tests, [new($"{Checkout}.Prepare", $"{Cart}.Buy", proceedOnFailure: true)]);

        selected.Should().BeSameAs(tests);
        selected.Should().Equal(first, second, first);
        first.Dependencies.Should().BeSameAs(originalDependencies);
        first.Dependencies.Should().ContainSingle();
        second.Dependencies.Should().BeNull();
        first.HostRecordingHandle.Should().BeSameAs(handle);
        first.CachedTestNodeUid.Should().Be(uid);
        first.TestMethod.ActualData.Should().Equal(17, "seventeen");
        first.TestMethod.TestCaseIndex.Should().Be(3);
    }

    public void SelectTests_EmptyInputAndUnassignedBucket_ReturnsEmpty()
    {
        new TestPartition(2, 4).SelectTests(Array.Empty<UnitTestElement>(), null).Should().BeEmpty();
        UnitTestElement[] tests = [Element(Checkout, "Prepare"), Element(Cart, "Buy")];
        new TestPartition(3, 4).SelectTests(tests, null).Should().BeEmpty();
        new TestPartition(4, 4).SelectTests(tests, null).Should().BeEmpty();
        tests.Select(test => test.TestMethod.FullClassName).Should().Equal(Checkout, Cart);
    }

    public void SelectTests_IndependentGoldenVectors_SelectOnlyExpectedClasses()
    {
        UnitTestElement checkout = Element(Checkout, "Prepare");
        UnitTestElement cart = Element(Cart, "Buy");
        UnitTestElement[] tests = [cart, checkout];

        // Independently calculated SHA256 prefixes: 69a7b6b77714706c and 134e357316c9c3bd.
        AssertSelection(tests, 1, 4, [checkout]);
        AssertSelection(tests, 2, 4, [cart]);
        AssertSelection(tests, 5, 7, [checkout]);
        AssertSelection(tests, 7, 7, [cart]);
        AssertSelection(tests, 1248058845, int.MaxValue, [checkout]);
        AssertSelection(tests, 1030106788, int.MaxValue, [cart]);
        AssertSelection(tests, 1248058844, int.MaxValue, []);
        AssertSelection(tests, 1248058846, int.MaxValue, []);
    }

    public void SelectTests_Utf8AndHighBitGoldenVectors_UseFramedUnsignedHash()
    {
        UnitTestElement unicode = Element("Acme.Caf\u00e9Tests", "Coffee");
        AssertSelection([unicode], 1, 7, [unicode]);
        AssertSelection([unicode], 217128803, int.MaxValue, [unicode]);
        AssertSelection([unicode], 217128802, int.MaxValue, []);

        UnitTestElement checkout = Element(Checkout, "Prepare");
        UnitTestElement cart = Element(Cart, "Buy");
        cart.Dependencies = [new(Checkout, "Prepare", proceedOnFailure: false)];
        UnitTestElement[] connected = [checkout, cart];
        // Connected-component hash begins 8f5769973694d74f: unsigned, not a signed Int64.
        AssertSelection(connected, 4, 4, connected);
        AssertSelection(connected, 5, 7, connected);
        AssertSelection(connected, 1430497920, int.MaxValue, connected);
        AssertSelection(connected, 1430497919, int.MaxValue, []);
    }

    public void SelectTests_AllBuckets_FormDisjointCompleteIdentityPartition()
    {
        UnitTestElement[] tests =
        [
            Element(Cart, "Second"),
            Element(Checkout, "First"),
            Element(Cart, "First"),
            Element("Acme.Caf\u00e9Tests", "Coffee"),
            Element(Checkout, "Second"),
        ];
        foreach (int count in new[] { 1, 2, 4, 7 })
        {
            var seen = new List<UnitTestElement>();
            var classBuckets = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 1; index <= count; index++)
            {
                ICollection<UnitTestElement> selected = new TestPartition(index, count).SelectTests(tests, null);
                foreach (UnitTestElement element in selected)
                {
                    seen.Should().NotContain(element, "a test must not occur in two buckets");
                    seen.Add(element);
                    if (classBuckets.TryGetValue(element.TestMethod.FullClassName, out int previous))
                    {
                        previous.Should().Be(index, "a concrete class is atomic");
                    }
                    else
                    {
                        classBuckets.Add(element.TestMethod.FullClassName, index);
                    }
                }

                selected.Should().Equal(tests.Where(selected.Contains), "each bucket preserves source order");
            }

            seen.Should().BeEquivalentTo(tests);
            seen.Count.Should().Be(tests.Length);
        }
    }

    public void SelectTests_LongUtf8Fields_UseUInt32BigEndianLengths()
    {
        // Independently framed SHA256 vectors for 255, 256, and 257 UTF-8 bytes. These distinguish
        // a UInt32 length from a truncated one-byte length without duplicating production hashing.
        foreach ((int suffixLength, int expectedIndex) in new[]
        {
            (250, 192630967),
            (251, 550082359),
            (252, 1876193806),
        })
        {
            UnitTestElement element = Element("Acme." + new string('A', suffixLength), "Run");
            AssertSelection([element], expectedIndex, int.MaxValue, [element]);
            AssertSelection([element], expectedIndex - 1, int.MaxValue, []);
        }
    }

    public void SelectTests_RepeatedElements_PreservesMultiplicity()
    {
        UnitTestElement checkout = Element(Checkout, "Prepare");
        UnitTestElement cart = Element(Cart, "Buy");
        UnitTestElement[] tests = [checkout, cart, checkout];
        AssertSelection(tests, 1, 4, [checkout, checkout]);
        AssertSelection(tests, 2, 4, [cart]);
        tests.Should().Equal(checkout, cart, checkout);
    }

    public void SelectTests_ReorderedDiscoveryAndCheckoutPaths_PreserveClassPlacement()
    {
        foreach (string source in new[]
        {
            Source,
            Path.Combine(Path.GetTempPath(), "checkout one", "bin", Source),
            Path.Combine(Path.GetTempPath(), "other-checkout", "different-output", Source),
        })
        {
            UnitTestElement[] tests =
            [
                Element(Cart, "Second", source),
                Element(Checkout, "First", source),
                Element(Cart, "First", source),
                Element(Checkout, "Second", source),
            ];
            foreach (UnitTestElement[] ordered in new[] { tests, tests.Reverse().ToArray() })
            {
                AssertSelection(ordered, 1, 4, [.. ordered.Where(test => test.TestMethod.FullClassName == Checkout)]);
                AssertSelection(ordered, 2, 4, [.. ordered.Where(test => test.TestMethod.FullClassName == Cart)]);
            }
        }
    }

    public void SelectTests_ExpandedFoldedAndRuntimeRows_StayWithConcreteClass()
    {
        UnitTestElement normal = Element(Checkout, "Normal");
        UnitTestElement firstRow = Element(Checkout, "Rows");
        firstRow.TestMethod.DataType = DynamicDataType.ITestDataSource;
        firstRow.TestMethod.ActualData = [17];
        firstRow.TestMethod.TestCaseIndex = 0;
        firstRow.TestMethod.DisplayName = "row seventeen";
        UnitTestElement secondRow = Element(Checkout, "Rows");
        secondRow.TestMethod.DataType = DynamicDataType.ITestDataSource;
        secondRow.TestMethod.ActualData = [23];
        secondRow.TestMethod.TestCaseIndex = 1;
        secondRow.TestMethod.DisplayName = "row twenty-three";
        UnitTestElement folded = Element(Checkout, "Folded");
        folded.UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold;
        UnitTestElement runtimeOnly = Element(Checkout, "RuntimeOnly");
        runtimeOnly.TestMethod.DataType = DynamicDataType.None;
        UnitTestElement distractor = Element(Cart, "Other");
        UnitTestElement[] tests = [firstRow, distractor, folded, normal, secondRow, runtimeOnly];

        AssertSelection(tests, 1, 4, [firstRow, folded, normal, secondRow, runtimeOnly]);
        AssertSelection(tests, 2, 4, [distractor]);
        firstRow.TestMethod.ActualData.Should().Equal(17);
        secondRow.TestMethod.ActualData.Should().Equal(23);
        secondRow.TestMethod.TestCaseIndex.Should().Be(1);
        folded.UnfoldingStrategy.Should().Be(TestDataSourceUnfoldingStrategy.Fold);
    }

    public void SelectTests_InheritedMethods_UseConcreteClassRatherThanManagedOrDisplayName()
    {
        UnitTestElement checkout = new(new TestMethod(
            "Acme.BaseTests.Shared()", null, "Shared", Checkout, Source, "same inherited display name", null));
        UnitTestElement cart = new(new TestMethod(
            "Acme.BaseTests.Shared()", null, "Shared", Cart, Source, "same inherited display name", null));

        AssertSelection([cart, checkout], 1, 4, [checkout]);
        AssertSelection([cart, checkout], 2, 4, [cart]);
        checkout.TestMethod.FullClassName.Should().Be(Checkout);
        cart.TestMethod.FullClassName.Should().Be(Cart);
    }

    public void SelectTests_ProcessLocalSchedulingMetadata_DoesNotChangePlacementOrElements()
    {
        UnitTestElement checkout = Element(Checkout, "Prepare");
        UnitTestElement cart = Element(Cart, "Buy");
        ResourceLockInfo[] checkoutLocks = [new("same-process-resource", ResourceAccessMode.ReadWrite)];
        ResourceLockInfo[] cartLocks = [new("same-process-resource", ResourceAccessMode.Read)];
        checkout.ResourceLocks = checkoutLocks;
        cart.ResourceLocks = cartLocks;
        checkout.DoNotParallelize = true;
        cart.DoNotParallelize = true;

        AssertSelection([checkout, cart], 1, 4, [checkout]);
        AssertSelection([checkout, cart], 2, 4, [cart]);
        checkout.ResourceLocks.Should().BeSameAs(checkoutLocks);
        cart.ResourceLocks.Should().BeSameAs(cartLocks);
        checkout.ResourceLocks![0].Mode.Should().Be(ResourceAccessMode.ReadWrite);
        cart.ResourceLocks![0].Mode.Should().Be(ResourceAccessMode.Read);
        checkout.DoNotParallelize.Should().BeTrue();
        cart.DoNotParallelize.Should().BeTrue();
    }

    public void SelectTests_ResolvableAttributeDependencies_CoLocateWholeClasses()
    {
        foreach (string? targetMethod in new string?[] { "Prepare", null })
        {
            UnitTestElement[] tests =
            [
                Element(Cart, "Buy"),
                Element(Checkout, "Prepare"),
                Element(Cart, "Other"),
                Element(Checkout, "Unrelated"),
            ];
            tests[0].Dependencies = [new(Checkout, targetMethod, proceedOnFailure: false)];

            AssertSelection(tests, 4, 4, tests);
            AssertSelection(tests, 1, 4, []);
            AssertSelection(tests, 2, 4, []);
        }
    }

    public void SelectTests_TransitiveAndCyclicDependencies_UseOneSortedComponent()
    {
        UnitTestElement cart = Element(Cart, "Buy");
        UnitTestElement checkout = Element(Checkout, "Prepare");
        UnitTestElement shipping = Element(Shipping, "Ship");
        cart.Dependencies = [new(Checkout, "Prepare", false), new(Checkout, "Prepare", true)];
        checkout.Dependencies = [new(Shipping, null, false)];
        UnitTestElement[] tests = [shipping, checkout, cart];

        AssertSelection(tests, 2, 7, tests);
        AssertSelection(tests, 5, 7, []);
        shipping.Dependencies = [new(Cart, "Buy", false)];
        AssertSelection(tests, 2, 7, tests);
        UnitTestElement[] reversed = tests.Reverse().ToArray();
        AssertSelection(reversed, 2, 7, reversed);
        AssertSelection(reversed, 1341624019, int.MaxValue, reversed);
    }

    public void SelectTests_SameNamedOverloadsWithDifferentDependencies_CoLocateEveryTargetClass()
    {
        UnitTestElement firstOverload = new(new TestMethod("Buy", null, "Buy", Cart, Source, "Buy without arguments", null))
        {
            Dependencies = [new(Checkout, "Prepare", false)],
        };
        UnitTestElement secondOverload = new(new TestMethod("Buy(System.Int32)", null, "Buy", Cart, Source, "Buy with integer", "System.Int32"))
        {
            Dependencies = [new(Shipping, "Ship", false)],
        };
        UnitTestElement[] tests = [Element(Checkout, "Prepare"), secondOverload, Element(Shipping, "Ship"), firstOverload];

        // The independent three-class oracle is bucket 2/7. Deduplicating Buy by method name loses
        // an edge and splits the component; reverse discovery order so neither overload can be discarded.
        foreach (UnitTestElement[] ordered in new[] { tests, tests.Reverse().ToArray() })
        {
            for (int index = 1; index <= 7; index++)
            {
                UnitTestElement[] expected = index == 2 ? ordered : [];
                AssertSelection(ordered, index, 7, expected);
            }
        }
    }

    public void SelectTests_SameClassDependencies_DoNotMergeIndependentClasses()
    {
        UnitTestElement buy = Element(Cart, "Buy");
        UnitTestElement other = Element(Cart, "Other");
        UnitTestElement prepare = Element(Checkout, "Prepare");
        buy.Dependencies = [new(null, "Other", false), new(Cart, null, false)];
        AssertSelection([buy, prepare, other], 2, 4, [buy, other]);
        AssertSelection([buy, prepare, other], 1, 4, [prepare]);
    }

    public void SelectTests_ConfiguredDependencies_MergeWithAttributesWithoutMutation()
    {
        UnitTestElement cart = Element(Cart, "Buy");
        UnitTestElement checkout = Element(Checkout, "Prepare");
        UnitTestElement shipping = Element(Shipping, "Ship");
        TestDependencyInfo[] attributes = [new(Checkout, "Prepare", false)];
        cart.Dependencies = attributes;
        TestDependencyDeclaration[] configuration = [new($"{Checkout}.Prepare", $"{Shipping}.Ship", true)];
        UnitTestElement[] tests = [shipping, cart, checkout];

        AssertSelection(tests, 2, 7, tests, configuration);
        AssertSelection(tests, 5, 7, [], configuration);
        tests.Should().Equal(shipping, cart, checkout);
        cart.Dependencies.Should().BeSameAs(attributes);
        cart.Dependencies.Should().ContainSingle();
        checkout.Dependencies.Should().BeNull();
        shipping.Dependencies.Should().BeNull();
        configuration[0].Dependent.Should().Be($"{Checkout}.Prepare");
        configuration[0].Prerequisite.Should().Be($"{Shipping}.Ship");
        configuration[0].ProceedOnFailure.Should().BeTrue();
    }

    public void SelectTests_ConfiguredMethodAndWildcardEdges_CoLocateWithoutMutatingOriginals()
    {
        foreach (string dependent in new[] { $"{Cart}.Buy", $"{Cart}.*" })
        {
            foreach (string prerequisite in new[] { $"{Checkout}.Prepare", $"{Checkout}.*" })
            {
                foreach (bool proceedOnFailure in new[] { false, true })
                {
                    UnitTestElement[] tests = [Element(Cart, "Buy"), Element(Checkout, "Prepare"), Element(Cart, "Other")];
                    TestDependencyDeclaration[] configuration = [new(dependent, prerequisite, proceedOnFailure)];
                    AssertSelection(tests, 4, 4, tests, configuration);
                    AssertSelection(tests, 1, 4, [], configuration);
                    tests.Select(test => test.Dependencies).Should().OnlyContain(value => value == null);
                }
            }
        }
    }

    public void SelectTests_UnresolvedDependencies_DoNotCreatePhantomComponents()
    {
        foreach (TestDependencyInfo dependency in new[]
        {
            new TestDependencyInfo("Acme.MissingTests", "Prepare", false),
            new TestDependencyInfo(Checkout, "MissingMethod", false),
        })
        {
            UnitTestElement cart = Element(Cart, "Buy");
            UnitTestElement checkout = Element(Checkout, "Prepare");
            cart.Dependencies = [dependency];
            TestDependencyDeclaration[] configuration =
            [
                new("malformed", $"{Checkout}.Prepare", false),
                new($"{Cart}.Buy", "malformed", false),
                new("Acme.MissingTests.Buy", $"{Checkout}.Prepare", false),
                new($"{Cart}.MissingMethod", $"{Checkout}.Prepare", false),
                new($"{Cart}.Buy", $"{Checkout}.MissingMethod", false),
            ];
            AssertSelection([cart, checkout], 1, 4, [checkout], configuration);
            AssertSelection([cart, checkout], 2, 4, [cart], configuration);
            cart.Dependencies.Should().Equal(dependency);
            checkout.Dependencies.Should().BeNull();
        }
    }

    private static UnitTestElement Element(string className, string methodName, string source = Source)
        => new(new TestMethod(methodName, className, source, displayName: null));

    private static void AssertSelection(
        ICollection<UnitTestElement> tests,
        int index,
        int count,
        UnitTestElement[] expected,
        TestDependencyDeclaration[]? configuration = null)
    {
        UnitTestElement[] original = [.. tests];
        ICollection<UnitTestElement> selected = new TestPartition(index, count).SelectTests(tests, configuration);
        selected.Should().Equal(expected, "bucket {0}/{1} has fixed original identities in source order", index, count);
        tests.Should().Equal(original, "selection must not reorder or remove input elements");
        UnitTestElement[] actual = [.. selected];
        for (int i = 0; i < expected.Length; i++)
        {
            actual[i].Should().BeSameAs(expected[i]);
        }
    }
}
