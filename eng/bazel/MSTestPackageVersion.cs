// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

if (args.Length != 2)
{
    throw new ArgumentException("Expected <platform-version-file> <dependency-range-file>.");
}

string version = File.ReadAllText(args[0]).Trim();
const string Suffix = "-preview.bazel";
if (!version.EndsWith(Suffix, StringComparison.Ordinal))
{
    throw new InvalidDataException($"Expected an experimental native package version, got '{version}'.");
}

Version prefix = Version.Parse(version[..^Suffix.Length]);
int nextMajor = checked(prefix.Major + 1);
File.WriteAllText(args[1], $"[{version}, {nextMajor}.0.0)", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
