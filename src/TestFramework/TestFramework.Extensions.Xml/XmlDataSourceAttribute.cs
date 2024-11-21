// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Data;
using System.Globalization;
using System.Reflection;
using System.Xml;

using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Attribute to define dynamic data from an XML file for a test method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class XmlDataSourceAttribute : Attribute, ITestDataSource
{
    public XmlDataSourceAttribute(string fileName, string tableName)
    {
        FileName = fileName;
        TableName = tableName;
    }

    internal string FileName { get; }

    internal string TableName { get; }

    IEnumerable<object?[]> ITestDataSource.GetData(MethodInfo methodInfo)
    {
        string fullPath = Path.GetFullPath(FileName);
        if (!File.Exists(fullPath))
        {
            // TODO: Localize.
            throw new FileNotFoundException($"Xml file '{fullPath}' cannot be found.", fullPath);
        }

        DataSet dataSet = new()
        {
            Locale = CultureInfo.CurrentCulture,
        };

        // ReadXml should use the overload with XmlReader to avoid DTD processing
        dataSet.ReadXml(new XmlTextReader(fullPath));

        DataTable table = dataSet.Tables[TableName];

        object?[][] dataRows = new object?[table.Rows.Count][];
        for (int i = 0; i < dataRows.Length; i++)
        {
            dataRows[i] = [table.Rows[i]];
        }

        return dataRows;
    }

    string? ITestDataSource.GetDisplayName(MethodInfo methodInfo, object?[]? data)
        => TestDataSourceUtilities.ComputeDefaultDisplayName(methodInfo, data, DynamicDataAttribute.TestIdGenerationStrategy);
}
