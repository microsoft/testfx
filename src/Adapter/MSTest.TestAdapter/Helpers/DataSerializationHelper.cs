// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.CodeDom;
using System.Collections.ObjectModel;
#endif
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
#if NETFRAMEWORK
        DataContractSurrogate = SerializationSurrogateProvider.Instance,
#endif
        KnownTypes = [typeof(SurrogatedDateOnly), typeof(SurrogatedTimeOnly)],
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
            // For some reason, we don't get SerializationSurrogateProvider.GetDeserializedObject to be called by .NET runtime.
            // So we manually call it.
            data[i] = SerializationSurrogateProvider.GetDeserializedObject(data[i]!);
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

    [DataContract]
    private sealed class SurrogatedDateOnly
    {
        [DataMember]
        public int DayNumber { get; set; }
    }

    [DataContract]
    private sealed class SurrogatedTimeOnly
    {
        [DataMember]
        public long Ticks { get; set; }
    }

    private sealed class SerializationSurrogateProvider
#if NETFRAMEWORK
        : IDataContractSurrogate
#else
        : ISerializationSurrogateProvider
#endif
    {
        public static SerializationSurrogateProvider Instance { get; } = new();

#if NETFRAMEWORK
        public object GetCustomDataToExport(MemberInfo memberInfo, Type dataContractType) => null!;

        public object GetCustomDataToExport(Type clrType, Type dataContractType) => null!;

        public void GetKnownCustomDataTypes(Collection<Type> customDataTypes)
        {
        }

        public Type GetReferencedTypeOnImport(string typeName, string typeNamespace, object customData) => null!;

        public CodeTypeDeclaration ProcessImportedType(CodeTypeDeclaration typeDeclaration, CodeCompileUnit compileUnit) => typeDeclaration;
#endif

        public object GetDeserializedObject(object obj, Type targetType)
            => GetDeserializedObject(obj);

        internal static object GetDeserializedObject(object obj)
        {
#if NET6_0_OR_GREATER
            if (obj is SurrogatedDateOnly surrogatedDateOnly)
            {
                return DateOnly.FromDayNumber(surrogatedDateOnly.DayNumber);
            }
            else if (obj is SurrogatedTimeOnly surrogatedTimeOnly)
            {
                return new TimeOnly(surrogatedTimeOnly.Ticks);
            }
#endif

            return obj;
        }

        public object GetObjectToSerialize(object obj, Type targetType)
            => obj switch
            {
#if NET6_0_OR_GREATER
                DateOnly dateOnly => new SurrogatedDateOnly() { DayNumber = dateOnly.DayNumber },
                TimeOnly timeOnly => new SurrogatedTimeOnly() { Ticks = timeOnly.Ticks },
#endif
                _ => obj,
            };

#if NETFRAMEWORK
        public Type GetDataContractType(Type type)
#else
        public Type GetSurrogateType(Type type)
#endif
        {
#if NET6_0_OR_GREATER
            if (type == typeof(DateOnly))
            {
                return typeof(SurrogatedDateOnly);
            }
            else if (type == typeof(TimeOnly))
            {
                return typeof(SurrogatedTimeOnly);
            }
#endif

            return type;
        }
    }
#pragma warning restore IL3050 // IL3050: Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT
#pragma warning restore IL2026 // IL2026: Members attributed with RequiresUnreferencedCode may break when trimming
}
