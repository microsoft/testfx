// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Performance.Runner;

internal interface IStep<TInput, TOutput>
     where TInput : class, IPayload
     where TOutput : class, IPayload
{
    string Description { get; }

    Task<TOutput> ExecuteAsync(TInput payload, IContext context);
}
