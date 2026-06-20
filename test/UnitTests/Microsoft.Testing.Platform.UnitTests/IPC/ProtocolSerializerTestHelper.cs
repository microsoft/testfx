// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Shared reflection-based helpers for exercising the dotnet test wire-protocol serializers from unit tests. The
/// serializers expose their <c>Serialize</c>/<c>Deserialize</c> methods non-publicly, so the tests reach them via
/// reflection. Used by both <see cref="ProtocolTests"/> and <see cref="ProtocolEdgeCaseTests"/>.
/// </summary>
internal static class ProtocolSerializerTestHelper
{
    public static TMessage RoundTrip<TMessage>(object serializer, TMessage message)
    {
        var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        return (TMessage)Deserialize(serializer, stream);
    }

    public static void Serialize<TMessage>(object serializer, TMessage message, Stream stream)
        => serializer.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == nameof(Serialize) && method.GetParameters() is [{ ParameterType: var messageType }, { ParameterType: var streamType }] && messageType == typeof(TMessage) && streamType == typeof(Stream))
            .Invoke(serializer, [message!, stream]);

    public static object Deserialize(object serializer, Stream stream)
        => serializer.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == nameof(Deserialize) && method.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == typeof(Stream))
            .Invoke(serializer, [stream])!;
}
