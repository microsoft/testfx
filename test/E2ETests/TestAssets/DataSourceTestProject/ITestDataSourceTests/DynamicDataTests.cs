// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DataSourceTestProject.ITestDataSourceTests;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;

[TestClass]
public class DynamicDataTests
{
    [DataTestMethod()]
    [DynamicData(nameof(GetParseUserDatas), DynamicDataSourceType.Method)]
    public void DynamicDataTest(string userDatas, User expectedUser)
    {
        // Prepare
        var srv = new UserService();

        // Act
        var user = srv.ParseUserDatas(userDatas);

        // Assert
        Assert.AreNotSame(user, expectedUser);
        Assert.AreEqual(user.FirstName, expectedUser.FirstName);
        Assert.AreEqual(user.LastName, expectedUser.LastName);
    }

    public static IEnumerable<object[]> GetParseUserDatas()
    {
        yield return new object[] {
            "John;Doe",
            new User()
            {
                FirstName = "John",
                LastName = "Doe"
            }
        };
    }
}
