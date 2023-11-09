// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

public interface ILoggerFactory
{
    ILogger CreateLogger(string categoryName);
}
