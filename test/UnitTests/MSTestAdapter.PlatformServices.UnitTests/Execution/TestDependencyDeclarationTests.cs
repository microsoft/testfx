// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

/// <summary>
/// Tests for the <c>testconfig.json</c> side of test dependencies - the edges declared outside the test
/// source, which have to end up indistinguishable from attribute-declared ones.
/// </summary>
public sealed class TestDependencyDeclarationTests : TestContainer
{
    private const string ClassA = "Ns.ClassA";

    private static UnitTestElement CreateElement(string className, string methodName)
        => new(new TestMethod(methodName, className, "DummyAssembly", displayName: null));

    public void ApplyAll_AttachesTheDeclaredEdgeToTheNamedTest()
    {
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        bool applied = TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration($"{ClassA}.PlaceOrder", $"{ClassA}.Setup", proceedOnFailure: false)],
            tests,
            logger: null);

        applied.Should().BeTrue();
        tests[0].Dependencies.Should().BeNull();
        tests[1].Dependencies.Should().ContainSingle();
        tests[1].Dependencies![0].TargetMethodName.Should().Be("Setup");
    }

    public void ApplyAll_WhenAWildcardDependentCoversThePrerequisite_DoesNotMakeItDependOnItself()
    {
        // "Ns.ClassA.*" expands onto every test of the class, including Setup - the prerequisite itself. The
        // declaration means "every test of this class waits for Setup", so that generated self-edge has to be
        // dropped, exactly as discovery drops it for a class-level [DependsOn]. Keeping it would report Setup
        // as a cycle and skip the whole class.
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration($"{ClassA}.*", $"{ClassA}.Setup", proceedOnFailure: false)],
            tests,
            logger: null);

        tests[0].Dependencies.Should().BeNull();
        tests[1].Dependencies.Should().ContainSingle();
        tests[1].Dependencies![0].TargetMethodName.Should().Be("Setup");
    }

    public void ApplyAll_WhenAWildcardDependentTargetsAnotherClass_KeepsEveryEdge()
    {
        // The self-edge suppression must not fire across classes: a same-named method elsewhere is a genuine
        // prerequisite.
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration($"{ClassA}.*", "Ns.Other.Setup", proceedOnFailure: false)],
            tests,
            logger: null);

        tests[0].Dependencies.Should().ContainSingle();
        tests[1].Dependencies.Should().ContainSingle();
    }

    public void ApplyAll_WhenTheDependentMatchesNothing_ReportsNothingApplied()
    {
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup")];

        bool applied = TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration("Ns.Missing.Test", $"{ClassA}.Setup", proceedOnFailure: false)],
            tests,
            logger: null);

        applied.Should().BeFalse();
        tests[0].Dependencies.Should().BeNull();
    }

    public void ApplyAll_WhenAReferenceIsMalformed_SkipsThatDeclaration()
    {
        // A bare identifier could be a class or a method; guessing would point the edge at the wrong thing.
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        bool applied = TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration("PlaceOrder", "Setup", proceedOnFailure: false)],
            tests,
            logger: null);

        applied.Should().BeFalse();
        tests[1].Dependencies.Should().BeNull();
    }

    public void ApplyAll_PreservesProceedOnFailure()
    {
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "Audit")];

        TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration($"{ClassA}.Audit", $"{ClassA}.Setup", proceedOnFailure: true)],
            tests,
            logger: null);

        tests[1].Dependencies![0].ProceedOnFailure.Should().BeTrue();
    }
}
