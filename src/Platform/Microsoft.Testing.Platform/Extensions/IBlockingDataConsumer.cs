// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// Marker interface for a <see cref="IDataConsumer"/> that must consume the data synchronously,
/// before the call to publish the data returns to the producer.
/// </summary>
/// <remarks>
/// A regular <see cref="IDataConsumer"/> consumes data asynchronously: the data is queued and
/// processed on a background loop, so there is no guarantee about when <see cref="IDataConsumer.ConsumeAsync"/>
/// runs relative to the producer continuing its work. A consumer that additionally implements
/// <see cref="IBlockingDataConsumer"/> is instead invoked inline by the message bus, so the producer's
/// call to publish does not return until <see cref="IDataConsumer.ConsumeAsync"/> has completed.
/// This is useful for extensions that need to guarantee a piece of work happens before the producer
/// proceeds (for example, before a test starts running).
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IBlockingDataConsumer : IDataConsumer;
