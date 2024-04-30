// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LibProjectReferencedByDataSourceTest;

public class User
{
    public string LastName { get; set; }

    public string FirstName { get; set; }
}

public class UserService
{
    public User ParseUserData(string data)
    {
        string[] splitData = data.Split(';');

        return new User()
        {
            FirstName = splitData[0],
            LastName = splitData[1],
        };
    }
}
