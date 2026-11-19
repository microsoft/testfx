// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

// Copy from https://github.com/microsoft/testfx/blob/b769496b8992bf8f51e000f7a5626b5ec6bb3d27/test/Utilities/Microsoft.Testing.TestInfrastructure/RandomId.cs
using System.Security.Cryptography;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Slightly random id that is just good enough for creating distinct directories for each test.
/// </summary>
internal static class RandomId
{
    private const string Pool = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

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
                    Rng.GetNonZeroBytes(bytes);
                    poolIndex = bytes[0];
                }

                id[idIndex] = Pool[poolIndex];
            }
        }

        return new string(id);
    }
}
