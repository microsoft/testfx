﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public interface IProcessHelper
{
    IProcessHandle Start(ProcessStartInfo startInfo, bool cleanDefaultEnvironmentVariableIfCustomAreProvided = false, int timeoutInSeconds = 300);
}
