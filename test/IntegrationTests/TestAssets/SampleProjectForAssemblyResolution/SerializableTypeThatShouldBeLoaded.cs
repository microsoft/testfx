using System;

namespace SampleProjectForAssemblyResolution;

[Serializable]
public class SerializableTypeThatShouldBeLoaded : MarshalByRefObject
{
    public void SomeMethod()
    {
    }
}
