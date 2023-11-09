// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// This exception is thrown if some internal invariant of the code is violated
/// and as such in normal operation should be never reached.
/// </summary>
internal sealed class UnreachableException : Exception
{
}
