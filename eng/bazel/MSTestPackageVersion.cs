// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

if (args.Length != 3)
{
    throw new ArgumentException("Expected <package-request-template> <platform-version-file> <package-request-output>.");
}

string version = File.ReadAllText(args[1]).Trim();
const string Suffix = "-preview.bazel";
if (!version.EndsWith(Suffix, StringComparison.Ordinal))
{
    throw new InvalidDataException($"Expected an experimental native package version, got '{version}'.");
}

Version prefix = Version.Parse(version[..^Suffix.Length]);
int nextMajor = checked(prefix.Major + 1);
JsonObject request = JsonNode.Parse(File.ReadAllText(args[0])) as JsonObject
    ?? throw new InvalidDataException("Expected a package request object.");
if (request["dependencyGroups"] is not JsonArray groups || groups.Count == 0)
{
    throw new InvalidDataException("Expected nonempty platform dependency groups.");
}

foreach (JsonNode? group in groups)
{
    if (group?["dependencies"] is not JsonArray dependencies)
    {
        throw new InvalidDataException("A package dependency group is missing its dependencies.");
    }

    JsonObject[] platform = dependencies.OfType<JsonObject>()
        .Where(dependency => dependency["id"]?.GetValue<string>() == "Microsoft.Testing.Platform")
        .ToArray();
    if (platform.Length != 1)
    {
        throw new InvalidDataException("Each bounded group must declare exactly one platform dependency.");
    }

    _ = platform[0].Remove("versionFile");
    platform[0]["version"] = $"[{version}, {nextMajor}.0.0)";
}

File.WriteAllText(args[2], request.ToJsonString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
