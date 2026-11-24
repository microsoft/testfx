// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TestDependencyGraph
{
    /// <summary>
    /// Finds every test that takes part in a dependency cycle, and describes each cycle as a path
    /// (<c>A &gt; B &gt; A</c>). Every member of a component with a cycle is reported, because each one is
    /// unschedulable - see <see cref="ForEachCyclicComponent"/> for why the components are computed with
    /// Tarjan's algorithm rather than by collecting depth-first back edges.
    /// </summary>
    private static string?[] FindCycles(int[][] prerequisites, UnitTestElement[] tests, List<string> errors)
    {
        string?[] cycleMessageByTest = new string?[prerequisites.Length];

        ForEachCyclicComponent(prerequisites, (component, root) =>
        {
            // A component is a *set*, not a path: rendering its members in any order would claim edges
            // that were never declared (with A depending on B and C, B on A and C on B, "A > B > C > A"
            // asserts a B -> C edge that does not exist, and tells the user to remove it). Walk real
            // edges inside the component instead, so every arrow in the message is one the user wrote.
            int[] cycleNodes = FindCyclePathWithin(prerequisites, component, root);
            var cycle = new List<string>(cycleNodes.Length + 1);
            foreach (int node in cycleNodes)
            {
                cycle.Add(tests[node].TestMethod.FullyQualifiedName);
            }

            cycle.Add(tests[cycleNodes[0]].TestMethod.FullyQualifiedName);

            string message = string.Format(CultureInfo.CurrentCulture, Resource.DependsOnCycle, string.Join(" > ", cycle));
            errors.Add(message);
            foreach (int node in component)
            {
                cycleMessageByTest[node] = message;
            }
        });

        return cycleMessageByTest;
    }

    /// <summary>
    /// Returns a genuine closed walk through <paramref name="component"/> starting at <paramref name="root"/>,
    /// following only declared prerequisite edges that stay inside the component. A breadth-first search finds
    /// the shortest such walk, which keeps the reported path as small as the graph allows. The component is
    /// strongly connected, so a walk back to the root always exists.
    /// </summary>
    private static int[] FindCyclePathWithin(int[][] prerequisites, List<int> component, int root)
    {
        var inComponent = new HashSet<int>(component);

        // Self-reference: the whole cycle is the one node.
        if (Array.IndexOf(prerequisites[root], root) >= 0)
        {
            return [root];
        }

        var parent = new Dictionary<int, int>();
        var queue = new Queue<int>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int prerequisite in prerequisites[current])
            {
                if (!inComponent.Contains(prerequisite))
                {
                    continue;
                }

                if (prerequisite == root)
                {
                    // Walk the parents back to the root, then reverse so the path reads root-first.
                    var path = new List<int> { current };
                    int node = current;
                    while (parent.TryGetValue(node, out int previous))
                    {
                        path.Add(previous);
                        node = previous;
                    }

                    path.Reverse();
                    return [.. path];
                }

                if (!parent.ContainsKey(prerequisite))
                {
                    parent[prerequisite] = current;
                    queue.Enqueue(prerequisite);
                }
            }
        }

        // Unreachable for a strongly connected component, but never render a path we cannot justify.
        return [root];
    }
}
