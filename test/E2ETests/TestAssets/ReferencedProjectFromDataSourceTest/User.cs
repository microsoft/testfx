// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DataSourceTestProject.ITestDataSourceTests
{
    public class User
    {
        public string LastName { get; set; }

        public string FirstName { get; set; }
    }

    public class UserService
    {
        public User ParseUserDatas(string datas)
        {
            var splittedDatas = datas.Split(';');

            return new User()
            {
                FirstName = splittedDatas[0],
                LastName = splittedDatas[1]
            };
        }
    }
}
