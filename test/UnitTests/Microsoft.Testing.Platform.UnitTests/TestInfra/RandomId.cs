// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace Microsoft.Testing.TestInfrastructure;

/// <summary>
/// Slightly random id that is just good enough for creating disctinct directories for each test.
/// </summary>
public static class RandomId
{
    private const string Pool = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable SA1311 // Static readonly fields should begin with upper-case letter
    private static readonly RandomNumberGenerator s_rng = RandomNumberGenerator.Create();
#pragma warning restore SA1311 // Static readonly fields should begin with upper-case letter
#pragma warning restore IDE1006 // Naming Styles

    /// <summary>
    /// 5 character long id from 0-9A-Za-z0, for example fUfko, A6uvM, sOMXa, RY1ei, KvdJZ.
    /// </summary>
    public static string Next() => Next(5);

    private static string Next(int length)
    {
        int poolLength = Pool.Length;
        char[] id = new char[length];
        lock (Pool)
        {
            for (int idIndex = 0; idIndex < length; idIndex++)
            {
                int poolIndex = poolLength + 1;
                while (poolIndex >= poolLength)
                {
                    byte[] bytes = new byte[1];
                    s_rng.GetNonZeroBytes(bytes);
                    poolIndex = bytes[0];
                }

                id[idIndex] = Pool[poolIndex];
            }
        }

        return new string(id);
    }
}
