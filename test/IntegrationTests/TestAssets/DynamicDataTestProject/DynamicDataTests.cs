// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using DynamicDataTestProject;

using LibProjectReferencedByDataSourceTest;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataSourceTestProject;

[TestClass]
public class DynamicDataTests
{
    [DataTestMethod]
    [DynamicData(nameof(GetParseUserData), DynamicDataSourceType.Method)]
    public void DynamicDataTest_SourceMethod(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(ParseUserData))]
    public void DynamicDataTest_SourceProperty(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(GetParseUserData), DynamicDataSourceType.Method,
        DynamicDataDisplayName = nameof(GetCustomDynamicDataDisplayName))]
    public void DynamicDataTest_SourceMethod_CustomDisplayName(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(ParseUserData), DynamicDataDisplayName = nameof(GetCustomDynamicDataDisplayName))]
    public void DynamicDataTest_SourceProperty_CustomDisplayName(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(GetParseUserData), DynamicDataSourceType.Method,
        DynamicDataDisplayName = nameof(DataProvider.GetUserDynamicDataDisplayName), DynamicDataDisplayNameDeclaringType = typeof(DataProvider))]
    public void DynamicDataTest_SourceMethod_CustomDisplayNameOtherType(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser); // todo

    [DataTestMethod]
    [DynamicData(nameof(ParseUserData), DynamicDataDisplayName = nameof(DataProvider.GetUserDynamicDataDisplayName), DynamicDataDisplayNameDeclaringType = typeof(DataProvider))]
    public void DynamicDataTest_SourceProperty_CustomDisplayNameOtherType(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser); // todo

    [DataTestMethod]
    [DynamicData(nameof(DataProvider.GetUserDataAndExceptedParsedUser), typeof(DataProvider), DynamicDataSourceType.Method)]
    public void DynamicDataTest_SourceMethodOtherType(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(DataProvider.UserDataAndExceptedParsedUser), typeof(DataProvider))]
    public void DynamicDataTest_SourcePropertyOtherType(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(DataProvider.GetUserDataAndExceptedParsedUser), typeof(DataProvider), DynamicDataSourceType.Method,
        DynamicDataDisplayName = nameof(GetCustomDynamicDataDisplayName))]
    public void DynamicDataTest_SourceMethodOtherType_CustomDisplayName(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(DataProvider.UserDataAndExceptedParsedUser), typeof(DataProvider),
        DynamicDataDisplayName = nameof(GetCustomDynamicDataDisplayName))]
    public void DynamicDataTest_SourcePropertyOtherType_CustomDisplayName(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(DataProvider.GetUserDataAndExceptedParsedUser), typeof(DataProvider), DynamicDataSourceType.Method,
        DynamicDataDisplayName = nameof(DataProvider.GetUserDynamicDataDisplayName), DynamicDataDisplayNameDeclaringType = typeof(DataProvider))]
    public void DynamicDataTest_SourceMethodOtherType_CustomDisplayNameOtherType(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(DataProvider.UserDataAndExceptedParsedUser), typeof(DataProvider),
        DynamicDataDisplayName = nameof(DataProvider.GetUserDynamicDataDisplayName), DynamicDataDisplayNameDeclaringType = typeof(DataProvider))]
    public void DynamicDataTest_SourcePropertyOtherType_CustomDisplayNameOtherType(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [TestCategory("DynamicDataWithCategory")]
    [DataTestMethod]
    [DynamicData(nameof(GetParseUserData), DynamicDataSourceType.Method)]
    public void DynamicDataTestWithTestCategory(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod] // See https://github.com/microsoft/testfx/issues/1050
    [DynamicData(nameof(GetExampleTestCases), DynamicDataSourceType.Method)]
    public void StackOverflowException_Example(ExampleTestCase exampleTestCase) => Assert.IsNotNull(exampleTestCase.Example);

    [DataTestMethod]
    [DynamicData(nameof(StringAndInt32), DynamicDataSourceType.Method)]
    public void MethodWithOverload(string x, int y)
    {
    }

    [DataTestMethod]
    [DynamicData(nameof(Int32AndString), DynamicDataSourceType.Method)]
    public void MethodWithOverload(int x, string y)
    {
    }

    private static void ParseAndAssert(string userData, User expectedUser)
    {
        // Prepare
        var service = new UserService();

        // Act
        User user = service.ParseUserData(userData);

        // Assert
        Assert.AreNotSame(user, expectedUser);
        Assert.AreEqual(user.FirstName, expectedUser.FirstName);
        Assert.AreEqual(user.LastName, expectedUser.LastName);
    }

    public static IEnumerable<object[]> GetParseUserData()
    {
        yield return
        [
            "John;Doe",
            new User()
            {
                FirstName = "John",
                LastName = "Doe",
            }
        ];

        yield return
        [
            "Jane;Doe",
            new User()
            {
                FirstName = "Jane",
                LastName = "Doe",
            }
        ];
    }

    public static IEnumerable<object[]> ParseUserData
    {
        get
        {
            yield return
            [
                "John;Doe",
                new User()
                {
                    FirstName = "John",
                    LastName = "Doe",
                }
            ];

            yield return
            [
                "Jane;Doe",
                new User()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                }
            ];
        }
    }

    public static string GetCustomDynamicDataDisplayName(MethodInfo methodInfo, object[] data)
        => $"Custom DynamicDataTestMethod {methodInfo.Name} with {data.Length} parameters";

    private static IEnumerable<object[]> GetExampleTestCases()
    {
        string json =
            """
            [
              {
                "TestCaseName": "Example test.",
                "Example": {
                  "jTokenDictionary" : {
                    "names": [
                      "Jane",
                      "John"
                    ]
                  }
                }
              }
            ]
            """;
        return JsonConvert.DeserializeObject<ExampleTestCase[]>(json)
            .Select(testCase => new object[] { testCase });
    }

    public class ExampleClass
    {
        [JsonProperty("jTokenDictionary")]
        public IDictionary<string, JToken> JTokenDictionary { get; set; }
    }

    public class ExampleTestCase
    {
        public string TestCaseName { get; set; }

        public ExampleClass Example { get; set; }
    }

    private static IEnumerable<object[]> StringAndInt32()
    {
        yield return new object[] { "1", 1 };
        yield return new object[] { "2", 1 };
    }

    private static IEnumerable<object[]> Int32AndString()
    {
        yield return new object[] { 1, "0" };
        yield return new object[] { 2, "2" };
    }
}
