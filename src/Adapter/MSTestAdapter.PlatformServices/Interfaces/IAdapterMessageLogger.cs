// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// Platform-agnostic sink for diagnostic messages (informational / warning / error) that the adapter
/// surfaces to whichever test host is running the tests.
/// </summary>
/// <remarks>
/// This abstraction lets the platform services layer report messages without taking a dependency on a
/// specific test platform's object model. The mapping to a concrete host logger (for example the VSTest
/// <c>IMessageLogger</c>) is provided by the adapter layer.
/// </remarks>
internal interface IAdapterMessageLogger
{
    /// <summary>
    /// Reports a diagnostic message to the running test host.
    /// </summary>
    /// <param name="level">The severity of the message.</param>
    /// <param name="message">The message text.</param>
    void SendMessage(MessageLevel level, string message);
}
