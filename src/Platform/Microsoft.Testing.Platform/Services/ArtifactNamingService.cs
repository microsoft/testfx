// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal sealed class ArtifactNamingService : IArtifactNamingService
{
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IEnvironment _environment;
    private readonly IClock _clock;

    public ArtifactNamingService(ITestApplicationModuleInfo testApplicationModuleInfo, IEnvironment environment, IClock clock)
    {
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _environment = environment;
        _clock = clock;
    }

    public string ResolveFileName(string template)
    {
        string processName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string processId = _environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        return ArtifactNamingHelper.ResolveAndSanitize(template, processName, processId, _clock.UtcNow, ArtifactFileNameSanitizer.ReplaceInvalidFileNameChars);
    }
}
