// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

public interface IDataProducer : IExtension
{
    // We don't use IReadOnlyCollection because we don't have cross api(like Contains) that are good in every tfm.
    // Internally we use Array.IndexOf to verify if the data type is supported, it's a hot path.
    Type[] DataTypesProduced { get; }
}
