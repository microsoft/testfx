// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

// Copy from https://github.com/dotnet/sdk/tree/1e5d8e39d3026edb222cdf4f8d8240f1eb99f24b/src/Cli/Microsoft.DotNet.Cli.Definitions/Telemetry
internal static class LLMEnvironmentDetector
{
    private static readonly EnvironmentDetectionRuleWithResult<string>[] DetectionRules =
    [
        // Claude Code
        new EnvironmentDetectionRuleWithResult<string>("claude", new AnyPresentEnvironmentRule("CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT")),
        // Cursor AI
        new EnvironmentDetectionRuleWithResult<string>("cursor", new AnyPresentEnvironmentRule("CURSOR_EDITOR", "CURSOR_AI")),
        // Gemini
        new EnvironmentDetectionRuleWithResult<string>("gemini", new BooleanEnvironmentRule("GEMINI_CLI")),
        // GitHub Copilot (legacy gh extension: GITHUB_COPILOT_CLI_MODE=true; new Copilot CLI: GH_COPILOT_WORKING_DIRECTORY is set)
        new EnvironmentDetectionRuleWithResult<string>("copilot", new AnyMatchEnvironmentRule(
            new BooleanEnvironmentRule("GITHUB_COPILOT_CLI_MODE"),
            new AnyPresentEnvironmentRule("GH_COPILOT_WORKING_DIRECTORY"))),
        // Codex CLI
        new EnvironmentDetectionRuleWithResult<string>("codex", new AnyPresentEnvironmentRule("CODEX_CLI", "CODEX_SANDBOX")),
        // Aider
        new EnvironmentDetectionRuleWithResult<string>("aider", new EnvironmentVariableValueRule("OR_APP_NAME", "Aider")),
        // Plandex
        new EnvironmentDetectionRuleWithResult<string>("plandex", new EnvironmentVariableValueRule("OR_APP_NAME", "plandex")),
        // Amp
        new EnvironmentDetectionRuleWithResult<string>("amp", new AnyPresentEnvironmentRule("AMP_HOME")),
        // Qwen Code
        new EnvironmentDetectionRuleWithResult<string>("qwen", new AnyPresentEnvironmentRule("QWEN_CODE")),
        // Droid
        new EnvironmentDetectionRuleWithResult<string>("droid", new BooleanEnvironmentRule("DROID_CLI")),
        // OpenCode
        new EnvironmentDetectionRuleWithResult<string>("opencode", new AnyPresentEnvironmentRule("OPENCODE_AI")),
        // Zed AI
        new EnvironmentDetectionRuleWithResult<string>("zed", new AnyPresentEnvironmentRule("ZED_ENVIRONMENT", "ZED_TERM")),
        // Kimi CLI
        new EnvironmentDetectionRuleWithResult<string>("kimi", new BooleanEnvironmentRule("KIMI_CLI")),
        // OpenHands
        new EnvironmentDetectionRuleWithResult<string>("openhands", new EnvironmentVariableValueRule("OR_APP_NAME", "OpenHands")),
        // Goose
        new EnvironmentDetectionRuleWithResult<string>("goose", new AnyPresentEnvironmentRule("GOOSE_TERMINAL")),
        // Cline
        new EnvironmentDetectionRuleWithResult<string>("cline", new AnyPresentEnvironmentRule("CLINE_TASK_ID")),
        // Roo Code
        new EnvironmentDetectionRuleWithResult<string>("roo", new AnyPresentEnvironmentRule("ROO_CODE_TASK_ID")),
        // Windsurf
        new EnvironmentDetectionRuleWithResult<string>("windsurf", new AnyPresentEnvironmentRule("WINDSURF_SESSION")),
        // (proposed) generic flag for Agentic usage
        new EnvironmentDetectionRuleWithResult<string>("generic_agent", new BooleanEnvironmentRule("AGENT_CLI")),
    ];

    private static string? LLMEnvironment { get; } = GetLLMEnvironment();

    private static string? GetLLMEnvironment()
    {
        string?[] results = DetectionRules.Select(r => r.GetResult()).Where(r => r != null).ToArray();
        return results.Length > 0 ? string.Join(", ", results) : null;
    }

    public static bool IsLLMEnvironment() => !RoslynString.IsNullOrEmpty(LLMEnvironment);

    /// <summary>
    /// Base class for environment detection rules that can be evaluated against environment variables.
    /// </summary>
    private abstract class EnvironmentDetectionRule
    {
        /// <summary>
        /// Evaluates the rule against the current environment.
        /// </summary>
        /// <returns>True if the rule matches the current environment; otherwise, false.</returns>
        public abstract bool IsMatch();
    }

    /// <summary>
    /// Rule that matches when any of the specified environment variables is set to "true".
    /// </summary>
    private sealed class BooleanEnvironmentRule : EnvironmentDetectionRule
    {
        private readonly string[] _variables;

        public BooleanEnvironmentRule(params string[] variables)
            => _variables = variables ?? throw new ArgumentNullException(nameof(variables));

        public override bool IsMatch()
#pragma warning disable RS0030 // Do not use banned APIs - fine here.
            => _variables.Any(variable => EnvironmentVariableParser.ParseBool(Environment.GetEnvironmentVariable(variable), defaultValue: false));
#pragma warning restore RS0030 // Do not use banned APIs
    }

    private static class EnvironmentVariableParser
    {
        public static bool ParseBool(string? str, bool defaultValue)
        {
            if (str is "1" ||
                string.Equals(str, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(str, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(str, "on", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (str is "0" ||
                string.Equals(str, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(str, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(str, "off", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Not set to a known value, return default value.
            return defaultValue;
        }
    }

    /// <summary>
    /// Rule that matches when any of the specified environment variables is present and not null/empty.
    /// </summary>
    private sealed class AnyPresentEnvironmentRule : EnvironmentDetectionRule
    {
        private readonly string[] _variables;

        public AnyPresentEnvironmentRule(params string[] variables)
            => _variables = variables ?? throw new ArgumentNullException(nameof(variables));

        public override bool IsMatch()
#pragma warning disable RS0030 // Do not use banned APIs - fine here.
            => _variables.Any(variable => !RoslynString.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)));
#pragma warning restore RS0030 // Do not use banned APIs
    }

    /// <summary>
    /// Rule that matches when any of the specified sub-rules match.
    /// </summary>
    private sealed class AnyMatchEnvironmentRule : EnvironmentDetectionRule
    {
        private readonly EnvironmentDetectionRule[] _rules;

        public AnyMatchEnvironmentRule(params EnvironmentDetectionRule[] rules)
            => _rules = rules ?? throw new ArgumentNullException(nameof(rules));

        public override bool IsMatch()
            => _rules.Any(rule => rule.IsMatch());
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

        public override bool IsMatch()
        {
#pragma warning disable RS0030 // Do not use banned APIs - fine here.
            string? value = Environment.GetEnvironmentVariable(_variable);
#pragma warning restore RS0030 // Do not use banned APIs
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
        /// Evaluates the rule and returns the result if matched.
        /// </summary>
        /// <returns>The result value if the rule matches; otherwise, null.</returns>
        public T? GetResult()
            => _rule.IsMatch() ? _result : null;
    }
}
