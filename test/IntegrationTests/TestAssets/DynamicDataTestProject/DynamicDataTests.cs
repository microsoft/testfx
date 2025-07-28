// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DynamicDataTestProject;

using LibProjectReferencedByDataSourceTest;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataSourceTestProject;

public abstract class DynamicDataTestsBase
{
    public static IEnumerable<object[]> GetDataFromBase()
    {
        yield return
        [
            "John;Doe",
            new User
            {
                FirstName = "John",
                LastName = "Doe",
            }
        ];

        yield return
        [
            "Jane;Doe",
            new User
            {
                FirstName = "Jane",
                LastName = "Doe",
            }
        ];
    }

    public static IEnumerable<object[]> DataFromBase
    {
        get
        {
            yield return
            [
                "John;Doe",
                new User
                {
                    FirstName = "John",
                    LastName = "Doe",
                }
            ];

            yield return
            [
                "Jane;Doe",
                new User
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                }
            ];
        }
    }

    public static IEnumerable<object[]> DataShadowingBase => throw new NotImplementedException();

    public static IEnumerable<object[]> GetDataShadowingBase() => throw new NotImplementedException();
}

[TestClass]
public class DynamicDataTests : DynamicDataTestsBase
{
    [DataTestMethod]
    [DynamicData(nameof(GetParseUserData), DynamicDataSourceType.Method)]
    public void DynamicDataTest_SourceMethod(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(GetDataFromBase), DynamicDataSourceType.Method)]
    public void DynamicDataTest_SourceMethodFromBase(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(GetDataShadowingBase), DynamicDataSourceType.Method)]
    public void DynamicDataTest_SourceMethodShadowingBase(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(GetParseUserData))]
    public void DynamicDataTest_SourceMethodAuto(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(GetDataFromBase))]
    public void DynamicDataTest_SourceMethodAutoFromBase(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(GetDataShadowingBase))]
    public void DynamicDataTest_SourceMethodAutoShadowingBase(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(ParseUserData), DynamicDataSourceType.Property)]
    public void DynamicDataTest_SourceProperty(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(DataFromBase), DynamicDataSourceType.Property)]
    public void DynamicDataTest_SourcePropertyFromBase(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(DataShadowingBase), DynamicDataSourceType.Property)]
    public void DynamicDataTest_SourcePropertyShadowingBase(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(ParseUserData))]
    public void DynamicDataTest_SourcePropertyAuto(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(DataFromBase))]
    public void DynamicDataTest_SourcePropertyAutoFromBase(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

    [DataTestMethod]
    [DynamicData(nameof(DataShadowingBase))]
    public void DynamicDataTest_SourcePropertyAutoShadowingBase(string userData, User expectedUser) => ParseAndAssert(userData, expectedUser);

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

    [TestMethod]
    [DynamicData(nameof(SimpleCollection))]
    public void DynamicDataTest_SimpleCollection(int value)
        => Assert.AreEqual(0, value % 2);

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

    public static new IEnumerable<object[]> GetDataShadowingBase() => GetDataFromBase();

    public static IEnumerable<object[]> GetParseUserData() => GetDataFromBase();

    public static IEnumerable<object[]> ParseUserData => DataFromBase;

    public static new IEnumerable<object[]> DataShadowingBase => DataFromBase;

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
        public IDictionary<string, JToken> JTokenDictionary { get; set; } = null!;
    }

    public class ExampleTestCase
    {
        public string TestCaseName { get; set; } = null!;

        public ExampleClass Example { get; set; } = null!;
    }

    private static IEnumerable<object[]> StringAndInt32()
    {
        yield return ["1", 1];
        yield return ["2", 1];
    }

    private static IEnumerable<object[]> Int32AndString()
    {
        yield return [1, "0"];
        yield return [2, "2"];
    }

    private static IEnumerable<int> SimpleCollection
    {
        get
        {
            yield return 0;
            yield return 2;
            yield return 4;
        }
    }

    // Test field support - static field for dynamic data
    private static readonly IEnumerable<object[]> FieldTestData = new[]
    {
        ["field", 5],
        new object[] { "test", 4 },
    };

    [DataTestMethod]
    [DynamicData(nameof(FieldTestData), DynamicDataSourceType.Field)]
    public void DynamicDataTest_SourceFieldExplicit(string text, int expectedLength)
        => Assert.AreEqual(expectedLength, text.Length);

    [DataTestMethod]
    [DynamicData(nameof(FieldTestData))] // AutoDetect should find the field
    public void DynamicDataTest_SourceFieldAutoDetect(string text, int expectedLength)
        => Assert.AreEqual(expectedLength, text.Length);
}
