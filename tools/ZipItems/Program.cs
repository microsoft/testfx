// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ZipItems
{
    class Program
    {
        public static XmlReaderSettings ReaderSettings
        {
            get
            {
                var settings = new XmlReaderSettings();
                settings.IgnoreComments = true;
                settings.IgnoreWhitespace = true;
                return settings;
            }
        }
        static void Main(string[] args)
        {
            XmlReader reader = XmlReader.Create(args[0], ReaderSettings);
            reader.Read();
            reader.Read();
            reader.Read();

            string baseDirectory = args[1];
            ZipArchive zip = ZipFile.Open(args[2], ZipArchiveMode.Create);

            while (reader.NodeType == XmlNodeType.Element)
            {
                string elementName = reader.Name.ToUpperInvariant();
                switch (elementName)
                {
                    case "FILE":
                        {
                            string targetFilename = reader.GetAttribute("TargetFileName");
                            string sourceFilePath = reader.ReadInnerXml();
                            string sourceFile = Path.Combine(baseDirectory, sourceFilePath);
                            zip.CreateEntryFromFile(sourceFile, targetFilename, CompressionLevel.Optimal);
                        }
                        break;
                    default:
                        reader.Skip();
                        break;

                }
            }
            zip.Dispose();
        }
    }
}
