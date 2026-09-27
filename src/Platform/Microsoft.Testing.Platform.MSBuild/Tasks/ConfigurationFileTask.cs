// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#if NETCOREAPP
using System.Text.Json;
#else
using Jsonite;
#endif

namespace Microsoft.Testing.Platform.MSBuild;

/// <summary>
/// A task that creates the Microsoft Testing Platform configuration file in the output directory.
/// </summary>
// Took inspiration from https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/GenerateRuntimeConfigurationFiles.cs
public sealed class ConfigurationFileTask : Build.Utilities.Task
{
    private const string ConfigurationFileNameSuffix = "testconfig.json";
    private readonly IFileSystem _fileSystem;

    internal ConfigurationFileTask(IFileSystem fileSystem)
        => _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationFileTask"/> class.
    /// </summary>
    public ConfigurationFileTask()
        : this(new FileSystem())
    {
    }

    /// <summary>
    /// Gets or sets the Microsoft Testing Platform configuration file source.
    /// </summary>
    [Required]
    public required ITaskItem TestingPlatformConfigurationFileSource { get; set; }

    /// <summary>
    /// Gets or sets the MSBuild project directory.
    /// </summary>
    [Required]
    public required ITaskItem MSBuildProjectDirectory { get; set; }

    /// <summary>
    /// Gets or sets the assembly name.
    /// </summary>
    [Required]
    public required ITaskItem AssemblyName { get; set; }

    /// <summary>
    /// Gets or sets the output path.
    /// </summary>
    [Required]
    public required ITaskItem OutputPath { get; set; }

    /// <summary>
    /// Gets or sets passive command-line option defaults to merge into the generated configuration file.
    /// Item identities are option names and the <c>Value</c> metadata contains one argument value.
    /// Repeating an identity produces a JSON array.
    /// </summary>
    public ITaskItem[]? TestingPlatformCommandLineOptionDefault { get; set; }

    /// <summary>
    /// Gets or sets the final Microsoft Testing Platform configuration file. It stays <see langword="null"/> when
    /// neither a source configuration file nor option defaults were provided, in which case the task produces no output item.
    /// </summary>
    [Output]
    public ITaskItem? FinalTestingPlatformConfigurationFile { get; set; }

    /// <inheritdoc/>
    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.Normal, $"Microsoft Testing Platform configuration file: '{TestingPlatformConfigurationFileSource.ItemSpec}'");
        bool sourceExists = _fileSystem.Exist(TestingPlatformConfigurationFileSource.ItemSpec);
        if (!sourceExists && TestingPlatformCommandLineOptionDefault is not { Length: > 0 })
        {
            Log.LogMessage(MessageImportance.Normal, "Microsoft Testing Platform configuration file not found");
            return true;
        }

        Log.LogMessage(MessageImportance.Normal, $"MSBuildProjectDirectory: '{MSBuildProjectDirectory.ItemSpec}'");
        Log.LogMessage(MessageImportance.Normal, $"AssemblyName: '{AssemblyName.ItemSpec}'");
        Log.LogMessage(MessageImportance.Normal, $"OutputPath: '{OutputPath.ItemSpec}'");

        string finalPath = Path.Combine(MSBuildProjectDirectory.ItemSpec, OutputPath.ItemSpec);
        Log.LogMessage(MessageImportance.Normal, $"Final path: '{finalPath}'");

        string finalFileName = Path.Combine(finalPath, $"{AssemblyName.ItemSpec}.{ConfigurationFileNameSuffix}");
        Log.LogMessage(MessageImportance.Normal, $"Final configuration file path : '{finalFileName}'");

        if (TestingPlatformCommandLineOptionDefault is not { Length: > 0 } optionDefaults)
        {
            Log.LogMessage(MessageImportance.Normal, $"Configuration file found: '{TestingPlatformConfigurationFileSource.ItemSpec}'");
            _fileSystem.CopyFile(TestingPlatformConfigurationFileSource.ItemSpec, finalFileName);
        }
        else
        {
            if (!TryCreateMergedConfiguration(
                sourceExists ? _fileSystem.ReadAllText(TestingPlatformConfigurationFileSource.ItemSpec) : null,
                optionDefaults,
                out string? mergedConfiguration))
            {
                return false;
            }

            _fileSystem.WriteAllText(finalFileName, mergedConfiguration);
        }

        FinalTestingPlatformConfigurationFile = new TaskItem(finalFileName);
        Log.LogMessage(MessageImportance.Normal, "Microsoft Testing Platform configuration file written");

        return true;
    }

    private bool TryCreateMergedConfiguration(
        string? source,
        ITaskItem[] optionDefaults,
        [NotNullWhen(true)] out string? mergedConfiguration)
    {
        var valuesByOption = new Dictionary<string, (string Name, List<string> Values)>(StringComparer.OrdinalIgnoreCase);
        foreach (ITaskItem item in optionDefaults)
        {
            string optionName = item.ItemSpec.Trim();
            if (RoslynString.IsNullOrEmpty(optionName))
            {
                Log.LogError("TestingPlatformCommandLineOptionDefault items must have a non-empty option name.");
                mergedConfiguration = null;
                return false;
            }

            if (optionName[0] == '-')
            {
                Log.LogError($"TestingPlatformCommandLineOptionDefault item '{optionName}' must use the option name without leading hyphens.");
                mergedConfiguration = null;
                return false;
            }

            if (!valuesByOption.TryGetValue(optionName, out (string Name, List<string> Values) entry))
            {
                entry = (optionName, []);
            }

            entry.Values.Add(item.GetMetadata("Value"));
            valuesByOption[optionName] = entry;
        }

#if NETCOREAPP
        return TryCreateMergedConfigurationNet(source, valuesByOption.Values, out mergedConfiguration);
#else
        return TryCreateMergedConfigurationNetStandard(source, valuesByOption.Values, out mergedConfiguration);
#endif
    }

#if NETCOREAPP
    private bool TryCreateMergedConfigurationNet(
        string? source,
        IEnumerable<(string Name, List<string> Values)> optionDefaults,
        [NotNullWhen(true)] out string? mergedConfiguration)
    {
        try
        {
            if (source is not null)
            {
                using var document = JsonDocument.Parse(
                    source,
                    new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                    });

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    Log.LogError("The top-level element in the Microsoft Testing Platform configuration file must be a JSON object.");
                    mergedConfiguration = null;
                    return false;
                }

                return TryWriteMergedConfiguration(document.RootElement, optionDefaults, out mergedConfiguration);
            }

            return TryWriteMergedConfiguration(root: null, optionDefaults, out mergedConfiguration);
        }
        catch (JsonException ex)
        {
            Log.LogError($"Failed to parse Microsoft Testing Platform configuration file '{TestingPlatformConfigurationFileSource.ItemSpec}': {ex.Message}");
            mergedConfiguration = null;
            return false;
        }
    }

    private bool TryWriteMergedConfiguration(
        JsonElement? root,
        IEnumerable<(string Name, List<string> Values)> optionDefaults,
        [NotNullWhen(true)] out string? mergedConfiguration)
    {
        const string SectionName = "commandLineOptionDefaults";
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            bool wroteDefaults = false;
            var rootPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root is JsonElement rootElement)
            {
                foreach (JsonProperty property in rootElement.EnumerateObject())
                {
                    if (!rootPropertyNames.Add(property.Name))
                    {
                        Log.LogError($"The Microsoft Testing Platform configuration file contains duplicate keys that differ only by casing: '{property.Name}'.");
                        mergedConfiguration = null;
                        return false;
                    }

                    if (!string.Equals(property.Name, SectionName, StringComparison.OrdinalIgnoreCase))
                    {
                        property.WriteTo(writer);
                        continue;
                    }

                    if (property.Value.ValueKind != JsonValueKind.Object)
                    {
                        Log.LogError($"The '{SectionName}' section in the Microsoft Testing Platform configuration file must be a JSON object.");
                        mergedConfiguration = null;
                        return false;
                    }

                    writer.WritePropertyName(property.Name);
                    writer.WriteStartObject();
                    var configuredOptionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (JsonProperty optionDefault in property.Value.EnumerateObject())
                    {
                        if (!configuredOptionNames.Add(optionDefault.Name))
                        {
                            Log.LogError($"The '{SectionName}' section contains duplicate option names that differ only by casing: '{optionDefault.Name}'.");
                            mergedConfiguration = null;
                            return false;
                        }

                        optionDefault.WriteTo(writer);
                    }

                    WriteOptionDefaults(writer, optionDefaults, configuredOptionNames);
                    writer.WriteEndObject();
                    wroteDefaults = true;
                }
            }

            if (!wroteDefaults)
            {
                writer.WritePropertyName(SectionName);
                writer.WriteStartObject();
                WriteOptionDefaults(writer, optionDefaults, []);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        mergedConfiguration = Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
        return true;
    }

    private static void WriteOptionDefaults(
        Utf8JsonWriter writer,
        IEnumerable<(string Name, List<string> Values)> optionDefaults,
        HashSet<string> configuredOptionNames)
    {
        foreach ((string optionName, List<string> values) in optionDefaults)
        {
            if (configuredOptionNames.Contains(optionName))
            {
                continue;
            }

            writer.WritePropertyName(optionName);
            if (values.Count == 1)
            {
                writer.WriteStringValue(values[0]);
            }
            else
            {
                writer.WriteStartArray();
                foreach (string value in values)
                {
                    writer.WriteStringValue(value);
                }

                writer.WriteEndArray();
            }
        }
    }
#else
    private bool TryCreateMergedConfigurationNetStandard(
        string? source,
        IEnumerable<(string Name, List<string> Values)> optionDefaults,
        [NotNullWhen(true)] out string? mergedConfiguration)
    {
        JsonObject configuration;
        try
        {
            if (source is null)
            {
                configuration = [];
            }
            else if (Json.Deserialize(
                source,
                new JsonSettings
                {
                    AllowComments = true,
                    AllowTrailingCommas = true,
                }) is JsonObject parsedObject)
            {
                configuration = parsedObject;
            }
            else
            {
                Log.LogError("The top-level element in the Microsoft Testing Platform configuration file must be a JSON object.");
                mergedConfiguration = null;
                return false;
            }
        }
        catch (JsonException ex)
        {
            Log.LogError($"Failed to parse Microsoft Testing Platform configuration file '{TestingPlatformConfigurationFileSource.ItemSpec}': {ex.Message}");
            mergedConfiguration = null;
            return false;
        }

        if (!TryGetDefaultsObject(configuration, out JsonObject? defaultsObject))
        {
            mergedConfiguration = null;
            return false;
        }

        foreach ((string optionName, List<string> values) in optionDefaults)
        {
            if (!TryGetCaseInsensitiveValue(defaultsObject, optionName, out bool hasConfiguredDefault, out _))
            {
                mergedConfiguration = null;
                return false;
            }

            if (hasConfiguredDefault)
            {
                continue;
            }

            if (values.Count == 1)
            {
                defaultsObject.Add(optionName, values[0]);
            }
            else
            {
                var array = new JsonArray { Capacity = values.Count };
                array.AddRange(values);
                defaultsObject.Add(optionName, array);
            }
        }

        mergedConfiguration = Json.Serialize(configuration, new JsonSettings { Indent = true }) + Environment.NewLine;
        return true;
    }

    private bool TryGetDefaultsObject(JsonObject configuration, [NotNullWhen(true)] out JsonObject? defaultsObject)
    {
        const string SectionName = "commandLineOptionDefaults";
        if (!TryGetCaseInsensitiveValue(configuration, SectionName, out bool hasSection, out object? sectionValue))
        {
            defaultsObject = null;
            return false;
        }

        if (!hasSection)
        {
            defaultsObject = [];
            configuration.Add(SectionName, defaultsObject);
            return true;
        }

        if (sectionValue is JsonObject sectionObject)
        {
            defaultsObject = sectionObject;
            return true;
        }

        Log.LogError($"The '{SectionName}' section in the Microsoft Testing Platform configuration file must be a JSON object.");
        defaultsObject = null;
        return false;
    }

    private bool TryGetCaseInsensitiveValue(JsonObject jsonObject, string requestedKey, out bool found, out object? value)
    {
        found = false;
        value = null;
        string? actualKey = null;
        foreach (KeyValuePair<string, object?> entry in jsonObject)
        {
            if (!string.Equals(entry.Key, requestedKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (actualKey is not null)
            {
                Log.LogError($"The Microsoft Testing Platform configuration file contains duplicate keys that differ only by casing: '{actualKey}' and '{entry.Key}'.");
                return false;
            }

            actualKey = entry.Key;
            found = true;
            value = entry.Value;
        }

        return true;
    }
#endif
}
