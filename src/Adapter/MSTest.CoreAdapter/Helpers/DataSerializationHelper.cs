// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization.Json;
    using System.Text;

    internal static class DataSerializationHelper
    {
        private static readonly Dictionary<Type, DataContractJsonSerializer> SerializerCache = new Dictionary<Type, DataContractJsonSerializer>();
        private static readonly DataContractJsonSerializerSettings SerializerSettings = new DataContractJsonSerializerSettings()
        {
            UseSimpleDictionaryFormat = true,
            EmitTypeInformation = System.Runtime.Serialization.EmitTypeInformation.Always
        };

        /// <summary>
        /// Serializes the date in such a way that won't throw exceptions during deserialization in Test Platform.
        /// The result can be deserialized using <see cref="Deserialize(string[])"/> method.
        /// </summary>
        /// <param name="data">Data array to serialize.</param>
        /// <returns>Serialzed array.</returns>
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
                var typeName = type.FullName;

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
        /// Deserialzes the data serialzed by <see cref="Serialize(object[])" /> method.
        /// </summary>
        /// <param name="serializedData">Serialized data array to deserialize.</param>
        /// <param name="assemblies">Assemblies that serialized types defined in.</param>
        /// <returns>Deserialized array.</returns>
        public static object[] Deserialize(string[] serializedData, params Assembly[] assemblies)
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
                var typeName = serializedData[typeIndex];
                var serializedValue = serializedData[typeIndex + 1];

                if (serializedValue == null || typeName == null)
                {
                    data[i] = null;
                    continue;
                }

                var serializer = GetSerializer(typeName, assemblies);

                var serialzedDataBytes = Encoding.UTF8.GetBytes(serializedValue);
                using (var memoryStream = new MemoryStream(serialzedDataBytes))
                {
                    data[i] = serializer.ReadObject(memoryStream);
                }
            }

            return data;
        }

        private static DataContractJsonSerializer GetSerializer(string typeName, Assembly[] assemblies)
        {
            var serializer = SerializerCache.SingleOrDefault(i => i.Key.FullName == typeName);
            if (serializer.Value != null)
            {
                return serializer.Value;
            }

            var type = Type.GetType(typeName);
            if (type != null)
            {
                return GetSerializer(type);
            }

            if (assemblies != null)
            {
                foreach (var assembly in assemblies)
                {
                    type = assembly.GetType(typeName);
                    if (type != null)
                    {
                        return GetSerializer(type);
                    }
                }
            }

            return GetSerializer(typeof(object));
        }

        private static DataContractJsonSerializer GetSerializer(Type type)
        {
            if (SerializerCache.ContainsKey(type))
            {
                return SerializerCache[type];
            }

            return SerializerCache[type] = new DataContractJsonSerializer(type, SerializerSettings);
        }
    }
}
