// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TestDependencyGraph
{
    /// <summary>
    /// Invokes <paramref name="onCyclicComponent"/> once for every strongly connected component of
    /// <paramref name="graph"/> that contains a cycle, passing the component's members and the node that
    /// roots it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cycle membership is computed with Tarjan's strongly-connected-components algorithm rather than by
    /// collecting back-edge paths from a plain depth-first search: a DFS reports the path it happened to
    /// close, so with overlapping cycles (A depends on B and C, B on A, C on B) it finds <c>A &gt; B &gt; A</c>,
    /// marks B finished, and never notices that C is in a cycle too. Every member of a component with a cycle
    /// has to be found, because each one is unschedulable.
    /// </para>
    /// <para>
    /// The traversal is iterative because the recursive form would stack-overflow on a long dependency chain.
    /// </para>
    /// </remarks>
    /// <param name="graph">For each node, the nodes it has an edge to.</param>
    /// <param name="onCyclicComponent">
    /// Called with the members of a component that contains a cycle, and the component's root. A component of
    /// one is only a cycle when the node names itself; anything larger always is.
    /// </param>
    private static void ForEachCyclicComponent(int[][] graph, Action<List<int>, int> onCyclicComponent)
    {
        int[] index = new int[graph.Length];
        int[] lowLink = new int[graph.Length];
        bool[] onStack = new bool[graph.Length];
        int[] edgeCursor = new int[graph.Length];
        for (int i = 0; i < index.Length; i++)
        {
            index[i] = -1;
        }

        var stack = new List<int>();
        var callStack = new List<int>();
        int nextIndex = 0;

        for (int start = 0; start < graph.Length; start++)
        {
            if (index[start] != -1)
            {
                continue;
            }

            callStack.Add(start);
            index[start] = lowLink[start] = nextIndex++;
            edgeCursor[start] = 0;
            stack.Add(start);
            onStack[start] = true;

            while (callStack.Count > 0)
            {
                int current = callStack[callStack.Count - 1];
                if (edgeCursor[current] < graph[current].Length)
                {
                    int next = graph[current][edgeCursor[current]++];
                    if (index[next] == -1)
                    {
                        index[next] = lowLink[next] = nextIndex++;
                        edgeCursor[next] = 0;
                        stack.Add(next);
                        onStack[next] = true;
                        callStack.Add(next);
                    }
                    else if (onStack[next])
                    {
                        lowLink[current] = Math.Min(lowLink[current], index[next]);
                    }

                    continue;
                }

                callStack.RemoveAt(callStack.Count - 1);
                if (callStack.Count > 0)
                {
                    int parent = callStack[callStack.Count - 1];
                    lowLink[parent] = Math.Min(lowLink[parent], lowLink[current]);
                }

                if (lowLink[current] != index[current])
                {
                    continue;
                }

                // 'current' roots a strongly connected component: pop it off the stack.
                var component = new List<int>();
                int member;
                do
                {
                    member = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    onStack[member] = false;
                    component.Add(member);
                }
                while (member != current);

                // A component of one is only a cycle when the node names itself; anything larger always is.
                if (component.Count > 1 || Array.IndexOf(graph[current], current) >= 0)
                {
                    onCyclicComponent(component, current);
                }
            }
        }
    }
}
