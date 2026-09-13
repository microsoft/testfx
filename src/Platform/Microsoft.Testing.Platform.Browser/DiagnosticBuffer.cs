// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal sealed class DiagnosticBuffer
{
    private const int MaximumEntries = 200;
    private const int MaximumEntryLength = 4 * 1024;

    private readonly Queue<string> _entries = new();
    private readonly object _sync = new();
    private readonly string[] _secrets;

    public DiagnosticBuffer(params string?[] secrets)
        => _secrets =
        [
            .. secrets
                .Where(static value => value is { Length: >= 8 })
                .Cast<string>(),
        ];

    public void Add(string source, string message)
    {
        string redacted = message;
        foreach (string secret in _secrets)
        {
            redacted = redacted.Replace(secret, "[redacted]", StringComparison.Ordinal);
        }

        if (redacted.Length > MaximumEntryLength)
        {
            redacted = redacted[..MaximumEntryLength] + "...";
        }

        lock (_sync)
        {
            if (_entries.Count == MaximumEntries)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue($"[{source}] {redacted}");
        }
    }

    public string Format()
    {
        lock (_sync)
        {
            return _entries.Count == 0
                ? "(no diagnostics captured)"
                : string.Join(Environment.NewLine, _entries);
        }
    }
}
