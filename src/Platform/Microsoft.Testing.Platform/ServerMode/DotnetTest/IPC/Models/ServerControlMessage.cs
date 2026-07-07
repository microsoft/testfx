// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

// The SDK's reply to a parked WaitForServerControlRequest on the reverse "server control" pipe. Kind is one of
// ServerControlKinds (e.g. CancelSession); on CancelSession the test host stops scheduling new tests and stops the
// current run cooperatively (preferring a graceful stop so trx/artifacts are still produced). Introduced with
// protocol version 1.4.0; the SDK only creates the control pipe when it advertises ServerControlPipeName, so an
// older SDK never sends this message.
internal sealed record ServerControlMessage(byte Kind) : IResponse;
