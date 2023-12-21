// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SampleProjectForAssemblyResolution;

[Serializable]
public class SerializableTypeThatShouldBeLoaded : MarshalByRefObject
{
    public void SomeMethod()
    {
    }
}
