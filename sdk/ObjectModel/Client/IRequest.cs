// ---------------------------------------------------------------------------
// <copyright file="IRequest.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.Threading;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    public interface IRequest : IDisposable
    {
        /// <summary>
        /// Waits for the request to complete
        /// </summary>
        /// <param name="timeout">Timeout</param>
        /// <returns>True if the request timeouts</returns>
        bool WaitForCompletion(int timeout);
    }

    public static class RequestExtensions
    {
        public static void WaitForCompletion(this IRequest request)
        {
            ValidateArg.NotNull(request, "request");

            request.WaitForCompletion(Timeout.Infinite);
        }
    }
}
