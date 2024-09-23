// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class DataSerializationHelper
{
    private static readonly ConcurrentDictionary<string, DataContractJsonSerializer> SerializerCache = new();
    private static readonly DataContractJsonSerializerSettings SerializerSettings = new()
    {
        UseSimpleDictionaryFormat = true,
        EmitTypeInformation = System.Runtime.Serialization.EmitTypeInformation.Always,
        DateTimeFormat = new System.Runtime.Serialization.DateTimeFormat("O", System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// Serializes the date in such a way that won't throw exceptions during deserialization in Test Platform.
    /// The result can be deserialized using <see cref="Deserialize(string[])"/> method.
    /// </summary>
    /// <param name="data">Data array to serialize.</param>
    /// <returns>Serialized array.</returns>
    public static string?[]? Serialize(object?[]? data)
    {
        if (data == null)
        {
            return null;
        }

        string?[] serializedData = new string?[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            int typeIndex = i * 2;
            int dataIndex = typeIndex + 1;
            if (data[i] == null)
            {
                serializedData[typeIndex] = null;
                serializedData[dataIndex] = null;

                continue;
            }

            Type type = data[i]!.GetType();
            string? typeName = type.AssemblyQualifiedName;

            serializedData[typeIndex] = typeName;

            DataContractJsonSerializer serializer = GetSerializer(type);

            using var memoryStream = new MemoryStream();
            // This should be safe as long as our generator mentions
            // getting fields / properties of the target type. https://github.com/dotnet/runtime/issues/71350#issuecomment-1168140551
            // Not the best solution, maybe we can replace this with System.Text.Json, but the we need one generator calling the other.
#pragma warning disable IL3050 // IL3050: Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#pragma warning disable IL2026 // IL2026: Members attributed with RequiresUnreferencedCode may break when trimming
            serializer.WriteObject(memoryStream, data[i]);
#pragma warning restore IL3050 // IL3050: Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#pragma warning restore IL2026 // IL2026: Members attributed with RequiresUnreferencedCode may break when trimming
            byte[] serializerData = memoryStream.ToArray();

            serializedData[dataIndex] = Encoding.UTF8.GetString(serializerData, 0, serializerData.Length);
        }

        return serializedData;
    }

    /// <summary>
    /// Deserializes the data serialized by <see cref="Serialize(object[])" /> method.
    /// </summary>
    /// <param name="serializedData">Serialized data array to deserialize.</param>
    /// <returns>Deserialized array.</returns>
    public static object?[]? Deserialize(string?[]? serializedData)
    {
        if (serializedData == null || serializedData.Length % 2 != 0)
        {
            return null;
        }

        int length = serializedData.Length / 2;
        object?[] data = new object?[length];

        for (int i = 0; i < length; i++)
        {
            int typeIndex = i * 2;
            string? assemblyQualifiedName = serializedData[typeIndex];
            string? serializedValue = serializedData[typeIndex + 1];

            if (serializedValue == null || assemblyQualifiedName == null)
            {
                data[i] = null;
                continue;
            }

            DataContractJsonSerializer serializer = GetSerializer(assemblyQualifiedName);

            byte[] serializedDataBytes = Encoding.UTF8.GetBytes(serializedValue);
            using var memoryStream = new MemoryStream(serializedDataBytes);
            // This should be safe as long as our generator mentions
            // getting fields / properties of the target type. https://github.com/dotnet/runtime/issues/71350#issuecomment-1168140551
            // Not the best solution, maybe we can replace this with System.Text.Json, but the we need one generator calling the other.
#pragma warning disable IL3050 // IL3050: Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#pragma warning disable IL2026 // IL2026: Members attributed with RequiresUnreferencedCode may break when trimming
            data[i] = serializer.ReadObject(memoryStream);
#pragma warning restore IL3050 // IL3050: Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#pragma warning restore IL2026 // IL2026: Members attributed with RequiresUnreferencedCode may break when trimming
        }

        return data;
    }

    private static DataContractJsonSerializer GetSerializer(string assemblyQualifiedName)
        => SerializerCache.GetOrAdd(
            assemblyQualifiedName,
            // This should be safe as long as our generator mentions
            // getting fields / properties of the target type. https://github.com/dotnet/runtime/issues/71350#issuecomment-1168140551
            // Not the best solution, maybe we can replace this with System.Text.Json, but the we need one generator calling the other.
#pragma warning disable IL3050 // IL3050: Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#pragma warning disable IL2026 // IL2026: Members attributed with RequiresUnreferencedCode may break when trimming
            _ => new DataContractJsonSerializer(PlatformServiceProvider.Instance.ReflectionOperations.GetType(assemblyQualifiedName) ?? typeof(object), SerializerSettings));
#pragma warning restore IL3050 // IL3050: Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#pragma warning restore IL2026 // IL2026: Members attributed with RequiresUnreferencedCode may break when trimming

    private static DataContractJsonSerializer GetSerializer(Type type)
        => SerializerCache.GetOrAdd(
            type.AssemblyQualifiedName!,
            // This should be safe as long as our generator mentions
            // getting fields / properties of the target type. https://github.com/dotnet/runtime/issues/71350#issuecomment-1168140551
            // Not the best solution, maybe we can replace this with System.Text.Json, but the we need one generator calling the other.
#pragma warning disable IL3050 // IL3050: Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#pragma warning disable IL2026 // IL2026: Members attributed with RequiresUnreferencedCode may break when trimming
            _ => new DataContractJsonSerializer(type, SerializerSettings));
#pragma warning restore IL3050 // IL3050: Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#pragma warning restore IL2026 // IL2026: Members attributed with RequiresUnreferencedCode may break when trimming
}
