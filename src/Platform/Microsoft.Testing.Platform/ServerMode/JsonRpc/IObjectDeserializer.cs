// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Note: System.Text.Json is only available in .NET 6.0 and above.
//       As such, we have two separate implementations for the serialization code.
namespace Microsoft.Testing.Platform.ServerMode;

internal interface IObjectDeserializer
{
    // Note: DeserializeObject method seems is done in two steps.
    // Firstly an object is deserialized into a generic string, object
    // dictionary and then the custom deserializer's DeserializeObject method is called.
    //
    // This is done because, the type of the value might not be known right away, if we try
    // to parse the json from top to bottom. I.e. if I have a payload:
    // {
    //   "params": { INITIALIZE_ARGS },
    //   "method": "initialize"
    // }
    // Note: We could let the deserializer control the entire deserialization process
    //       so that we could avoid the step of deserializing to a generic dictionary,
    //       however, we should create a more optimized interface only if it's really required
    //       for performance reasons.
    object? DeserializeObject(IDictionary<string, object?> properties);
}

internal sealed class ObjectDeserializer<T>(Func<IDictionary<string, object?>, T> fn) : IObjectDeserializer
{
    private readonly Func<IDictionary<string, object?>, T> _fn = fn;

    public object? DeserializeObject(IDictionary<string, object?> properties)
        => _fn(properties);
}
