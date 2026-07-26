// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using MessageLevel = Microsoft.VisualStudio.TestTools.UnitTesting.MessageLevel;
using StringEx = Microsoft.VisualStudio.TestTools.UnitTesting.StringEx;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Reads a dependency chain file: an XML document that declares the same edges as <c>[DependsOn]</c>, but
/// outside the test source. It exists for the cases the attribute cannot serve - orchestrating tests that
/// somebody else owns, keeping the whole order visible in one reviewable place, or letting a role that does
/// not edit code define the run - and is the same idea as TestNG's <c>testng.xml</c> or Playwright's project
/// <c>dependencies</c>.
/// </summary>
/// <remarks>
/// <para>
/// The format is XML rather than JSON because the adapter already parses <c>.runsettings</c> with
/// <see cref="XmlReader"/> on every target framework it supports, including those with no JSON reader
/// available, and because MSTest's own legacy ordered-test files were XML.
/// </para>
/// <para>
/// A test is referenced by <c>Namespace.Class.Method</c>, or by <c>Namespace.Class.*</c> to mean every test
/// of a class. Edges from the file are merged with those declared by attributes; neither overrides the other.
/// </para>
/// <example>
/// <code language="xml">
/// &lt;TestDependencies&gt;
///   &lt;!-- A chain is the flat case: every entry waits for the one before it. --&gt;
///   &lt;Chain&gt;
///     &lt;Test name="Contoso.Tests.SetupTests.CreateDatabase" /&gt;
///     &lt;Test name="Contoso.Tests.ImportTests.ImportCatalog" /&gt;
///     &lt;Test name="Contoso.Tests.CheckoutTests.PlaceOrder" /&gt;
///   &lt;/Chain&gt;
///
///   &lt;!-- An explicit node is the tree case: fan-in, fan-out and per-edge options. --&gt;
///   &lt;Test name="Contoso.Tests.ReportTests.WriteAudit" proceedOnFailure="true"&gt;
///     &lt;DependsOn name="Contoso.Tests.CheckoutTests.PlaceOrder" /&gt;
///     &lt;DependsOn name="Contoso.Tests.ImportTests.*" /&gt;
///   &lt;/Test&gt;
/// &lt;/TestDependencies&gt;
/// </code>
/// </example>
/// </remarks>
internal sealed class TestDependencyChainFile
{
    private TestDependencyChainFile(IReadOnlyList<DeclaredEdge> edges) => Edges = edges;

    /// <summary>Gets the edges declared by the file, in declaration order.</summary>
    public IReadOnlyList<DeclaredEdge> Edges { get; }

    /// <summary>
    /// Parses the chain file at <paramref name="path"/>. Any problem - a missing file, malformed XML, an
    /// unusable reference - is reported through <paramref name="logger"/> and yields no edges, because a
    /// broken orchestration file must not silently reorder or skip a run.
    /// </summary>
    public static TestDependencyChainFile? TryLoad(string path, IAdapterMessageLogger? logger)
    {
        try
        {
            if (!File.Exists(path))
            {
                logger?.SendMessage(MessageLevel.Error, string.Format(CultureInfo.CurrentCulture, Resource.DependencyChainFileNotFound, path));
                return null;
            }

            var edges = new List<DeclaredEdge>();
            var settings = new XmlReaderSettings
            {
                // The file is plain data; resolving external entities would let it reach the file system or
                // the network, which a test-ordering document has no business doing.
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreWhitespace = true,
                IgnoreComments = true,
            };

            using (var reader = XmlReader.Create(path, settings))
            {
                reader.MoveToContent();
                if (reader.NodeType != XmlNodeType.Element || !string.Equals(reader.Name, "TestDependencies", StringComparison.OrdinalIgnoreCase))
                {
                    logger?.SendMessage(MessageLevel.Error, string.Format(CultureInfo.CurrentCulture, Resource.DependencyChainFileInvalidRoot, path, reader.Name));
                    return null;
                }

                if (!reader.IsEmptyElement)
                {
                    ReadRootChildren(reader, edges, path, logger);
                }
            }

            return new TestDependencyChainFile(edges);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            logger?.SendMessage(MessageLevel.Error, string.Format(CultureInfo.CurrentCulture, Resource.DependencyChainFileUnreadable, path, ex.Message));
            return null;
        }
    }

    private static void ReadRootChildren(XmlReader reader, List<DeclaredEdge> edges, string path, IAdapterMessageLogger? logger)
    {
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement)
            {
                return;
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (string.Equals(reader.Name, "Chain", StringComparison.OrdinalIgnoreCase))
            {
                ReadChain(reader, edges, path, logger);
            }
            else if (string.Equals(reader.Name, "Test", StringComparison.OrdinalIgnoreCase))
            {
                ReadTest(reader, edges, path, logger);
            }
            else
            {
                logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.DependencyChainFileUnknownElement, path, reader.Name));
                reader.Skip();
            }
        }
    }

    /// <summary>
    /// Reads a <c>&lt;Chain&gt;</c>: the flat case, where each entry simply waits for the previous one. It is
    /// expanded here into ordinary edges so that everything downstream sees a single kind of declaration.
    /// </summary>
    private static void ReadChain(XmlReader reader, List<DeclaredEdge> edges, string path, IAdapterMessageLogger? logger)
    {
        bool proceedOnFailure = ReadProceedOnFailure(reader, path, logger);
        if (reader.IsEmptyElement)
        {
            return;
        }

        TestReference? previous = null;
        using XmlReader chain = reader.ReadSubtree();
        chain.Read();
        while (chain.Read())
        {
            if (chain.NodeType != XmlNodeType.Element || !string.Equals(chain.Name, "Test", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryReadReference(chain, path, logger) is not { } current)
            {
                continue;
            }

            if (previous is { } prerequisite)
            {
                edges.Add(new DeclaredEdge(current, prerequisite, proceedOnFailure));
            }

            previous = current;
        }
    }

    /// <summary>
    /// Reads an explicit <c>&lt;Test&gt;</c> node with its <c>&lt;DependsOn&gt;</c> children: the tree case,
    /// where one test can name several prerequisites and several tests can name the same one.
    /// </summary>
    private static void ReadTest(XmlReader reader, List<DeclaredEdge> edges, string path, IAdapterMessageLogger? logger)
    {
        bool proceedOnFailure = ReadProceedOnFailure(reader, path, logger);
        TestReference? dependent = TryReadReference(reader, path, logger);
        if (reader.IsEmptyElement || dependent is not { } dependentReference)
        {
            if (!reader.IsEmptyElement)
            {
                reader.Skip();
            }

            return;
        }

        using XmlReader node = reader.ReadSubtree();
        node.Read();
        while (node.Read())
        {
            if (node.NodeType != XmlNodeType.Element || !string.Equals(node.Name, "DependsOn", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // An edge may relax the node's default, so an audit step can proceed past one prerequisite while
            // still being held back by another.
            bool edgeProceedOnFailure = proceedOnFailure || ReadProceedOnFailure(node, path, logger);
            if (TryReadReference(node, path, logger) is { } prerequisite)
            {
                edges.Add(new DeclaredEdge(dependentReference, prerequisite, edgeProceedOnFailure));
            }
        }
    }

    private static bool ReadProceedOnFailure(XmlReader reader, string path, IAdapterMessageLogger? logger)
    {
        string? value = reader.GetAttribute("proceedOnFailure");
        if (StringEx.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out bool parsed))
        {
            return parsed;
        }

        logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.DependencyChainFileInvalidBoolean, path, value));
        return false;
    }

    private static TestReference? TryReadReference(XmlReader reader, string path, IAdapterMessageLogger? logger)
    {
        string? name = reader.GetAttribute("name");
        if (StringEx.IsNullOrWhiteSpace(name))
        {
            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.DependencyChainFileMissingName, path, reader.Name));
            return null;
        }

        var reference = TestReference.TryParse(name!);
        if (reference is null)
        {
            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.DependencyChainFileInvalidReference, path, name));
        }

        return reference;
    }

    /// <summary>
    /// A reference to a test, or to every test of a class, written as <c>Namespace.Class.Method</c> or
    /// <c>Namespace.Class.*</c>.
    /// </summary>
    internal sealed class TestReference
    {
        private TestReference(string className, string? methodName)
        {
            ClassName = className;
            MethodName = methodName;
        }

        public string ClassName { get; }

        /// <summary>Gets the method name, or <see langword="null"/> when the reference covers a whole class.</summary>
        public string? MethodName { get; }

        public static TestReference? TryParse(string value)
        {
            string trimmed = value.Trim();
            int lastDot = trimmed.LastIndexOf('.');

            // Both parts are required: a bare identifier could be a class or a method, and guessing would
            // silently point the edge at the wrong thing.
            if (lastDot <= 0 || lastDot == trimmed.Length - 1)
            {
                return null;
            }

            string className = trimmed.Substring(0, lastDot);
            string methodName = trimmed.Substring(lastDot + 1);
            return methodName == "*" ? new TestReference(className, null) : new TestReference(className, methodName);
        }

        public bool Matches(UnitTestElement element)
            => string.Equals(element.TestMethod.FullClassName, ClassName, StringComparison.Ordinal)
                && (MethodName is null || string.Equals(element.TestMethod.Name, MethodName, StringComparison.Ordinal));
    }

    /// <summary>One declared edge: <see cref="Dependent"/> runs after <see cref="Prerequisite"/>.</summary>
    internal sealed class DeclaredEdge
    {
        public DeclaredEdge(TestReference dependent, TestReference prerequisite, bool proceedOnFailure)
        {
            Dependent = dependent;
            Prerequisite = prerequisite;
            ProceedOnFailure = proceedOnFailure;
        }

        public TestReference Dependent { get; }

        public TestReference Prerequisite { get; }

        public bool ProceedOnFailure { get; }
    }

    /// <summary>
    /// Adds the file's edges to the matching elements, so that from here on an edge from the file is
    /// indistinguishable from one declared by an attribute.
    /// </summary>
    /// <returns><see langword="true"/> when at least one edge was applied.</returns>
    public bool ApplyTo(UnitTestElement[] tests, IAdapterMessageLogger? logger)
    {
        bool applied = false;
        foreach (DeclaredEdge edge in Edges)
        {
            bool matchedDependent = false;
            foreach (UnitTestElement test in tests)
            {
                if (!edge.Dependent.Matches(test))
                {
                    continue;
                }

                matchedDependent = true;

                // The prerequisite is stored as declared rather than resolved to concrete tests here: the
                // graph already knows how to expand a class-wide target and how to report one that matches
                // nothing, and doing it in one place keeps both sources of edges behaving identically.
                var dependency = new TestDependencyInfo(edge.Prerequisite.ClassName, edge.Prerequisite.MethodName, edge.ProceedOnFailure);
                test.Dependencies = test.Dependencies is { Length: > 0 } existing
                    ? [.. existing, dependency]
                    : [dependency];
                applied = true;
            }

            if (!matchedDependent)
            {
                logger?.SendMessage(
                    MessageLevel.Warning,
                    string.Format(CultureInfo.CurrentCulture, Resource.DependencyChainFileDependentNotFound, DescribeReference(edge.Dependent)));
            }
        }

        return applied;

        static string DescribeReference(TestReference reference) => $"{reference.ClassName}.{reference.MethodName ?? "*"}";
    }
}
