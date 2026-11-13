// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

bool exactFramework = args.Length == 4 && args[3] == "--exact-framework";
if (args.Length != 3 && !exactFramework)
{
    throw new ArgumentException("Expected <package-request-template> <dependency-version-file> <package-request-output> [--exact-framework].");
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
    throw new InvalidDataException("Expected nonempty dependency groups.");
}

if (exactFramework)
{
    if (request["metadata"]?["id"]?.GetValue<string>() is not ("MSTest.Uwp" or "MSTest.WinUI"))
    {
        throw new InvalidDataException("Only UI packages may pin the matching framework dependency.");
    }

    string ownVersionFile = request["versionFile"]?.GetValue<string>()
        ?? throw new InvalidDataException("The UI package must declare its own version file.");
    if (File.ReadAllText(ownVersionFile).Trim() != version)
    {
        throw new InvalidDataException("The UI package and core dependency versions must match exactly.");
    }
}

string dependencyId = exactFramework ? "MSTest.TestFramework" : "Microsoft.Testing.Platform";
foreach (JsonNode? group in groups)
{
    if (group?["dependencies"] is not JsonArray dependencies)
    {
        throw new InvalidDataException("A package dependency group is missing its dependencies.");
    }

    JsonObject[] bounded = dependencies.OfType<JsonObject>()
        .Where(dependency => dependency["id"]?.GetValue<string>() == dependencyId)
        .ToArray();
    if (bounded.Length != 1)
    {
        throw new InvalidDataException($"Each bounded group must declare exactly one {dependencyId} dependency.");
    }

    _ = bounded[0].Remove("versionFile");
    bounded[0]["version"] = exactFramework ? $"[{version}]" : $"[{version}, {nextMajor}.0.0)";
}

File.WriteAllText(args[2], request.ToJsonString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
