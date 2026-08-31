// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsRunIdCoordinator
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly UTF8Encoding Utf8EncodingWithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Logs a warning, swallowing any failure from the logging providers.
    /// </summary>
    /// <remarks>
    /// The aggregate logger invokes each provider directly, so a failing provider propagates. Every caller
    /// here is already recovering from something, and letting a diagnostic replace the failure it was
    /// describing would lose both.
    /// </remarks>
    private void TryLogWarning(string message)
    {
        try
        {
            _logger.LogWarning(message);
        }
        catch (Exception)
        {
            // There is nowhere left to report this: the diagnostic logger is the fallback sink.
        }
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "The coordination payload type is internal, fixed, and controlled by this extension.")]
    private async Task WriteJsonFileAsync<TPayload>(string path, TPayload payload, bool overwrite, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(payload, JsonSerializerOptions);
        using IFileStream stream = _fileSystem.NewFileStream(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using StreamWriter writer = new(stream.Stream, Utf8EncodingWithoutBom, 1024, leaveOpen: true);
#if NET
        await writer.WriteAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
        await writer.WriteAsync(json).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
#endif
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (_fileSystem.ExistFile(path))
            {
                _fileSystem.DeleteFile(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToDeleteCoordinationFile} {path}: {ex.Message}");
        }
    }
}
