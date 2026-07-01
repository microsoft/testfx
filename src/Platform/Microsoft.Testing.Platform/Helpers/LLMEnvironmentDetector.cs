// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

// Adapted from https://github.com/dotnet/sdk/tree/bcafbe92a30b1866bd17789759c9941761bf6b49/src/Cli/dotnet/Telemetry/LLMEnvironmentDetectorForTelemetry.cs
// Diverged from the upstream telemetry-only version so detection results can drive
// user-facing platform defaults (ANSI mode, banner, --show-stdout/--show-stderr).
// IMPORTANT: keep the environment-variable list below in sync with
// test/Utilities/Microsoft.Testing.TestInfrastructure/WellKnownEnvironmentVariables.cs
// (LLMEnvironmentVariables) so child processes spawned by acceptance tests can be
// deterministically isolated from an ambient agent shell.
internal sealed class LLMEnvironmentDetector
{
    private static readonly EnvironmentDetectionRuleWithResult<string>[] DetectionRules =
    [
        // Cowork (Claude Code cowork mode) - placed before Claude so the more specific variable is listed first
        new EnvironmentDetectionRuleWithResult<string>("cowork", new AnyPresentEnvironmentRule("CLAUDE_CODE_IS_COWORK")),
        // Claude Code
        new EnvironmentDetectionRuleWithResult<string>("claude", new AnyPresentEnvironmentRule("CLAUDECODE", "CLAUDE_CODE", "CLAUDE_CODE_ENTRYPOINT")),
        // Cursor AI
        new EnvironmentDetectionRuleWithResult<string>("cursor", new AnyPresentEnvironmentRule("CURSOR_EDITOR", "CURSOR_AI", "CURSOR_TRACE_ID", "CURSOR_AGENT")),
        // Gemini
        new EnvironmentDetectionRuleWithResult<string>("gemini", new AnyPresentEnvironmentRule("GEMINI_CLI")),
        // GitHub Copilot CLI (legacy gh extension: GITHUB_COPILOT_CLI_MODE; new Copilot CLI: GH_COPILOT_WORKING_DIRECTORY, COPILOT_CLI, COPILOT_MODEL, COPILOT_ALLOW_ALL, or COPILOT_GITHUB_TOKEN is set).
        new EnvironmentDetectionRuleWithResult<string>("copilot-cli", new AnyPresentEnvironmentRule(
            "GITHUB_COPILOT_CLI_MODE", "GH_COPILOT_WORKING_DIRECTORY", "COPILOT_CLI", "COPILOT_MODEL", "COPILOT_ALLOW_ALL", "COPILOT_GITHUB_TOKEN")),
        // GitHub Copilot agent mode in VS Code, which sets AI_AGENT=github_copilot_vscode_agent and COPILOT_AGENT=1 on the terminals it runs commands in.
        new EnvironmentDetectionRuleWithResult<string>("copilot-vscode", new AnyMatchEnvironmentRule(
            new EnvironmentVariableValueRule("AI_AGENT", "github_copilot_vscode_agent"),
            new AnyPresentEnvironmentRule("COPILOT_AGENT"))),
        // Codex CLI
        new EnvironmentDetectionRuleWithResult<string>("codex", new AnyPresentEnvironmentRule("CODEX_CLI", "CODEX_SANDBOX", "CODEX_CI", "CODEX_THREAD_ID")),
        // Aider
        new EnvironmentDetectionRuleWithResult<string>("aider", new EnvironmentVariableValueRule("OR_APP_NAME", "Aider")),
        // Plandex
        new EnvironmentDetectionRuleWithResult<string>("plandex", new EnvironmentVariableValueRule("OR_APP_NAME", "plandex")),
        // Amp
        new EnvironmentDetectionRuleWithResult<string>("amp", new AnyPresentEnvironmentRule("AMP_HOME")),
        // Qwen Code
        new EnvironmentDetectionRuleWithResult<string>("qwen", new AnyPresentEnvironmentRule("QWEN_CODE")),
        // Droid
        new EnvironmentDetectionRuleWithResult<string>("droid", new AnyPresentEnvironmentRule("DROID_CLI")),
        // OpenCode
        new EnvironmentDetectionRuleWithResult<string>("opencode", new AnyPresentEnvironmentRule("OPENCODE_AI")),
        // Zed AI
        new EnvironmentDetectionRuleWithResult<string>("zed", new AnyPresentEnvironmentRule("ZED_ENVIRONMENT", "ZED_TERM")),
        // Kimi CLI
        new EnvironmentDetectionRuleWithResult<string>("kimi", new AnyPresentEnvironmentRule("KIMI_CLI")),
        // OpenHands
        new EnvironmentDetectionRuleWithResult<string>("openhands", new EnvironmentVariableValueRule("OR_APP_NAME", "OpenHands")),
        // Goose
        new EnvironmentDetectionRuleWithResult<string>("goose", new AnyPresentEnvironmentRule("GOOSE_TERMINAL", "GOOSE_PROVIDER")),
        // Cline
        new EnvironmentDetectionRuleWithResult<string>("cline", new AnyPresentEnvironmentRule("CLINE_TASK_ID")),
        // Roo Code
        new EnvironmentDetectionRuleWithResult<string>("roo", new AnyPresentEnvironmentRule("ROO_CODE_TASK_ID")),
        // Windsurf
        new EnvironmentDetectionRuleWithResult<string>("windsurf", new AnyPresentEnvironmentRule("WINDSURF_SESSION")),
        // Replit
        new EnvironmentDetectionRuleWithResult<string>("replit", new AnyPresentEnvironmentRule("REPL_ID")),
        // Augment
        new EnvironmentDetectionRuleWithResult<string>("augment", new AnyPresentEnvironmentRule("AUGMENT_AGENT")),
        // Antigravity
        new EnvironmentDetectionRuleWithResult<string>("antigravity", new AnyPresentEnvironmentRule("ANTIGRAVITY_AGENT")),
        // (proposed) generic flag for Agentic usage
        new EnvironmentDetectionRuleWithResult<string>("generic_agent", new AnyPresentEnvironmentRule("AGENT_CLI")),
    ];

    private readonly IEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="LLMEnvironmentDetector"/> class.
    /// </summary>
    /// <param name="environment">The environment abstraction to use for reading environment variables.</param>
    public LLMEnvironmentDetector(IEnvironment environment)
        => _environment = environment ?? throw new ArgumentNullException(nameof(environment));

    /// <summary>
    /// Detects if the current environment is hosted by a known LLM/AI agent CLI.
    /// </summary>
    /// <returns><c>true</c> if a known LLM agent environment is detected; otherwise, <c>false</c>.</returns>
    public bool IsLLMEnvironment()
        => DetectionRules.Any(r => r.GetResult(_environment) is not null);

    /// <summary>
    /// Base class for environment detection rules that can be evaluated against environment variables.
    /// </summary>
    private abstract class EnvironmentDetectionRule
    {
        /// <summary>
        /// Evaluates the rule against the provided environment abstraction.
        /// </summary>
        /// <param name="environment">The environment abstraction to use for reading environment variables.</param>
        /// <returns>True if the rule matches the current environment; otherwise, false.</returns>
        public abstract bool IsMatch(IEnvironment environment);
    }

    /// <summary>
    /// Rule that matches when any of the specified environment variables is present and not null/empty.
    /// </summary>
    private sealed class AnyPresentEnvironmentRule : EnvironmentDetectionRule
    {
        private readonly string[] _variables;

        public AnyPresentEnvironmentRule(params string[] variables)
            => _variables = variables ?? throw new ArgumentNullException(nameof(variables));

        public override bool IsMatch(IEnvironment environment)
            => _variables.Any(variable => !RoslynString.IsNullOrEmpty(environment.GetEnvironmentVariable(variable)));
    }

    /// <summary>
    /// Rule that matches when any of the specified sub-rules match.
    /// </summary>
    private sealed class AnyMatchEnvironmentRule : EnvironmentDetectionRule
    {
        private readonly EnvironmentDetectionRule[] _rules;

        public AnyMatchEnvironmentRule(params EnvironmentDetectionRule[] rules)
            => _rules = rules ?? throw new ArgumentNullException(nameof(rules));

        public override bool IsMatch(IEnvironment environment)
            => _rules.Any(rule => rule.IsMatch(environment));
    }

    /// <summary>
    /// Rule that matches when an environment variable contains a specific value (case-insensitive).
    /// </summary>
    private sealed class EnvironmentVariableValueRule : EnvironmentDetectionRule
    {
        private readonly string _variable;
        private readonly string _expectedValue;

        public EnvironmentVariableValueRule(string variable, string expectedValue)
        {
            _variable = variable ?? throw new ArgumentNullException(nameof(variable));
            _expectedValue = expectedValue ?? throw new ArgumentNullException(nameof(expectedValue));
        }

        public override bool IsMatch(IEnvironment environment)
        {
            string? value = environment.GetEnvironmentVariable(_variable);
            return !RoslynString.IsNullOrEmpty(value) && value.Equals(_expectedValue, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Rule that matches when any of the specified environment variables is present and not null/empty,
    /// and returns the associated result value.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    private sealed class EnvironmentDetectionRuleWithResult<T>
        where T : class
    {
        private readonly EnvironmentDetectionRule _rule;
        private readonly T _result;

        public EnvironmentDetectionRuleWithResult(T result, EnvironmentDetectionRule rule)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _result = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>
        /// Evaluates the rule against the provided environment and returns the result if matched.
        /// </summary>
        /// <param name="environment">The environment abstraction to use for reading environment variables.</param>
        /// <returns>The result value if the rule matches; otherwise, null.</returns>
        public T? GetResult(IEnvironment environment)
            => _rule.IsMatch(environment) ? _result : null;
    }
}
