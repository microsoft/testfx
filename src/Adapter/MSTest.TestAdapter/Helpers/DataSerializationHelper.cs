// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class DataSerializationHelper
{
    private static readonly ConcurrentDictionary<string, DataContractJsonSerializer> SerializerCache = new();
    private static readonly DataContractJsonSerializerSettings SerializerSettings = new()
    {
        UseSimpleDictionaryFormat = true,
        EmitTypeInformation = System.Runtime.Serialization.EmitTypeInformation.Always,
        DateTimeFormat = new System.Runtime.Serialization.DateTimeFormat("O", CultureInfo.InvariantCulture),
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
#if NET7_0_OR_GREATER
            serializer.SetSerializationSurrogateProvider(SerializationSurrogateProvider.Instance);
#elif NETFRAMEWORK
            // TODO: Use serializer.DataContractSurrogate.
#endif

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
#if NET7_0_OR_GREATER
            serializer.SetSerializationSurrogateProvider(SerializationSurrogateProvider.Instance);
#elif NETFRAMEWORK
            // TODO: Use serializer.DataContractSurrogate.
#endif

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

    private sealed class SerializationSurrogateProvider : ISerializationSurrogateProvider
    {
        public static SerializationSurrogateProvider Instance { get; } = new();

        public object GetDeserializedObject(object obj, Type targetType)
        {
#if NET6_0_OR_GREATER
            if (targetType == typeof(DateOnly))
            {
                return DateOnly.FromDayNumber(((DateOnly)obj).DayNumber);
            }
            else if (targetType == typeof(TimeOnly))
            {
                return new TimeOnly(((TimeOnly)obj).Ticks);
            }
#endif

            return obj;
        }

        public object GetObjectToSerialize(object obj, Type targetType)
            => obj switch
            {
#if NET6_0_OR_GREATER
                DateOnly dateOnly => dateOnly.DayNumber,
                TimeOnly timeOnly => timeOnly.Ticks,
#endif
                _ => obj,
            };

        public Type GetSurrogateType(Type type)
        {
#if NET6_0_OR_GREATER
            if (type == typeof(DateOnly))
            {
                return typeof(int);
            }
            else if (type == typeof(TimeOnly))
            {
                return typeof(long);
            }
#endif

            return type;
        }
    }
#pragma warning restore IL3050 // IL3050: Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#pragma warning restore IL2026 // IL2026: Members attributed with RequiresUnreferencedCode may break when trimming
}
