// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// Internal class to indicate type inspection failure.
/// </summary>
[Serializable]
internal sealed class TypeInspectionException : Exception
{
    public TypeInspectionException()
        : base()
    {
    }

    public TypeInspectionException(string? message)
        : base(message)
    {
    }

    public TypeInspectionException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
