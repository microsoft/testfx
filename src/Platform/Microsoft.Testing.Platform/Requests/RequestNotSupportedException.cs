// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

[Serializable]
public class RequestNotSupportedException : Exception
{
    public RequestNotSupportedException(IRequest request)
    {
        Request = request;
    }

    public IRequest Request { get; }
}
