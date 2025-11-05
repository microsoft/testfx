#if !NETCOREAPP
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Threading.Channels
{
    /// <summary>Provides options that control the behavior of channel instances.</summary>
    public abstract class ChannelOptions
    {
        /// <summary>
        /// <code>true</code> if writers to the channel guarantee that there will only ever be at most one write operation
        /// at a time; <code>false</code> if no such constraint is guaranteed.
        /// </summary>
        /// <remarks>
        /// If true, the channel may be able to optimize certain operations based on knowing about the single-writer guarantee.
        /// The default is false.
        /// </remarks>
        public bool SingleWriter { get; set; }

        /// <summary>
        /// <code>true</code> if readers from the channel guarantee that there will only ever be at most one read operation
        /// at a time; <code>false</code> if no such constraint is guaranteed.
        /// </summary>
        /// <remarks>
        /// If true, the channel may be able to optimize certain operations based on knowing about the single-reader guarantee.
        /// The default is false.
        /// </remarks>
        public bool SingleReader { get; set; }

        /// <summary>
        /// <code>true</code> if operations performed on a channel may synchronously invoke continuations subscribed to
        /// notifications of pending async operations; <code>false</code> if all continuations should be invoked asynchronously.
        /// </summary>
        /// <remarks>
        /// Setting this option to <code>true</code> can provide measurable throughput improvements by avoiding
        /// scheduling additional work items. However, it may come at the cost of reduced parallelism, as for example a producer
        /// may then be the one to execute work associated with a consumer, and if not done thoughtfully, this can lead
        /// to unexpected interactions. The default is false.
        /// </remarks>
        public bool AllowSynchronousContinuations { get; set; }
    }

    /// <summary>Provides options that control the behavior of instances created by <see cref="M:Channel.CreateUnbounded"/>.</summary>
    public sealed class UnboundedChannelOptions : ChannelOptions
    {
    }
}
#endif
