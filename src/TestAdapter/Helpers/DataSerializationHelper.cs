// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Runtime.Serialization.Json;
    using System.Text;

    internal static class DataSerializationHelper
    {
        private static readonly ConcurrentDictionary<string, DataContractJsonSerializer> SerializerCache = new ConcurrentDictionary<string, DataContractJsonSerializer>();
        private static readonly DataContractJsonSerializerSettings SerializerSettings = new DataContractJsonSerializerSettings()
        {
            UseSimpleDictionaryFormat = true,
            EmitTypeInformation = System.Runtime.Serialization.EmitTypeInformation.Always,
            DateTimeFormat = new System.Runtime.Serialization.DateTimeFormat("O", System.Globalization.CultureInfo.InvariantCulture)
        };

        /// <summary>
        /// Serializes the date in such a way that won't throw exceptions during deserialization in Test Platform.
        /// The result can be deserialized using <see cref="Deserialize(string[])"/> method.
        /// </summary>
        /// <param name="data">Data array to serialize.</param>
        /// <returns>Serialized array.</returns>
        public static string[] Serialize(object[] data)
        {
            if (data == null)
            {
                return null;
            }

            var serializedData = new string[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                var typeIndex = i * 2;
                var dataIndex = typeIndex + 1;
                if (data[i] == null)
                {
                    serializedData[typeIndex] = null;
                    serializedData[dataIndex] = null;

                    continue;
                }

                var type = data[i].GetType();
                var typeName = type.AssemblyQualifiedName;

                serializedData[typeIndex] = typeName;

                var serializer = GetSerializer(type);

                using (var memoryStream = new MemoryStream())
                {
                    serializer.WriteObject(memoryStream, data[i]);
                    var serializerData = memoryStream.ToArray();

                    serializedData[dataIndex] = Encoding.UTF8.GetString(serializerData, 0, serializerData.Length);
                }
            }

            return serializedData;
        }

        /// <summary>
        /// Deserializes the data serialized by <see cref="Serialize(object[])" /> method.
        /// </summary>
        /// <param name="serializedData">Serialized data array to deserialize.</param>
        /// <returns>Deserialized array.</returns>
        public static object[] Deserialize(string[] serializedData)
        {
            if (serializedData == null || serializedData.Length % 2 != 0)
            {
                return null;
            }

            var length = serializedData.Length / 2;
            var data = new object[length];

            for (int i = 0; i < length; i++)
            {
                var typeIndex = i * 2;
                var assemblyQualifiedName = serializedData[typeIndex];
                var serializedValue = serializedData[typeIndex + 1];

                if (serializedValue == null || assemblyQualifiedName == null)
                {
                    data[i] = null;
                    continue;
                }

                var serializer = GetSerializer(assemblyQualifiedName);

                var serialzedDataBytes = Encoding.UTF8.GetBytes(serializedValue);
                using (var memoryStream = new MemoryStream(serialzedDataBytes))
                {
                    data[i] = serializer.ReadObject(memoryStream);
                }
            }

            return data;
        }

        private static DataContractJsonSerializer GetSerializer(string assemblyQualifiedName)
        {
            return SerializerCache.GetOrAdd(
                assemblyQualifiedName,
                _ => new DataContractJsonSerializer(Type.GetType(assemblyQualifiedName) ?? typeof(object), SerializerSettings));
        }

        private static DataContractJsonSerializer GetSerializer(Type type)
        {
            return SerializerCache.GetOrAdd(
                type.AssemblyQualifiedName,
                _ => new DataContractJsonSerializer(type, SerializerSettings));
        }
    }
}
