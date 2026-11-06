// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Selects stable, class-preserving partitions of the complete discovery for one source.
/// </summary>
internal sealed class TestPartition
{
    public TestPartition(int index, int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (index <= 0 || index > count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        Index = index;
        Count = count;
    }

    public int Index { get; }

    public int Count { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out TestPartition? partition)
    {
        partition = null;
        string[] parts = value.Split('/');
        if (parts.Length != 2
            || !TryParsePositiveInteger(parts[0], out int index)
            || !TryParsePositiveInteger(parts[1], out int count)
            || index > count)
        {
            return false;
        }

        partition = new TestPartition(index, count);
        return true;
    }

    public ICollection<UnitTestElement> SelectTests(ICollection<UnitTestElement> tests, TestDependencyDeclaration[]? configuredDependencies)
    {
        if (Count == 1 || tests.Count == 0)
        {
            return tests;
        }

        UnitTestElement[] groupingTests = [.. tests];
        string[] classNames = [.. groupingTests.Select(test => test.TestMethod.FullClassName).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal)];
        var classIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        int[] parents = new int[classNames.Length];
        for (int i = 0; i < classNames.Length; i++)
        {
            classIndices.Add(classNames[i], i);
            parents[i] = i;
        }

        if (configuredDependencies is { Length: > 0 }
            || groupingTests.Any(test => test.Dependencies is { Length: > 0 }))
        {
            var methodsByClass = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (string className in classNames)
            {
                methodsByClass.Add(className, []);
            }

            foreach (UnitTestElement test in groupingTests)
            {
                methodsByClass[test.TestMethod.FullClassName].Add(test.TestMethod.Name);
            }

            foreach (UnitTestElement test in groupingTests)
            {
                if (test.Dependencies is not { Length: > 0 } dependencies)
                {
                    continue;
                }

                int dependent = classIndices[test.TestMethod.FullClassName];
                foreach (TestDependencyInfo dependency in dependencies)
                {
                    string targetClass = dependency.TargetClassFullName ?? test.TestMethod.FullClassName;
                    if (!classIndices.TryGetValue(targetClass, out int prerequisite)
                        || (dependency.TargetMethodName is { } method && !methodsByClass[targetClass].Contains(method)))
                    {
                        // The ordinary executor owns diagnostics for unresolved dependencies.
                        continue;
                    }

                    parents[Find(parents, prerequisite)] = Find(parents, dependent);
                }
            }

            if (configuredDependencies is not null)
            {
                foreach (TestDependencyDeclaration declaration in configuredDependencies)
                {
                    if (declaration.TryResolveClassDependency(methodsByClass, out string? dependentClass, out string? prerequisiteClass))
                    {
                        parents[Find(parents, classIndices[prerequisiteClass])] = Find(parents, classIndices[dependentClass]);
                    }
                }
            }
        }

        var components = new Dictionary<int, List<string>>();
        for (int i = 0; i < classNames.Length; i++)
        {
            int root = Find(parents, i);
            if (!components.TryGetValue(root, out List<string>? component))
            {
                components.Add(root, component = []);
            }

            component.Add(classNames[i]);
        }

        string moduleName = Path.GetFileNameWithoutExtension(groupingTests[0].TestMethod.AssemblyName);
        var selectedClasses = new HashSet<string>(StringComparer.Ordinal);
        foreach (List<string> component in components.Values)
        {
            if (GetPartitionIndex(moduleName, component) == Index)
            {
                selectedClasses.UnionWith(component);
            }
        }

        return [.. tests.Where(test => selectedClasses.Contains(test.TestMethod.FullClassName))];
    }

    private static bool TryParsePositiveInteger(string value, out int result)
    {
        foreach (char character in value)
        {
            if (character is < '0' or > '9')
            {
                result = 0;
                return false;
            }
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result > 0;
    }

    private static int Find(int[] parents, int index)
    {
        while (parents[index] != index)
        {
            parents[index] = parents[parents[index]];
            index = parents[index];
        }

        return index;
    }

    private int GetPartitionIndex(string moduleName, List<string> classNames)
    {
        using var stream = new MemoryStream();
        WriteField(stream, "mtp.partition.v1");
        WriteField(stream, moduleName);
        foreach (string className in classNames)
        {
            WriteField(stream, className);
        }

#if NET8_0_OR_GREATER
        byte[] hash = SHA256.HashData(stream.GetBuffer().AsSpan(0, (int)stream.Length));
#else
        using var algorithm = SHA256.Create();
        byte[] hash = algorithm.ComputeHash(stream.ToArray());
#endif
        ulong prefix = 0;
        for (int i = 0; i < sizeof(ulong); i++)
        {
            prefix = (prefix << 8) | hash[i];
        }

        return (int)(prefix % (uint)Count) + 1;
    }

    private static void WriteField(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        stream.WriteByte((byte)(bytes.Length >> 24));
        stream.WriteByte((byte)((bytes.Length >> 16) & byte.MaxValue));
        stream.WriteByte((byte)((bytes.Length >> 8) & byte.MaxValue));
        stream.WriteByte((byte)(bytes.Length & byte.MaxValue));
        stream.Write(bytes, 0, bytes.Length);
    }
}
