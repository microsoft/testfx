// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Interface to notify the completion of a request.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IExecuteRequestCompletionNotifier
{
    /// <summary>
    /// Notifies the completion of the request.
    /// </summary>
    void Complete();
}
