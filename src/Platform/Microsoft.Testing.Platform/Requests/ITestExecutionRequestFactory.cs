// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Requests;

internal interface ITestExecutionRequestFactory
{
    Task<TestExecutionRequest> CreateRequestAsync(TestSessionContext session);
}
