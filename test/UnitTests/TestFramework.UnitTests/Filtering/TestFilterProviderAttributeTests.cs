// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

#pragma warning disable MSTESTEXP // Exercise the experimental adapter-facing registration contract.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Filtering;

public sealed class TestFilterProviderAttributeTests : TestContainer
{
    public void GenericProvider_CreatesTheRegisteredFilterAndPreservesRunSkipDecisions()
    {
        ITestFilterProviderAttribute[] providers =
        [
            new TestFilterProviderAttribute<CategoryFilter>(),
            new TestFilterProviderAttribute(typeof(CategoryFilter)),
        ];
        foreach (ITestFilterProviderAttribute provider in providers)
        {
            var filter = (ITestFilter)Activator.CreateInstance(provider.FilterType)!;
            TestFilterResult run = filter.Filter(new TestFilterContext { Categories = ["Smoke"] });
            TestFilterResult skip = filter.Filter(new TestFilterContext { Categories = ["Smoke", "Nightly"] });
            run.Action.Should().Be(TestFilterAction.Run);
            run.SkipReason.Should().BeNull();
            skip.Action.Should().Be(TestFilterAction.Skip);
            skip.SkipReason.Should().Be("Nightly policy requires explicit opt-in.");
        }
    }

    public void GenericProvider_StructFilterIsSupportedAndInvalidTypeArgumentsAreRejected()
    {
        ITestFilterProviderAttribute provider = new TestFilterProviderAttribute<DropFilter>();
        var filter = (ITestFilter)Activator.CreateInstance(provider.FilterType)!;
        filter.Filter(new TestFilterContext { MethodName = "Case" }).Action.Should().Be(TestFilterAction.Drop);

        Action invalid = () => typeof(TestFilterProviderAttribute<>).MakeGenericType(typeof(string));
        invalid.Should().ThrowExactly<ArgumentException>();
        Action nullType = () => _ = new TestFilterProviderAttribute(null!);
        nullType.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("filterType");
    }

    private sealed class CategoryFilter : ITestFilter
    {
        public CategoryFilter()
        {
        }

        public TestFilterResult Filter(TestFilterContext context)
            => context.Categories.Contains("Nightly")
                ? TestFilterResult.Skip("Nightly policy requires explicit opt-in.")
                : TestFilterResult.Run;
    }

    private readonly struct DropFilter : ITestFilter
    {
        public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Drop;
    }

    public void ContextMetadata_ProgrammaticPolicyDistinguishesManagedSignatureAndDuplicateTraits()
    {
        ITestFilterProviderAttribute provider = new TestFilterProviderAttribute<SignatureFilter>();
        var filter = (ITestFilter)Activator.CreateInstance(provider.FilterType)!;
        var context = new TestFilterContext
        {
            FullyQualifiedName = "Acme.Outer+Inner.Case",
            Namespace = "Acme",
            ClassName = "Outer+Inner",
            ManagedTypeName = "Acme.Outer+Inner`1",
            ManagedMethodName = "Case`1(System.Int32)",
            MethodArity = 1,
            ParameterTypeFullNames = ["System.Int32"],
            Priority = 7,
            Traits = [new("Owner", "one"), new("Owner", "two")],
        };
        filter.Filter(new TestFilterContext()).Action.Should().Be(TestFilterAction.Run);
        TestFilterResult matched = filter.Filter(context);
        matched.Action.Should().Be(TestFilterAction.Skip);
        matched.SkipReason.Should().Be("Matched managed signature and duplicate traits.");
        context.MethodArity = 2;
        filter.Filter(context).Action.Should().Be(TestFilterAction.Run);
        context.MethodArity = 1;
        context.Priority = 6;
        filter.Filter(context).Action.Should().Be(TestFilterAction.Run);
        context.Priority = 7;
        context.Traits = [new("Owner", "one")];
        filter.Filter(context).Action.Should().Be(TestFilterAction.Run);
    }

    private sealed class SignatureFilter : ITestFilter
    {
        public SignatureFilter()
        {
        }

        public TestFilterResult Filter(TestFilterContext context)
            => context.FullyQualifiedName == "Acme.Outer+Inner.Case"
                && context.Namespace == "Acme"
                && context.ClassName == "Outer+Inner"
                && context.ManagedTypeName == "Acme.Outer+Inner`1"
                && context.ManagedMethodName == "Case`1(System.Int32)"
                && context.MethodArity == 1
                && context.ParameterTypeFullNames is { Count: 1 }
                && context.ParameterTypeFullNames[0] == "System.Int32"
                && context.Priority == 7
                && context.Traits.Select(trait => (trait.Key, trait.Value)).SequenceEqual<(string Key, string? Value)>([("Owner", "one"), ("Owner", "two")])
                    ? TestFilterResult.Skip("Matched managed signature and duplicate traits.")
                    : TestFilterResult.Run;
    }
}
#endif
