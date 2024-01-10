// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using LibProjectReferencedByDataSourceTest;

namespace DynamicDataTestProject;

public class DataProvider
{
    public static IEnumerable<object[]> GetUserDataAndExceptedParsedUser()
    {
        yield return new object[]
        {
            "John;Doe",
            new User()
            {
                FirstName = "John",
                LastName = "Doe",
            },
        };

        yield return new object[]
        {
            "Jane;Doe",
            new User()
            {
                FirstName = "Jane",
                LastName = "Doe",
            },
        };
    }

    public static IEnumerable<object[]> UserDataAndExceptedParsedUser
    {
        get
        {
            yield return new object[]
            {
                "John;Doe",
                new User()
                {
                    FirstName = "John",
                    LastName = "Doe",
                },
            };

            yield return new object[]
            {
                "Jane;Doe",
                new User()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                },
            };
        }
    }

    public static string GetUserDynamicDataDisplayName(MethodInfo methodInfo, object[] data)
        => $"UserDynamicDataTestMethod {methodInfo.Name} with {data.Length} parameters";
}
