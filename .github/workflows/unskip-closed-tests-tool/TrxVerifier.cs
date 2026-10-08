using System.Xml;
using System.Xml.Linq;

namespace UnskipClosedTests.Tool;

internal static class TrxVerifier
{
    public static (bool Success, string Reason) Verify(IReadOnlyList<VerificationTest> tests)
    {
        Dictionary<string, VerificationTest> expectedFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (VerificationTest test in tests)
        {
            string requestedFile = Path.GetFullPath(test.ResultFile);
            string directory = Path.GetDirectoryName(requestedFile)!;
            string requestedName = Path.GetFileName(requestedFile);
            string targetPrefix = $"{Path.GetFileNameWithoutExtension(requestedFile)}--";
            List<string> resultFiles = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.trx", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFullPath)
                    .Where(path =>
                        string.Equals(Path.GetFileName(path), requestedName, StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(path).StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : [];
            if (resultFiles.Count == 0)
            {
                return (false, $"missing_trx:{requestedName}");
            }

            foreach (string resultFile in resultFiles)
            {
                if (!expectedFiles.TryAdd(resultFile, test))
                {
                    return (false, $"duplicate_trx_path:{Path.GetFileName(resultFile)}");
                }
            }
        }

        foreach (string directory in tests
                     .Select(static test => Path.GetDirectoryName(Path.GetFullPath(test.ResultFile))!)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(directory))
            {
                string? unexpected = Directory.EnumerateFiles(directory, "*.trx", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFullPath)
                    .FirstOrDefault(path => !expectedFiles.ContainsKey(path));
                if (unexpected is not null)
                {
                    return (false, $"unexpected_trx:{Path.GetFileName(unexpected)}");
                }
            }
        }

        foreach ((string resultFile, VerificationTest test) in expectedFiles)
        {
            try
            {
                XDocument document = XDocument.Load(resultFile, LoadOptions.None);
                Dictionary<string, string> mappings = new(StringComparer.Ordinal);
                foreach (XElement unitTest in document.Descendants().Where(static element =>
                             element.Name.LocalName == "UnitTest"))
                {
                    string? id = unitTest.Attribute("id")?.Value;
                    XElement? method = unitTest.Descendants().FirstOrDefault(static element =>
                        element.Name.LocalName == "TestMethod");
                    string? className = method?.Attribute("className")?.Value;
                    string? methodName = method?.Attribute("name")?.Value;
                    if (string.IsNullOrWhiteSpace(id) ||
                        string.IsNullOrWhiteSpace(className) ||
                        string.IsNullOrWhiteSpace(methodName))
                    {
                        return (false, "malformed_trx_test_definition");
                    }

                    string fqn = $"{className}.{methodName}";
                    if (!string.Equals(fqn, test.Fqn, StringComparison.Ordinal))
                    {
                        return (false, $"mismatched_fqn:{fqn}");
                    }

                    if (!mappings.TryAdd(id, fqn))
                    {
                        return (false, $"duplicate_trx_test_id:{id}");
                    }
                }

                if (mappings.Count == 0)
                {
                    return (false, "zero_selected_tests");
                }

                List<XElement> results = document.Descendants().Where(static element =>
                    element.Name.LocalName == "UnitTestResult").ToList();
                if (results.Count == 0)
                {
                    return (false, "zero_executed_tests");
                }

                int passed = 0;
                foreach (XElement result in results)
                {
                    string? testId = result.Attribute("testId")?.Value;
                    string? outcome = result.Attribute("outcome")?.Value;
                    if (string.IsNullOrWhiteSpace(testId) ||
                        !mappings.TryGetValue(testId, out string? mappedFqn))
                    {
                        return (false, "result_without_exact_test_mapping");
                    }

                    if (!string.Equals(mappedFqn, test.Fqn, StringComparison.Ordinal))
                    {
                        return (false, $"mismatched_result_fqn:{mappedFqn}");
                    }

                    if (string.Equals(outcome, "Passed", StringComparison.OrdinalIgnoreCase))
                    {
                        passed++;
                    }
                    else
                    {
                        return (false, $"non_passing_outcome:{outcome ?? "missing"}");
                    }
                }

                if (passed == 0)
                {
                    return (false, "no_executed_pass");
                }
            }
            catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
            {
                return (false, $"malformed_trx:{ex.GetType().Name}");
            }
        }

        return (true, "passed");
    }
}
