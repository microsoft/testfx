// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using Microsoft.Testing.Framework;

namespace Microsoft.Testing.TestInfrastructure;

public static class TestsRunWatchDog
{
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable SA1311 // Static readonly fields should begin with upper-case letter
    private static readonly ConcurrentDictionary<TestNodeUid, int> s_testNodes = new();
#pragma warning restore SA1311 // Static readonly fields should begin with upper-case letter
#pragma warning restore IDE1006 // Naming Styles

    public static string? BaselineFile { get; set; }

    public static void AddTestRun(TestNodeUid testNodeUid)
        => s_testNodes.AddOrUpdate(testNodeUid, 1, (_, count) => count + 1);

    public static async Task Verify(bool skip = false, bool fixBaseLine = false)
    {
        if (skip)
        {
            return;
        }

        if (BaselineFile is null)
        {
            throw new InvalidOperationException("Baseline file should not be null");
        }

        if (s_testNodes.IsEmpty)
        {
            throw new InvalidOperationException("No tests were executed. Have you called 'TestsRunWatchDog.AddTestRun'?");
        }

        var expectedTestsDidNotRun = new List<string>();
        var unexpectedRanTests = new List<string>();
        using (FileStream fs = File.OpenRead(BaselineFile))
        {
            using var streamReader = new StreamReader(fs);
            string? testFullyQualifiedName;
            while ((testFullyQualifiedName = await streamReader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(testFullyQualifiedName))
                {
                    // Skip empty lines.
                    continue;
                }
                else if (!s_testNodes.TryGetValue(testFullyQualifiedName, out int _))
                {
                    expectedTestsDidNotRun.Add(testFullyQualifiedName);
                }
                else
                {
                    s_testNodes[testFullyQualifiedName]--;
                    if (s_testNodes[testFullyQualifiedName] == 0)
                    {
                        s_testNodes.TryRemove(testFullyQualifiedName, out _);
                    }
                }
            }
        }

        if (!s_testNodes.IsEmpty)
        {
            foreach (KeyValuePair<TestNodeUid, int> notRunNodes in s_testNodes)
            {
                for (int i = 0; i < notRunNodes.Value; i++)
                {
                    unexpectedRanTests.Add(notRunNodes.Key.Value);
                }
            }
        }

        StringBuilder sb = new();
        if (unexpectedRanTests.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"Unexpected tests that ran (base line file name {BaselineFile}):");
            sb.AppendLine();
            foreach (string unexpectedTest in unexpectedRanTests)
            {
                sb.AppendLine(unexpectedTest);
            }
        }

        if (expectedTestsDidNotRun.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"Expected tests that did not run (base line file name {BaselineFile}):");
            sb.AppendLine();
            foreach (string missingTest in expectedTestsDidNotRun)
            {
                sb.AppendLine(missingTest);
            }
        }

        try
        {
            if (unexpectedRanTests.Count > 0 || expectedTestsDidNotRun.Count > 0)
            {
                throw new InvalidOperationException(sb.ToString());
            }
        }
        finally
        {
            if (fixBaseLine)
            {
                List<string> tests = new(File.ReadAllLines(BaselineFile));
                tests.RemoveAll(expectedTestsDidNotRun.Contains);
                tests.AddRange(unexpectedRanTests ?? []);
                tests.Sort();
                File.WriteAllLines(BaselineFile, tests);
                Console.WriteLine();
                Console.WriteLine($"FIXED BASELINE: '{BaselineFile}'");
            }
        }
    }
}
