// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed partial class JsonConfigurationSource
{
    internal sealed class JsonConfigurationProvider : IConfigurationProvider
    {
        private readonly ITestApplicationModuleInfo _currentModuleInfo;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger? _logger;
        private Dictionary<string, string?>? _propertyToAllChildren;
        private Dictionary<string, string?>? _singleValueData;

        public string? ConfigurationFile { get; private set; }

        private async Task LogInformationAsync(string message)
        {
            if (_logger is not null)
            {
                await _logger.LogInformationAsync(message);
            }
        }

        public JsonConfigurationProvider(IRuntime runtime, IFileSystem fileSystem, ILogger? logger)
        {
            _currentModuleInfo = runtime.GetCurrentModuleInfo();
            _fileSystem = fileSystem;
            _logger = logger;
        }

        public async Task LoadAsync()
        {
            string configFileName = $"{Path.Combine(
                Path.GetDirectoryName(_currentModuleInfo.GetCurrentTestApplicationFullPath())!,
                Path.GetFileNameWithoutExtension(_currentModuleInfo.GetCurrentTestApplicationFullPath()))}{PlatformConfigurationConstants.PlatformConfigSuffixFileName}";
            if (!_fileSystem.Exists(configFileName))
            {
                await LogInformationAsync($"Config file '{configFileName}' not found.");
                return;
            }

            await LogInformationAsync($"Config file '{configFileName}' loaded.");

            ConfigurationFile = configFileName;

            using Stream fileStream = _fileSystem.NewFileStream(configFileName, FileMode.Open, FileAccess.Read);
            (_singleValueData, _propertyToAllChildren) = JsonConfigurationFileParser.Parse(fileStream);
        }

        public bool TryGet(string key, out string? value)
        {
            value = null;
            return (_singleValueData != null && _singleValueData.TryGetValue(key, out value)) || (_propertyToAllChildren != null && _propertyToAllChildren.TryGetValue(key, out value));
        }
    }
}
