// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class AssemblyPreloaderTests : TestContainer
{
    public void Preload_LoadsTransitiveReferencesOnceThroughTheAssemblyThatReferencesThem()
    {
        var graph = new FakeGraph();
        FakeAssembly root = graph.Add("Root", "A", "B");
        graph.Add("A", "Shared");
        graph.Add("B", "Shared");
        graph.Add("Shared");

        (int loaded, int failed) = graph.Preload(root);

        loaded.Should().Be(3);
        failed.Should().Be(0);
        graph.Loads.Should().Equal("Root->A", "Root->B", "A->Shared", "B->Shared");
    }

    public void Preload_SkipsFrameworkAndTestPlatformReferences()
    {
        var graph = new FakeGraph();
        FakeAssembly root = graph.Add(
            "Root",
            "System.Runtime",
            "mscorlib",
            "netstandard",
            "Microsoft.VisualStudio.TestPlatform.TestFramework",
            "MSTest.TestFramework",
            "Microsoft.Extensions.Logging",
            "Library");
        graph.Add("Microsoft.Extensions.Logging");
        graph.Add("Library");

        (int loaded, int failed) = graph.Preload(root);

        loaded.Should().Be(2);
        failed.Should().Be(0);
        graph.Loads.Should().Equal("Root->Microsoft.Extensions.Logging", "Root->Library");
    }

    public void Preload_ResolvesSharedNamesThroughEachReferrer()
    {
        var graph = new FakeGraph();
        FakeAssembly root = graph.Add("Root", "A", "B");
        graph.Add("A", "Shared");
        FakeAssembly b = graph.Add("B", "Shared");
        graph.Add("Shared");
        FakeAssembly otherShared = graph.Add("OtherContextShared", "OnlyInB");
        graph.Add("OnlyInB");

        (int loaded, int failed) = graph.Preload(root, (referrer, reference) =>
            referrer == b && reference.Name == "Shared" ? otherShared : graph.TryLoad(referrer, reference));

        loaded.Should().Be(5);
        failed.Should().Be(0);
        graph.Loads.Should().Contain("OtherContextShared->OnlyInB");
    }

    public void Preload_ReferencesResolvingToTheSameAssemblyAreWalkedOnce()
    {
        var graph = new FakeGraph();
        FakeAssembly root = graph.Add("Root", "Unified, Version=1.0.0.0", "Unified, Version=2.0.0.0");
        graph.Add("Unified", "Leaf");
        graph.Add("Leaf");

        (int loaded, int failed) = graph.Preload(root);

        loaded.Should().Be(2);
        failed.Should().Be(0);
        graph.Loads.Should().Equal("Root->Unified", "Root->Unified", "Unified->Leaf");
    }

    public void Preload_ReferenceThatCannotBeLoadedIsCountedAndDoesNotStopTheWalk()
    {
        var graph = new FakeGraph();
        FakeAssembly root = graph.Add("Root", "Missing", "Present");
        graph.Add("Present", "Leaf");
        graph.Add("Leaf");
        graph.MarkUnloadable("Missing");

        (int loaded, int failed) = graph.Preload(root);

        loaded.Should().Be(2);
        failed.Should().Be(1);
        graph.Loads.Should().Equal("Root->Missing", "Root->Present", "Present->Leaf");
    }

    public void Preload_StopsLoadingOnceTheRunIsCanceled()
    {
        var graph = new FakeGraph();
        FakeAssembly root = graph.Add("Root", "A", "B", "C");
        graph.Add("A");
        graph.Add("B");
        graph.Add("C");
        graph.IsCanceled = () => graph.Loads.Count >= 1;

        (int loaded, int failed) = graph.Preload(root);

        loaded.Should().Be(1);
        failed.Should().Be(0);
        graph.Loads.Should().Equal("Root->A");
    }

    public void Preload_RunCanceledBeforeTheWalk_LoadsNothing()
    {
        var graph = new FakeGraph();
        FakeAssembly root = graph.Add("Root", "A");
        graph.Add("A");
        graph.IsCanceled = () => true;

        (int loaded, int failed) = graph.Preload(root);

        loaded.Should().Be(0);
        failed.Should().Be(0);
        graph.Loads.Should().BeEmpty();
    }

    public void Preload_DoesNotWalkIntoDynamicAssemblies()
    {
        var graph = new FakeGraph();
        FakeAssembly root = graph.Add("Root", "Dynamic");
        graph.Add("Dynamic", isDynamic: true, "Hidden");
        graph.Add("Hidden");

        (int loaded, int failed) = graph.Preload(root);

        loaded.Should().Be(0);
        failed.Should().Be(0);
        graph.Loads.Should().Equal("Root->Dynamic");
    }

    public void Preload_ReferenceCycleBackToTheRootTerminates()
    {
        var graph = new FakeGraph();
        FakeAssembly root = graph.Add("Root", "A");
        graph.Add("A", "B");
        graph.Add("B", "A", "Root");

        (int loaded, int failed) = graph.Preload(root);

        loaded.Should().Be(2);
        failed.Should().Be(0);
        graph.Loads.Should().Equal("Root->A", "A->B", "B->A", "B->Root");
    }

    public void Preload_TestAssemblyWithRealReferences_LoadsItsNonFrameworkDependencies()
    {
        (int loaded, int _) = AssemblyPreloader.Preload(typeof(AssemblyPreloaderTests).Assembly);

        loaded.Should().BeGreaterThan(0);
    }

    private sealed class FakeAssembly(string name, bool isDynamic) : Assembly
    {
        public override string FullName => name;

        public override bool IsDynamic => isDynamic;
    }

    private sealed class FakeGraph
    {
        private readonly Dictionary<string, (FakeAssembly Assembly, string[] References)> _nodes = [with(StringComparer.Ordinal)];
        private readonly HashSet<string> _unloadable = [with(StringComparer.Ordinal)];

        public List<string> Loads { get; } = [];

        public Func<bool> IsCanceled { get; set; } = () => false;

        public FakeAssembly Add(string name, params string[] references)
            => Add(name, isDynamic: false, references);

        public FakeAssembly Add(string name, bool isDynamic, params string[] references)
        {
            var assembly = new FakeAssembly(name, isDynamic);
            _nodes[name] = (assembly, references);
            return assembly;
        }

        public void MarkUnloadable(string name) => _unloadable.Add(name);

        public (int Loaded, int Failed) Preload(FakeAssembly root, Func<Assembly, AssemblyName, Assembly?>? tryLoad = null)
            => AssemblyPreloader.Preload(root, GetReferences, tryLoad ?? TryLoad, () => IsCanceled());

        private AssemblyName[] GetReferences(Assembly assembly)
            => [.. _nodes[assembly.FullName!].References.Select(reference => new AssemblyName(reference))];

        public Assembly? TryLoad(Assembly referrer, AssemblyName reference)
        {
            Loads.Add($"{referrer.FullName}->{reference.Name}");
            return !_unloadable.Contains(reference.Name!) && _nodes.TryGetValue(reference.Name!, out (FakeAssembly Assembly, string[] References) node)
                ? node.Assembly
                : null;
        }
    }
}
