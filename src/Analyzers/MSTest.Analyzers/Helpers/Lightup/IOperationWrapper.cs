// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers.Lightup;

internal interface IOperationWrapper
{
    IOperation? WrappedOperation { get; }
}
