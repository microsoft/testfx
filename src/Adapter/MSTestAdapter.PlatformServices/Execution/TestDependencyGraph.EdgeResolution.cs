// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TestDependencyGraph
{
    /// <summary>
    /// Resolves every declared dependency into edges between test indices. A dependency that names a class
    /// but no method matches every test of that class; one that names no class is looked up in the
    /// dependent's own class. A dependency that matches no test in this run is reported as a warning and
    /// dropped, so that running a subset of the suite (for example with a filter, or a single test from an
    /// IDE) stays possible.
    /// </summary>
    private static (int[][] Prerequisites, bool[] ProceedOnFailure) ResolveEdges(UnitTestElement[] tests, List<string> warnings)
    {
        var byClass = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var byClassAndMethod = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int i = 0; i < tests.Length; i++)
        {
            TestMethod testMethod = tests[i].TestMethod;
            AddToIndex(byClass, testMethod.FullClassName, i);
            AddToIndex(byClassAndMethod, MakeKey(testMethod.FullClassName, testMethod.Name), i);
        }

        int[][] prerequisites = new int[tests.Length][];
        bool[] proceedOnFailure = new bool[tests.Length];
        var resolved = new List<int>();
        var seen = new HashSet<int>();

        for (int i = 0; i < tests.Length; i++)
        {
            if (tests[i].Dependencies is not { Length: > 0 } dependencies)
            {
                prerequisites[i] = [];
                continue;
            }

            resolved.Clear();
            seen.Clear();

            // A dependent proceeds past a failed prerequisite only when every edge it declares says so:
            // one edge that wants the ordinary skip is enough to hold it back.
            bool allProceed = true;

            foreach (TestDependencyInfo dependency in dependencies)
            {
                string targetClass = dependency.TargetClassFullName ?? tests[i].TestMethod.FullClassName;
                List<int>? matches;
                if (dependency.TargetMethodName is null)
                {
                    byClass.TryGetValue(targetClass, out matches);
                }
                else
                {
                    byClassAndMethod.TryGetValue(MakeKey(targetClass, dependency.TargetMethodName), out matches);
                }

                int added = 0;
                if (matches is not null)
                {
                    foreach (int match in matches)
                    {
                        // A whole-class dependency naturally includes the dependent itself when it lives in
                        // that class; that is not a cycle the user wrote, so drop it. An explicit self
                        // reference by name is kept, so it surfaces as a cycle.
                        if (match == i && dependency.TargetMethodName is null)
                        {
                            continue;
                        }

                        added++;
                        if (seen.Add(match))
                        {
                            resolved.Add(match);
                        }
                    }
                }

                if (added == 0)
                {
                    warnings.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.DependsOnTargetNotFound,
                        tests[i].TestMethod.FullyQualifiedName,
                        dependency.DescribeTarget()));
                    continue;
                }

                allProceed &= dependency.ProceedOnFailure;
            }

            prerequisites[i] = [.. resolved];
            proceedOnFailure[i] = resolved.Count > 0 && allProceed;
        }

        return (prerequisites, proceedOnFailure);

        static void AddToIndex(Dictionary<string, List<int>> index, string key, int value)
        {
            if (!index.TryGetValue(key, out List<int>? list))
            {
                index[key] = list = [];
            }

            list.Add(value);
        }

        static string MakeKey(string className, string methodName) => className + "." + methodName;
    }
}
