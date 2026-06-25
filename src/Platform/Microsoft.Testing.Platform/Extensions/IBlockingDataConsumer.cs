// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// Marker interface for a <see cref="IDataConsumer"/> that must consume the data inline,
/// before the call to publish the data returns to the producer.
/// </summary>
/// <remarks>
/// <para>
/// A regular <see cref="IDataConsumer"/> consumes data asynchronously: the data is queued and
/// processed on a background loop, so there is no guarantee about when <see cref="IDataConsumer.ConsumeAsync"/>
/// runs relative to the producer continuing its work. A consumer that additionally implements
/// <see cref="IBlockingDataConsumer"/> is instead invoked inline by the message bus, so the producer's
/// call to publish does not return until <see cref="IDataConsumer.ConsumeAsync"/> has completed.
/// This is useful for extensions that need to guarantee a piece of work happens before the producer
/// proceeds (for example, before a test starts running).
/// </para>
/// <para>
/// Because consumption happens inline, any exception thrown by <see cref="IDataConsumer.ConsumeAsync"/>
/// propagates back to the producer that published the data, rather than being observed later during
/// drain. Implementations should therefore avoid throwing for non-fatal conditions.
/// </para>
/// <para>
/// Inline consumption is serialized: only one <see cref="IDataConsumer.ConsumeAsync"/> call runs at a
/// time per consumer. As a consequence, a blocking consumer must not, from within its
/// <see cref="IDataConsumer.ConsumeAsync"/>, publish data that is routed back to itself, as that would
/// deadlock.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IBlockingDataConsumer : IDataConsumer;
