using System.Diagnostics;

namespace UnskipClosedTests.Tool;

internal static partial class ApplyEngine
{
    private static readonly string[] RestrictedVerificationEnvironmentVariables =
    [
        "ACTIONS_CACHE_URL",
        "ACTIONS_ID_TOKEN_REQUEST_TOKEN",
        "ACTIONS_ID_TOKEN_REQUEST_URL",
        "ACTIONS_RESULTS_URL",
        "ACTIONS_RUNTIME_TOKEN",
        "GH_ENTERPRISE_TOKEN",
        "GH_AW_AGENT_OUTPUT",
        "GH_TOKEN",
        "GITHUB_ENTERPRISE_TOKEN",
        "GITHUB_ENV",
        "GITHUB_OUTPUT",
        "GITHUB_PATH",
        "GITHUB_STEP_SUMMARY",
        "GITHUB_TOKEN",
        "ORIGINAL_MANIFEST",
        "RESULT_PATH",
    ];

    private static async Task<VerificationOutcome> VerifyCandidateAsync(
        GitRepository repository,
        ToolConfig config,
        Manifest manifest,
        Candidate candidate,
        string workRoot)
    {
        string candidateDirectory = Path.Combine(workRoot, candidate.CandidateId);
        ResetDirectory(candidateDirectory);
        List<VerificationTest> tests = candidate.Owner.TestFqns
            .Order(StringComparer.Ordinal)
            .Select((fqn, index) => new VerificationTest
            {
                Fqn = fqn,
                SourcePath = candidate.Path,
                ResultFile = Path.GetFullPath(Path.Combine(candidateDirectory, $"{index:D4}.trx")),
            })
            .ToList();
        ApplyRequest request = new()
        {
            Candidate = new VerificationCandidateRequest
            {
                CandidateId = candidate.CandidateId,
            },
            Repository = manifest.Repository,
            SourceCommit = manifest.SourceCommit,
            Tests = tests,
        };
        string requestPath = Path.GetFullPath(Path.Combine(candidateDirectory, "request.json"));
        JsonSupport.Write(requestPath, request);

        List<string> argv = [.. config.VerificationCommand, requestPath];

        ProcessStartInfo startInfo = CreateVerificationStartInfo(
            argv[0],
            repository.Root,
            argv.Skip(1));

        try
        {
            using Process process = Process.Start(startInfo)
                ?? throw new InfrastructureException($"Could not start verification command '{argv[0]}'.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(config.VerificationTimeoutSeconds));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                await Task.WhenAll(stdout, stderr);
                return new VerificationOutcome(false, "verification_timeout");
            }

            string standardError = await stderr;
            await stdout;
            if (process.ExitCode != 0)
            {
                string detail = standardError.Trim();
                if (detail.Length > 160)
                {
                    detail = detail[..160];
                }

                return new VerificationOutcome(
                    false,
                    detail.Length == 0
                        ? $"verification_nonzero_exit:{process.ExitCode}"
                        : $"verification_nonzero_exit:{process.ExitCode}:{detail}");
            }
        }
        catch (InfrastructureException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            throw new InfrastructureException($"Could not invoke verification command '{argv[0]}'.", ex);
        }

        (bool success, string reason) = TrxVerifier.Verify(tests);
        return new VerificationOutcome(success, reason);
    }

    internal static ProcessStartInfo CreateVerificationStartInfo(
        string executable,
        string workingDirectory,
        IEnumerable<string> arguments)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (string variable in RestrictedVerificationEnvironmentVariables)
        {
            startInfo.Environment.Remove(variable);
        }

        return startInfo;
    }
}
