// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed partial class CrashDumpProcessLifetimeHandler
{
    private sealed class CrashDumpSequenceFileHandler
    {
        private readonly CrashDumpProcessLifetimeHandler _owner;
        private readonly IMessageBus _messageBus;
        private readonly IOutputDevice _outputDisplay;
        private readonly CrashDumpConfiguration _configuration;

        public CrashDumpSequenceFileHandler(
            CrashDumpProcessLifetimeHandler owner,
            IMessageBus messageBus,
            IOutputDevice outputDisplay,
            CrashDumpConfiguration configuration)
        {
            _owner = owner;
            _messageBus = messageBus;
            _outputDisplay = outputDisplay;
            _configuration = configuration;
        }

        public async Task TryPublishAsync(CancellationToken cancellationToken)
        {
            string? sequenceFilePath = _configuration.SequenceFileName;
            if (RoslynString.IsNullOrEmpty(sequenceFilePath) || !File.Exists(sequenceFilePath))
            {
                return;
            }

            var inFlight = new Dictionary<string, (string DisplayName, DateTimeOffset StartedAt)>(StringComparer.Ordinal);
            DateTimeOffset latestSeen = DateTimeOffset.MinValue;
            try
            {
                foreach (string line in File.ReadLines(sequenceFilePath))
                {
                    if (line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }

                    string[] parts = line.Split('\t');
                    if (parts.Length < 4
                        || !DateTimeOffset.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset timestamp))
                    {
                        continue;
                    }

                    if (timestamp > latestSeen)
                    {
                        latestSeen = timestamp;
                    }

                    string uid = parts[2];
                    string lastField = parts.Length == 4 ? parts[3] : string.Join("\t", parts, 3, parts.Length - 3);
                    if (parts[0].Equals(CrashDumpSequenceLogger.StartedEvent, StringComparison.Ordinal))
                    {
                        inFlight[uid] = (lastField, timestamp);
                    }
                    else if (parts[0].Equals(CrashDumpSequenceLogger.EndedEvent, StringComparison.Ordinal))
                    {
                        inFlight.Remove(uid);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException)
            {
                await _outputDisplay.DisplayAsync(
                    _owner,
                    new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpSequenceFileReadError, sequenceFilePath, ex.Message)),
                    cancellationToken).ConfigureAwait(false);
                await PublishArtifactAsync(sequenceFilePath).ConfigureAwait(false);
                return;
            }

            if (inFlight.Count > 0)
            {
                await _outputDisplay.DisplayAsync(_owner, new ErrorMessageOutputDeviceData(CrashDumpResources.CrashDumpTestsRunningAtCrash), cancellationToken).ConfigureAwait(false);
                DateTimeOffset anchor = latestSeen == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : latestSeen;
                foreach (KeyValuePair<string, (string DisplayName, DateTimeOffset StartedAt)> entry in inFlight.OrderBy(static x => x.Value.StartedAt))
                {
                    TimeSpan elapsed = anchor - entry.Value.StartedAt;
                    if (elapsed < TimeSpan.Zero)
                    {
                        elapsed = TimeSpan.Zero;
                    }

                    await _outputDisplay.DisplayAsync(_owner, new ErrorMessageOutputDeviceData($"[{elapsed}] {entry.Value.DisplayName}"), cancellationToken).ConfigureAwait(false);
                }
            }

            await PublishArtifactAsync(sequenceFilePath).ConfigureAwait(false);
        }

        public void TryDelete()
        {
            string? sequenceFilePath = _configuration.SequenceFileName;
            if (RoslynString.IsNullOrEmpty(sequenceFilePath))
            {
                return;
            }

            try
            {
                if (File.Exists(sequenceFilePath))
                {
                    File.Delete(sequenceFilePath);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup; a leftover sequence file is harmless after a successful run.
            }
            catch (UnauthorizedAccessException)
            {
                // Same rationale as IOException.
            }
        }

        private Task PublishArtifactAsync(string sequenceFilePath)
            => _messageBus.PublishAsync(
                _owner,
                new FileArtifact(
                    new FileInfo(sequenceFilePath),
                    CrashDumpResources.CrashDumpSequenceArtifactDisplayName,
                    CrashDumpResources.CrashDumpSequenceArtifactDescription));
    }
}
