// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using Microsoft.Testing.Internal.Framework;

namespace Microsoft.Testing.TestInfrastructure;

public static class TestsRunWatchDog
{
    private static readonly ConcurrentDictionary<TestNodeUid, int> TestNodes = new();

    public static string? BaselineFile { get; set; }

    public static void AddTestRun(TestNodeUid testNodeUid) => TestNodes.AddOrUpdate(testNodeUid, 1, (_, count) => count + 1);

    public static async Task VerifyAsync(bool skip = false, bool fixBaseLine = false)
    {
        if (skip)
        {
            return;
        }

        if (BaselineFile is null)
        {
            throw new InvalidOperationException("Baseline file should not be null");
        }

        if (TestNodes.IsEmpty)
        {
            throw new InvalidOperationException("No tests were executed. Have you called 'TestsRunWatchDog.AddTestRun'?");
        }

        List<string> expectedTestsDidNotRun = new();
        List<string> unexpectedRanTests = new();
        using (FileStream fs = File.OpenRead(BaselineFile))
        {
            using StreamReader streamReader = new(fs);
            string? testFullyQualifiedName;
            while ((testFullyQualifiedName = await streamReader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(testFullyQualifiedName))
                {
                    // Skip empty lines.
                    continue;
                }
                else if (!TestNodes.TryGetValue(testFullyQualifiedName, out int _))
                {
                    expectedTestsDidNotRun.Add(testFullyQualifiedName);
                }
                else
                {
                    TestNodes[testFullyQualifiedName]--;
                    if (TestNodes[testFullyQualifiedName] == 0)
                    {
                        TestNodes.TryRemove(testFullyQualifiedName, out _);
                    }
                }
            }
        }

        if (!TestNodes.IsEmpty)
        {
            foreach (KeyValuePair<TestNodeUid, int> notRunNodes in TestNodes)
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
                tests.RemoveAll(t => expectedTestsDidNotRun.Contains(t));
                tests.AddRange(unexpectedRanTests);
                tests.Sort();
                File.WriteAllLines(BaselineFile, tests);
                Console.WriteLine();
                Console.WriteLine($"FIXED BASELINE: '{BaselineFile}'");
            }
        }
    }
}
