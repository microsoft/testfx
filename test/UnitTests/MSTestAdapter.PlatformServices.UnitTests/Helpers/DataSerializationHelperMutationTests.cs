// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class DataSerializationHelperMutationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;

    public DataSerializationHelperMutationTests()
    {
        PlatformServiceProvider.Instance = new TestablePlatformServiceProvider();
    }

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void Deserialize_ResolvesTheTypeFromItsAssemblyQualifiedNameWhenNoSerializerIsCachedYet()
    {
        string assemblyQualifiedName = typeof(UncachedPayload).AssemblyQualifiedName!;
        ConcurrentDictionary<string, DataContractJsonSerializer> cache = GetSerializerCache();

        try
        {
            string?[]? serialized = DataSerializationHelper.Serialize([new UncachedPayload { Count = 3, Name = "probe" }]);
            cache.TryRemove(assemblyQualifiedName, out _);

            object?[]? actual = DataSerializationHelper.Deserialize(serialized);

            actual.Should().NotBeNull(because: "an even-length payload is deserialized");
            actual!.Length.Should().Be(1, because: "one value was serialized");
            UncachedPayload payload = actual[0].Should().BeOfType<UncachedPayload>(because: "the serializer is created for the type named in the payload, not for object").Which;
            payload.Count.Should().Be(3, because: "the numeric member is read back");
            payload.Name.Should().Be("probe", because: "the string member is read back");
        }
        finally
        {
            cache.TryRemove(assemblyQualifiedName, out _);
        }
    }

    // The simple dictionary format writes a dictionary as a plain JSON object; the data contract default is an array of Key/Value pairs.
    public void Serialize_WritesADictionaryAsAPlainJsonObject()
    {
        var dictionary = new Dictionary<string, int> { ["answer"] = 42 };

        string?[]? serialized = DataSerializationHelper.Serialize([dictionary]);

        serialized.Should().Equal(typeof(Dictionary<string, int>).AssemblyQualifiedName, "{\"answer\":42}");
        object?[]? deserialized = DataSerializationHelper.Deserialize(serialized);
        deserialized.Should().ContainSingle().Which.Should().BeOfType<Dictionary<string, int>>().Which.Should().Equal(dictionary);
    }

#if NET6_0_OR_GREATER
    public void SurrogateProvider_MapsOnlyDateOnlyAndTimeOnlyToTheirSurrogateDataContracts()
    {
        ISerializationSurrogateProvider provider = GetSurrogateProvider();

        provider.GetSurrogateType(typeof(DateOnly)).Should().Be(GetNestedType("SurrogatedDateOnly"), because: "a DateOnly is replaced by its day-number data contract");
        provider.GetSurrogateType(typeof(TimeOnly)).Should().Be(GetNestedType("SurrogatedTimeOnly"), because: "a TimeOnly is replaced by its ticks data contract");
        provider.GetSurrogateType(typeof(string)).Should().Be(typeof(string), because: "other types are not replaced");
        provider.GetSurrogateType(typeof(DateTime)).Should().Be(typeof(DateTime), because: "DateTime is not replaced either");
    }

    public void SurrogateProvider_ReplacesDateOnlyAndTimeOnlyByDataContractsCarryingTheirValues()
    {
        ISerializationSurrogateProvider provider = GetSurrogateProvider();
        var date = new DateOnly(2024, 5, 17);
        var time = new TimeOnly(13, 45, 30);
        const string other = "not a date";

        object dateSurrogate = provider.GetObjectToSerialize(date, typeof(DateOnly));
        object timeSurrogate = provider.GetObjectToSerialize(time, typeof(TimeOnly));

        dateSurrogate.GetType().Should().Be(GetNestedType("SurrogatedDateOnly"), because: "a DateOnly is serialized through its day-number data contract");
        ReadMember(dateSurrogate, "DayNumber").Should().Be(date.DayNumber, because: "the day number is carried over");
        timeSurrogate.GetType().Should().Be(GetNestedType("SurrogatedTimeOnly"), because: "a TimeOnly is serialized through its ticks data contract");
        ReadMember(timeSurrogate, "Ticks").Should().Be(time.Ticks, because: "the ticks are carried over");
        provider.GetObjectToSerialize(other, typeof(string)).Should().BeSameAs(other, because: "other values are serialized as they are");
    }

    public void SurrogateProvider_ConvertsSurrogatesBackToDateOnlyAndTimeOnly()
    {
        ISerializationSurrogateProvider provider = GetSurrogateProvider();
        object dateSurrogate = CreateSurrogate("SurrogatedDateOnly", "DayNumber", new DateOnly(2024, 5, 17).DayNumber);
        object timeSurrogate = CreateSurrogate("SurrogatedTimeOnly", "Ticks", new TimeOnly(13, 45, 30).Ticks);
        const string other = "not a surrogate";

        provider.GetDeserializedObject(dateSurrogate, typeof(DateOnly)).Should().Be(new DateOnly(2024, 5, 17), because: "the day number is converted back");
        provider.GetDeserializedObject(timeSurrogate, typeof(TimeOnly)).Should().Be(new TimeOnly(13, 45, 30), because: "the ticks are converted back");
        provider.GetDeserializedObject(other, typeof(string)).Should().BeSameAs(other, because: "other values are returned as they are");
    }

    public void Deserialize_ConvertsDeserializedSurrogatesToDateOnlyAndTimeOnly()
    {
        object?[]? actual = DataSerializationHelper.Deserialize(
        [
            GetNestedType("SurrogatedDateOnly").AssemblyQualifiedName,
            "{\"DayNumber\":739022}",
            GetNestedType("SurrogatedTimeOnly").AssemblyQualifiedName,
            "{\"Ticks\":495300000000}",
        ]);

        actual.Should().NotBeNull(because: "an even-length payload is deserialized");
        actual!.Length.Should().Be(2, because: "two values were serialized");
        actual[0].Should().Be(new DateOnly(2024, 5, 17), because: "the manual surrogate conversion turns the day number back into a DateOnly");
        actual[1].Should().Be(new TimeOnly(13, 45, 30), because: "the manual surrogate conversion turns the ticks back into a TimeOnly");
    }

#if NET7_0_OR_GREATER
    // DateOnly and TimeOnly are built-in data contracts from .NET 10 on, so the configuration of the cached
    // serializer is the only effect of registering the surrogate provider that is observable on every target.
    public void Serialize_RegistersTheSurrogateProviderOnTheSerializerItCaches()
    {
        string assemblyQualifiedName = typeof(UncachedPayload).AssemblyQualifiedName!;
        ConcurrentDictionary<string, DataContractJsonSerializer> cache = GetSerializerCache();
        cache.TryRemove(assemblyQualifiedName, out _);

        try
        {
            DataSerializationHelper.Serialize([new UncachedPayload { Count = 3, Name = "probe" }]);

            cache.TryGetValue(assemblyQualifiedName, out DataContractJsonSerializer? serializer).Should().BeTrue(because: "the serializer used for a type is cached by its assembly-qualified name");
            ReadSurrogateProvider(serializer!).Should().BeSameAs(GetSurrogateProvider(), because: "DateOnly and TimeOnly members can only be written through the surrogate provider");
        }
        finally
        {
            cache.TryRemove(assemblyQualifiedName, out _);
        }
    }

    public void Deserialize_RegistersTheSurrogateProviderOnTheSerializerItCaches()
    {
        string assemblyQualifiedName = typeof(UncachedPayload).AssemblyQualifiedName!;
        ConcurrentDictionary<string, DataContractJsonSerializer> cache = GetSerializerCache();

        try
        {
            string?[]? serialized = DataSerializationHelper.Serialize([new UncachedPayload { Count = 3, Name = "probe" }]);
            cache.TryRemove(assemblyQualifiedName, out _);

            DataSerializationHelper.Deserialize(serialized);

            cache.TryGetValue(assemblyQualifiedName, out DataContractJsonSerializer? serializer).Should().BeTrue(because: "the serializer used for a payload is cached by its assembly-qualified name");
            ReadSurrogateProvider(serializer!).Should().BeSameAs(GetSurrogateProvider(), because: "DateOnly and TimeOnly members can only be read through the surrogate provider");
        }
        finally
        {
            cache.TryRemove(assemblyQualifiedName, out _);
        }
    }

    private static ISerializationSurrogateProvider? ReadSurrogateProvider(DataContractJsonSerializer serializer)
    {
        PropertyInfo? property = typeof(DataContractJsonSerializer).GetProperty("SerializationSurrogateProvider", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.Should().NotBeNull(because: "the serializer exposes the provider it was configured with through an internal property");

        return (ISerializationSurrogateProvider?)property!.GetValue(serializer);
    }
#endif

    private static ISerializationSurrogateProvider GetSurrogateProvider()
        => (ISerializationSurrogateProvider)GetNestedType("SerializationSurrogateProvider").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    private static object CreateSurrogate(string name, string memberName, object value)
    {
        object surrogate = Activator.CreateInstance(GetNestedType(name))!;
        surrogate.GetType().GetProperty(memberName)!.SetValue(surrogate, value);

        return surrogate;
    }

    private static object? ReadMember(object instance, string memberName)
        => instance.GetType().GetProperty(memberName)!.GetValue(instance);

    private static Type GetNestedType(string name)
        => typeof(DataSerializationHelper).GetNestedType(name, BindingFlags.NonPublic)!;
#endif

    private static ConcurrentDictionary<string, DataContractJsonSerializer> GetSerializerCache()
        => (ConcurrentDictionary<string, DataContractJsonSerializer>)typeof(DataSerializationHelper).GetField("SerializerCache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [DataContract]
    private sealed class UncachedPayload
    {
        [DataMember]
        public int Count { get; set; }

        [DataMember]
        public string? Name { get; set; }
    }
}
