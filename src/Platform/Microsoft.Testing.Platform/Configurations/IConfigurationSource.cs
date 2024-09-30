// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Configurations;

internal interface IConfigurationSource : IExtension
{
    IConfigurationProvider Build(CommandLineParseResult commandLineParseResult);
}
