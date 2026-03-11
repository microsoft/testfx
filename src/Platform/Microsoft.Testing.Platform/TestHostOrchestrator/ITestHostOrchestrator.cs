// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Back-compat shim: the old ITestHostOrchestrator was renamed to ITestHostExecutionOrchestrator.
namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

internal interface ITestHostOrchestrator : ITestHostExecutionOrchestrator;
