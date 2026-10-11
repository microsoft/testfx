using System.Diagnostics;
using System.Text;

namespace UnskipClosedTests.Tool.Tests;

internal static class Program
{
    private static readonly string[] RestrictedEnvironmentVariables =
    [
        "ACTIONS_ID_TOKEN_REQUEST_TOKEN",
        "ACTIONS_RUNTIME_TOKEN",
        "GH_ENTERPRISE_TOKEN",
        "GH_AW_AGENT_OUTPUT",
        "GH_TOKEN",
        "GITHUB_ENTERPRISE_TOKEN",
        "GITHUB_ENV",
        "GITHUB_OUTPUT",
        "GITHUB_TOKEN",
        "ORIGINAL_MANIFEST",
        "RESULT_PATH",
    ];

    public static int Main()
    {
        List<(string Name, Action Test)> tests =
        [
            ("inventories method and class ignores from Git", InventoriesMethodAndClassIgnores),
            ("rejects malformed issue reference suffixes", RejectsMalformedIssueReferenceSuffixes),
            ("removes GitHub credentials from verification", RemovesGitHubCredentialsFromVerification),
            ("records exact final content hashes", RecordsExactFinalContentHashes),
            ("keeps duplicate detection marker in titles", KeepsDuplicateDetectionMarkerInTitles),
            ("verifies every target-specific TRX", VerifiesEveryTargetSpecificTrx),
        ];

        foreach ((string name, Action test) in tests)
        {
            test();
            Console.WriteLine($"PASS: {name}");
        }

        Console.WriteLine($"All {tests.Count} unskip closed tests tool tests passed.");
        return 0;
    }

    private static void InventoriesMethodAndClassIgnores()
    {
        string root = Path.Combine(Path.GetTempPath(), $"unskip-inventory-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Sample.cs"), """
                namespace Example;

                [Ignore("#123")]
                public class SkippedClass
                {
                    [TestMethod]
                    public void TestOne() { }
                }

                public class SkippedMethod
                {
                    [TestMethod]
                    [Ignore("https://github.com/microsoft/testfx/issues/456")]
                    public void TestTwo() { }
                }
                """, new UTF8Encoding(true));
            RunGit(root, "init", "-q");
            RunGit(root, "add", "Sample.cs");
            RunGit(root, "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "-qm", "Initial");

            ToolConfig config = new()
            {
                SourceRoots = ["Sample.cs"],
                IgnoreAttributeNames = ["Ignore"],
                TestAttributeNames = ["TestMethod"],
            };
            Manifest manifest = InventoryEngine.Create(root, "microsoft/testfx", config);
            AssertEqual(2, manifest.CandidateCount, "Not all ignored owners were inventoried.");
            Candidate classCandidate = manifest.Candidates.Single(static candidate => candidate.Owner.Kind == "class");
            AssertEqual("Example.SkippedClass", classCandidate.Owner.TypeFqn, "Class owner was misidentified.");
            AssertEqual("Example.SkippedClass.TestOne", classCandidate.Owner.TestFqns.Single(), "Class test was not enumerated.");
            AssertEqual("microsoft/testfx#123", classCandidate.CanonicalIssueReferences.Single().Canonical, "Class issue was not parsed.");

            Candidate methodCandidate = manifest.Candidates.Single(static candidate => candidate.Owner.Kind == "method");
            AssertEqual("M:Example.SkippedMethod.TestTwo()", methodCandidate.Owner.DeclarationId, "Method owner was misidentified.");
            AssertEqual("Example.SkippedMethod.TestTwo", methodCandidate.Owner.TestFqns.Single(), "Method test was not enumerated.");
            AssertEqual("microsoft/testfx#456", methodCandidate.CanonicalIssueReferences.Single().Canonical, "Method issue was not parsed.");
            AssertEqual(manifest.ManifestDigest, InventoryEngine.Create(root, "microsoft/testfx", config).ManifestDigest,
                "Inventory changed without a source change.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void RunGit(string root, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = root,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        AssertEqual(0, process.ExitCode, $"git {string.Join(' ', arguments)} failed: {error}");
    }

    private static void RejectsMalformedIssueReferenceSuffixes()
    {
        string repository = "microsoft/testfx";
        string[] malformed =
        [
            "https://github.com/microsoft/testfx/issues/123abc",
            "microsoft/testfx#456suffix",
            "#789_identifier",
        ];
        foreach (string value in malformed)
        {
            AssertEqual(0, InventoryEngine.ParseReferences(value, repository).Count(), $"Reference '{value}' was accepted.");
        }

        List<IssueReference> valid = InventoryEngine.ParseReferences(
            "https://github.com/microsoft/testfx/issues/123, microsoft/testfx#456; #789.",
            repository).ToList();
        AssertEqual(3, valid.Count, "Valid issue references were not all accepted.");
        AssertEqual("microsoft/testfx#123", valid[0].Canonical, "Full issue URL was not canonicalized.");
        AssertEqual("microsoft/testfx#456", valid[1].Canonical, "Qualified issue reference was not canonicalized.");
        AssertEqual("microsoft/testfx#789", valid[2].Canonical, "Bare issue reference was not canonicalized.");
    }

    private static void RemovesGitHubCredentialsFromVerification()
    {
        Dictionary<string, string?> originalValues = RestrictedEnvironmentVariables
            .ToDictionary(static variable => variable, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
        const string sentinel = "UNSKIP_VERIFICATION_ENVIRONMENT_SENTINEL";
        string? originalSentinel = Environment.GetEnvironmentVariable(sentinel);

        try
        {
            foreach (string variable in RestrictedEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(variable, "secret");
            }

            Environment.SetEnvironmentVariable(sentinel, "retained");
            ProcessStartInfo startInfo = ApplyEngine.CreateVerificationStartInfo(
                "dotnet",
                Environment.CurrentDirectory,
                ["--info"]);

            foreach (string variable in RestrictedEnvironmentVariables)
            {
                AssertFalse(startInfo.Environment.ContainsKey(variable), $"{variable} remained in the child environment.");
            }

            AssertEqual("retained", startInfo.Environment[sentinel], "Non-sensitive environment was unexpectedly removed.");
            AssertEqual("--info", startInfo.ArgumentList.Single(), "Verification argument was not preserved.");
        }
        finally
        {
            foreach ((string variable, string? value) in originalValues)
            {
                Environment.SetEnvironmentVariable(variable, value);
            }

            Environment.SetEnvironmentVariable(sentinel, originalSentinel);
        }
    }

    private static void RecordsExactFinalContentHashes()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"unskip-tool-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            byte[] firstContent = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("first")];
            byte[] secondContent = Encoding.UTF8.GetBytes("second");
            File.WriteAllBytes(Path.Combine(temporaryRoot, "First.cs"), firstContent);
            File.WriteAllBytes(Path.Combine(temporaryRoot, "Second.cs"), secondContent);

            List<ChangedFileResult> files = ApplyEngine.CreateChangedFiles(
                temporaryRoot,
                ["Second.cs", "First.cs", "Second.cs"]);

            AssertEqual(2, files.Count, "Changed files were not deduplicated.");
            AssertEqual("First.cs", files[0].Path, "Changed files were not sorted.");
            AssertEqual(JsonSupport.Sha256(firstContent), files[0].ContentSha256, "BOM-preserving hash was incorrect.");
            AssertEqual("Second.cs", files[1].Path, "Second changed file was missing.");
            AssertEqual(JsonSupport.Sha256(secondContent), files[1].ContentSha256, "Content hash was incorrect.");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static void KeepsDuplicateDetectionMarkerInTitles()
    {
        AssertTrue(
            ApplyEngine.CreatePrTitle(1).StartsWith("[unskip-closed-tests] ", StringComparison.Ordinal),
            "Single-candidate title is missing the duplicate-detection marker.");
        AssertTrue(
            ApplyEngine.CreatePrTitle(2).StartsWith("[unskip-closed-tests] ", StringComparison.Ordinal),
            "Multi-candidate title is missing the duplicate-detection marker.");
    }

    private static void VerifiesEveryTargetSpecificTrx()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"unskip-trx-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            string requestedResult = Path.Combine(temporaryRoot, "0000.trx");
            string firstResult = Path.Combine(temporaryRoot, "0000--net8.0.trx");
            string secondResult = Path.Combine(temporaryRoot, "0000--net9.0.trx");
            WriteTrx(firstResult, "first", "Passed");
            WriteTrx(secondResult, "second", "Passed");

            VerificationTest test = new()
            {
                Fqn = "Example.Tests.TestOne",
                ResultFile = requestedResult,
                SourcePath = "Tests.cs",
            };
            (bool success, string reason) = TrxVerifier.Verify([test]);
            AssertTrue(success, $"Multi-target TRX verification failed: {reason}");

            WriteTrx(secondResult, "second", "Failed");
            (success, reason) = TrxVerifier.Verify([test]);
            AssertFalse(success, "A failing target-specific TRX was accepted.");
            AssertTrue(
                reason.StartsWith("non_passing_outcome:", StringComparison.Ordinal),
                $"Unexpected failure reason: {reason}");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static void WriteTrx(string path, string id, string outcome) =>
        File.WriteAllText(
            path,
            $"""
            <TestRun>
              <TestDefinitions>
                <UnitTest id="{id}">
                  <TestMethod className="Example.Tests" name="TestOne" />
                </UnitTest>
              </TestDefinitions>
              <Results>
                <UnitTestResult testId="{id}" outcome="{outcome}" />
              </Results>
            </TestRun>
            """,
            new UTF8Encoding(false));

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected '{expected}', actual '{actual}'.");
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
