// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.Policy;

internal sealed partial class RetryOrchestrator
{
    private const string AppContainerArtifactRootsConfiguredEnvironmentVariable = "TESTINGPLATFORM_PACKAGEDAPP_APPCONTAINER_ARTIFACT_ROOTS_CONFIGURED";
    private const string ArtifactPathDestinationRootEnvironmentVariable = "TESTINGPLATFORM_ARTIFACT_PATH_DESTINATION_ROOT";
    private const string DiagnosticArtifactPathDestinationRootEnvironmentVariable = "TESTINGPLATFORM_DIAGNOSTIC_ARTIFACT_PATH_DESTINATION_ROOT";
    private const long MaxRecoveredArtifactManifestBytes = 16L * 1024 * 1024;
    private const int MaxRecoveredArtifactManifestLineBytes = 64 * 1024;
    private const int MaxRecoveredArtifactManifestRecords = 10_000;
    private const int MaxRecoveredArtifactPathChars = 32 * 1024;
    private const int MaxRecoveredArtifactKindChars = 1024;

    private static void CollectRecoveredArtifacts(
        IFileSystem fileSystem,
        string manifestPath,
        string attemptDirectory,
        List<ArtifactRequest> artifacts,
        ILogger logger)
    {
        try
        {
            if (!fileSystem.ExistFile(manifestPath))
            {
                return;
            }

            using IFileStream stream = fileSystem.NewFileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var reader = new BoundedManifestLineReader(stream.Stream);
            int recordCount = 0;
            while (recordCount++ < MaxRecoveredArtifactManifestRecords)
            {
                BoundedManifestLineReadResult readResult = reader.ReadLine(out string line);
                if (readResult == BoundedManifestLineReadResult.End)
                {
                    return;
                }

                if (readResult == BoundedManifestLineReadResult.LimitExceeded)
                {
                    logger.LogWarning($"Stopped reading recovered retry artifact manifest '{manifestPath}' because it exceeded a configured size limit.");
                    return;
                }

                int separatorIndex = line.IndexOf('\t');
                if (separatorIndex <= 0)
                {
                    logger.LogWarning($"Ignoring malformed recovered retry artifact manifest entry in '{manifestPath}'.");
                    continue;
                }

                try
                {
                    string path = Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(0, separatorIndex)));
                    string encodedKind = line.Substring(separatorIndex + 1);
                    string? kind = encodedKind == "-"
                        ? null
                        : Encoding.UTF8.GetString(Convert.FromBase64String(encodedKind));
                    if (path.Length > MaxRecoveredArtifactPathChars
                        || kind?.Length > MaxRecoveredArtifactKindChars)
                    {
                        logger.LogWarning($"Ignoring oversized recovered retry artifact manifest entry in '{manifestPath}'.");
                        continue;
                    }

                    string artifactPath = Path.GetFullPath(path);
                    if (!IsUnderDirectory(artifactPath, attemptDirectory))
                    {
                        logger.LogWarning(
                            $"Ignoring recovered retry artifact '{path}' because it is outside the retry attempt directory '{attemptDirectory}'.");
                        continue;
                    }

                    if (!fileSystem.ExistFile(artifactPath))
                    {
                        logger.LogWarning($"Ignoring recovered retry artifact '{path}' because it does not exist.");
                        continue;
                    }

                    if (kind is not null)
                    {
                        artifacts.RemoveAll(artifact => string.Equals(artifact.Kind, kind, StringComparison.Ordinal));
                    }
                    else if (artifacts.Any(artifact => string.Equals(artifact.Path, artifactPath, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    artifacts.Add(new ArtifactRequest(artifactPath, kind));
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException or PathTooLongException)
                {
                    logger.LogWarning($"Ignoring malformed recovered retry artifact manifest entry in '{manifestPath}': {ex.Message}");
                }
            }

            logger.LogWarning($"Stopped reading recovered retry artifact manifest '{manifestPath}' after the maximum of {MaxRecoveredArtifactManifestRecords} records.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning($"Failed to read recovered retry artifact manifest '{manifestPath}': {ex}");
        }
        finally
        {
            try
            {
                if (fileSystem.ExistFile(manifestPath))
                {
                    fileSystem.DeleteFile(manifestPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning($"Failed to delete recovered retry artifact manifest '{manifestPath}': {ex}");
            }
        }
    }

    private static void RemoveArtifactsOutsideControllerRoots(
        IEnvironment environment,
        List<ArtifactRequest> artifacts,
        ILogger logger)
    {
        if (!string.Equals(
                environment.GetEnvironmentVariable(AppContainerArtifactRootsConfiguredEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        string? artifactRoot = environment.GetEnvironmentVariable(ArtifactPathDestinationRootEnvironmentVariable);
        string? diagnosticArtifactRoot = environment.GetEnvironmentVariable(DiagnosticArtifactPathDestinationRootEnvironmentVariable);
        string[] allowedRoots =
        [
            .. new[] { artifactRoot, diagnosticArtifactRoot }
                .OfType<string>()
                .Where(root => root.Length > 0)
                .Select(Path.GetFullPath),
        ];

        for (int i = artifacts.Count - 1; i >= 0; i--)
        {
            ArtifactRequest artifact = artifacts[i];
            try
            {
                string artifactPath = Path.GetFullPath(artifact.Path);
                if (allowedRoots.Any(root => IsUnderDirectory(artifactPath, root)))
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                logger.LogWarning($"Ignoring malformed retry artifact path '{artifact.Path}': {ex.Message}");
                artifacts.RemoveAt(i);
                continue;
            }

            logger.LogWarning(
                $"Ignoring retry artifact '{artifact.Path}' because it is outside the configured AppContainer artifact roots.");
            artifacts.RemoveAt(i);
        }
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        string directoryPrefix = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        StringComparison comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return path.StartsWith(
            directoryPrefix,
            comparison);
    }

    private enum BoundedManifestLineReadResult
    {
        Line,
        End,
        LimitExceeded,
    }

    private sealed class BoundedManifestLineReader(Stream stream)
    {
        private const int BufferSize = 8192;

        private readonly byte[] _readBuffer = new byte[BufferSize];
        private readonly byte[] _lineBuffer = new byte[MaxRecoveredArtifactManifestLineBytes];
        private int _readOffset;
        private int _readCount;
        private long _bytesRead;

        public BoundedManifestLineReadResult ReadLine(out string line)
        {
            int lineLength = 0;
            while (TryReadByte(out byte value))
            {
                if (++_bytesRead > MaxRecoveredArtifactManifestBytes)
                {
                    line = string.Empty;
                    return BoundedManifestLineReadResult.LimitExceeded;
                }

                if (value == (byte)'\n')
                {
                    return DecodeLine(lineLength, out line);
                }

                if (lineLength >= MaxRecoveredArtifactManifestLineBytes)
                {
                    line = string.Empty;
                    return BoundedManifestLineReadResult.LimitExceeded;
                }

                _lineBuffer[lineLength++] = value;
            }

            if (lineLength == 0)
            {
                line = string.Empty;
                return BoundedManifestLineReadResult.End;
            }

            return DecodeLine(lineLength, out line);
        }

        private BoundedManifestLineReadResult DecodeLine(int lineLength, out string line)
        {
            if (lineLength > 0 && _lineBuffer[lineLength - 1] == (byte)'\r')
            {
                lineLength--;
            }

            line = Encoding.UTF8.GetString(_lineBuffer, 0, lineLength);
            return BoundedManifestLineReadResult.Line;
        }

        private bool TryReadByte(out byte value)
        {
            if (_readOffset >= _readCount)
            {
                _readCount = stream.Read(_readBuffer, 0, _readBuffer.Length);
                _readOffset = 0;
                if (_readCount == 0)
                {
                    value = default;
                    return false;
                }
            }

            value = _readBuffer[_readOffset++];
            return true;
        }
    }
}
