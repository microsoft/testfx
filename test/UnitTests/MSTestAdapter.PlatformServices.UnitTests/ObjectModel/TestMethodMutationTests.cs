// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel;

public sealed class TestMethodMutationTests : TestContainer
{
    public void Constructor_WhenTheAssemblyNameIsMissing_Throws()
    {
        // The assembly name is what every later source/deployment decision keys off, so a test method without one
        // must be refused at construction rather than blow up far from the cause.
        Action nullName = () => _ = new TestMethod("M", "C", null!, displayName: null);
        nullName.Should().Throw<ArgumentNullException>().WithParameterName("assemblyName");

        Action emptyName = () => _ = new TestMethod("M", "C", string.Empty, displayName: null);
        emptyName.Should().Throw<ArgumentException>().WithParameterName("assemblyName");

        Action whitespaceName = () => _ = new TestMethod("M", "C", "   ", displayName: null);
        whitespaceName.Should().Throw<ArgumentException>().WithParameterName("assemblyName");
    }

    public void ManagedTypeName_IsComputedOnceAndThenCached()
    {
        // The generic arguments are cut off, which allocates a new string every time it is computed; handing back
        // the very same instance on the second read is what proves that the first result was stored.
        var method = new TestMethod("M", "Ns.Cls[System.Int32]", "A", displayName: null);

        string? first = method.ManagedTypeName;

        first.Should().Be("Ns.Cls");
        method.ManagedTypeName.Should().BeSameAs(first);
    }

    public void ManagedTypeName_StripsTheGenericArgumentsOnlyWhenThereAreSome()
    {
        new TestMethod("M", "Ns.Cls", "A", displayName: null).ManagedTypeName.Should().Be("Ns.Cls");
        new TestMethod("M", "Ns.Cls[System.Int32,System.String]", "A", displayName: null).ManagedTypeName.Should().Be("Ns.Cls");

        // A name that opens the generic-argument list at its very first character has nothing in front of it.
        new TestMethod("M", "[System.Int32]", "A", displayName: null).ManagedTypeName.Should().BeEmpty();
    }

    public void CloneWithUpdatedSource_ReturnsACloneThatTargetsTheNewSource()
    {
        MethodInfo methodInfo = typeof(TestMethodMutationTests).GetMethod(nameof(CloneWithUpdatedSource_ReturnsACloneThatTargetsTheNewSource))!;
        var original = new TestMethod("M", "C", "OldAssembly", displayName: null) { MethodInfo = methodInfo };

        TestMethod clone = original.CloneWithUpdatedSource("NewAssembly");

        clone.Should().NotBeNull();
        clone.Should().NotBeSameAs(original);
        clone.AssemblyName.Should().Be("NewAssembly");
        clone.MethodInfo.Should().BeNull("a method resolved from the old source does not belong to the new one");
        clone.Name.Should().Be("M");
        clone.FullClassName.Should().Be("C");
        original.AssemblyName.Should().Be("OldAssembly");
        original.MethodInfo.Should().BeSameAs(methodInfo);
    }
}
