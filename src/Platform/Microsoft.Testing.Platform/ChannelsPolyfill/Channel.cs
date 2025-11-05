#if !NETCOREAPP
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Threading.Channels
{
    /// <summary>Provides static methods for creating channels.</summary>
    public static partial class Channel
    {
        /// <summary>Creates an unbounded channel usable by any number of readers and writers concurrently.</summary>
        /// <returns>The created channel.</returns>
        public static Channel<T> CreateUnbounded<T>() =>
            new UnboundedChannel<T>(runContinuationsAsynchronously: true);

        /// <summary>Creates an unbounded channel subject to the provided options.</summary>
        /// <typeparam name="T">Specifies the type of data in the channel.</typeparam>
        /// <param name="options">Options that guide the behavior of the channel.</param>
        /// <returns>The created channel.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
        public static Channel<T> CreateUnbounded<T>(UnboundedChannelOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (options.SingleReader)
            {
                return new SingleConsumerUnboundedChannel<T>(!options.AllowSynchronousContinuations);
            }

            return new UnboundedChannel<T>(!options.AllowSynchronousContinuations);
        }
    }
}
#endif
