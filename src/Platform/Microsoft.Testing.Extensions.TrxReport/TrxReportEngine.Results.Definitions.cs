// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    private static XElement CreateUnitTestElementForTestDefinition(string testDefinitionName, string testAppModule, string id, TrxTestResult testResult, string executionId)
    {
        var unitTest = new XElement(
            "UnitTest",
            new XAttribute("name", testDefinitionName),
            new XAttribute("storage", testAppModule.ToLowerInvariant()),
            new XAttribute("id", id));

        if (testResult.Categories is { Count: > 0 } categories)
        {
            unitTest.Add(new XElement("TestCategory", categories.Select(c => new XElement("TestCategoryItem", new XAttribute("TestCategory", c)))));
        }

        unitTest.Add(new XElement("Execution", new XAttribute("id", executionId)));

        XElement? properties = null;
        XElement? owners = null;
        XElement? description = null;
        foreach (TrxTestMetadata property in testResult.Metadata ?? [])
        {
            switch (property.Key)
            {
                case "Owner":
                    owners ??= new XElement("Owners", new XElement("Owner", new XAttribute("name", property.Value)));
                    break;

                case "Priority":
                    // 2147483647 (int.MaxValue) is already the default priority.
                    if (int.TryParse(property.Value, out int priorityValue) && priorityValue != int.MaxValue)
                    {
                        unitTest.SetAttributeValue("priority", property.Value);
                    }

                    break;

                case "Description":
                    description ??= new XElement("Description", property.Value);
                    break;

                default:
                    // NOTE: VSTest doesn't produce Properties as of writing this.
                    // It was historically fixed, but the fix wasn't correct and the fix was reverted and never revisited to be properly fixed.
                    // Revert PR: https://github.com/microsoft/vstest/pull/15080
                    // The original implementation (buggy) was setting "Key" and "Value" as attributes on "Property" element.
                    // However, Visual Studio will validate the TRX file against vstst.xsd file in
                    //  C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Xml\Schemas\vstst.xsd
                    // In xsd, "Properties" element is defined as:
                    // <xs:element name="Properties" minOccurs="0">
                    //   <xs:complexType>
                    //     <xs:sequence>
                    //       <xs:element name="Property" minOccurs="0" maxOccurs="unbounded">
                    //         <xs:complexType>
                    //           <xs:sequence>
                    //             <xs:element name="Key" />
                    //             <xs:element name="Value" />
                    //           </xs:sequence>
                    //         </xs:complexType>
                    //       </xs:element>
                    //     </xs:sequence>
                    //   </xs:complexType>
                    // </xs:element>
                    // So, Key and Value are **elements**, not attributes.
                    // In MTP, we do the right thing and follow the XSD definition.
                    properties ??= new XElement("Properties");
                    properties.Add(new XElement(
                        "Property",
                        new XElement("Key", property.Key), new XElement("Value", property.Value)));
                    break;
            }
        }

        if (owners is not null)
        {
            unitTest.Add(owners);
        }

        if (description is not null)
        {
            unitTest.Add(description);
        }

        if (properties is not null)
        {
            unitTest.Add(properties);
        }

        // TODO: We are not adding Workitems, but VSTest does.
        return unitTest;
    }
}
