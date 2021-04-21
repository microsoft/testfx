// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers
{
    using System.IO;
    using System.Runtime.Serialization.Json;
    using System.Text;

    internal static class DataSerializationHelper
    {
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

            var serializer = GetSerializer();
            var serializedData = new string[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == null)
                {
                    serializedData[i] = null;
                    continue;
                }

                using (var memoryStream = new MemoryStream())
                {
                    serializer.WriteObject(memoryStream, data[i]);
                    var serializerData = memoryStream.ToArray();

                    serializedData[i] = Encoding.UTF8.GetString(serializerData, 0, serializerData.Length);
                }
            }

            return serializedData;
        }

        /// <summary>
        /// Deserialzes the data serialzed by <see cref="Serialize(object[])" /> method.
        /// </summary>
        /// <param name="serializedData">Serialized data array to deserialize.</param>
        /// <returns>Deserialized array.</returns>
        public static object[] Deserialize(string[] serializedData)
        {
            if (serializedData == null)
            {
                return null;
            }

            var serializer = GetSerializer();
            var data = new object[serializedData.Length];

            for (int i = 0; i < serializedData.Length; i++)
            {
                if (serializedData[i] == null)
                {
                    data[i] = null;
                    continue;
                }

                var serialzedDataBytes = Encoding.UTF8.GetBytes(serializedData[i]);
                using (var memoryStream = new MemoryStream(serialzedDataBytes))
                {
                    data[i] = serializer.ReadObject(memoryStream);
                }
            }

            return data;
        }

        private static DataContractJsonSerializer GetSerializer()
        {
            var settings = new DataContractJsonSerializerSettings()
            {
                UseSimpleDictionaryFormat = true,
                EmitTypeInformation = System.Runtime.Serialization.EmitTypeInformation.Always
            };

            var serializer = new DataContractJsonSerializer(typeof(object), settings);

            return serializer;
        }
    }
}
