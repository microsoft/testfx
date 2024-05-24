// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET462
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Services;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName
public class DiaSessionOperationsTests : TestContainer
{
    private readonly FileOperations _fileOperations;

    public DiaSessionOperationsTests()
    {
        _fileOperations = new FileOperations();
    }

    public void CreateNavigationSessionShouldReturnNullIfSourceIsNull()
    {
        try
        {
            DiaSessionOperations.Initialize(typeof(MockDiaSession).AssemblyQualifiedName, typeof(MockDiaNavigationData).AssemblyQualifiedName);

            Verify(_fileOperations.CreateNavigationSession(null) is null);
            Verify(MockDiaSession.IsConstructorInvoked);
        }
        finally
        {
            MockDiaSession.Reset();
        }
    }

    public void CreateNavigationSessionShouldReturnNullIfDiaSessionNotFound()
    {
        try
        {
            DiaSessionOperations.Initialize(string.Empty, string.Empty);

            Verify(_fileOperations.CreateNavigationSession(null) is null);
            Verify(!MockDiaSession.IsConstructorInvoked);
        }
        finally
        {
            MockDiaSession.Reset();
        }
    }

    public void CreateNavigationSessionShouldReturnDiaSession()
    {
        try
        {
            DiaSessionOperations.Initialize(
                typeof(MockDiaSession).AssemblyQualifiedName,
                typeof(MockDiaNavigationData).AssemblyQualifiedName);

            object diaSession = _fileOperations.CreateNavigationSession(typeof(DiaSessionOperationsTests).GetTypeInfo().Assembly.Location);

            Verify(diaSession is MockDiaSession);
        }
        finally
        {
            MockDiaSession.Reset();
        }
    }

    public void GetNavigationDataShouldReturnDataFromNavigationSession()
    {
        try
        {
            DiaSessionOperations.Initialize(
                typeof(MockDiaSession).AssemblyQualifiedName,
                typeof(MockDiaNavigationData).AssemblyQualifiedName);
            var navigationData = new MockDiaNavigationData() { FileName = "mock", MinLineNumber = 86 };
            MockDiaSession.DiaNavigationData = navigationData;

            object diaSession = _fileOperations.CreateNavigationSession(typeof(DiaSessionOperationsTests).GetTypeInfo().Assembly.Location);
            _fileOperations.GetNavigationData(
diaSession,
typeof(DiaSessionOperationsTests).FullName,
"GetNavigationDataShouldReturnDataFromNavigationSession",
out int minLineNumber,
out string fileName);

            Verify(navigationData.MinLineNumber == minLineNumber);
            Verify(navigationData.FileName == fileName);
            Verify(MockDiaSession.IsGetNavigationDataInvoked);
        }
        finally
        {
            MockDiaSession.Reset();
        }
    }

    public void GetNavigationDataShouldNotThrowOnNullNavigationSession()
    {
        DiaSessionOperations.Initialize(string.Empty, string.Empty);

        _fileOperations.GetNavigationData(
        null,
        typeof(DiaSessionOperationsTests).FullName,
        "GetNavigationDataShouldReturnDataFromNavigationSession",
        out int minLineNumber,
        out string fileName);

        Verify(minLineNumber == -1);
        Verify(fileName is null);
    }

    public void GetNavigationDataShouldNotThrowOnMissingFileNameField()
    {
        try
        {
            DiaSessionOperations.Initialize(
            typeof(MockDiaSession).AssemblyQualifiedName,
            typeof(MockDiaNavigationData3).AssemblyQualifiedName);
            var navigationData = new MockDiaNavigationData3() { MinLineNumber = 86 };
            MockDiaSession.DiaNavigationData = navigationData;

            object diaSession = _fileOperations.CreateNavigationSession(typeof(DiaSessionOperationsTests).GetTypeInfo().Assembly.Location);
            _fileOperations.GetNavigationData(
            diaSession,
            typeof(DiaSessionOperationsTests).FullName,
            "GetNavigationDataShouldReturnDataFromNavigationSession",
            out int minLineNumber,
            out string fileName);

            Verify(minLineNumber == 86);
            Verify(fileName is null);
        }
        finally
        {
            MockDiaSession.Reset();
        }
    }

    public void GetNavigationDataShouldNotThrowOnMissingLineNumberField()
    {
        try
        {
            DiaSessionOperations.Initialize(
                typeof(MockDiaSession).AssemblyQualifiedName,
                typeof(MockDiaNavigationData2).AssemblyQualifiedName);
            var navigationData = new MockDiaNavigationData2() { FileName = "mock" };
            MockDiaSession.DiaNavigationData = navigationData;

            object diaSession = _fileOperations.CreateNavigationSession(typeof(DiaSessionOperationsTests).GetTypeInfo().Assembly.Location);
            _fileOperations.GetNavigationData(
            diaSession,
            typeof(DiaSessionOperationsTests).FullName,
            "GetNavigationDataShouldReturnDataFromNavigationSession",
            out int minLineNumber,
            out string fileName);

            Verify(minLineNumber == -1);
            Verify(navigationData.FileName == fileName);
        }
        finally
        {
            MockDiaSession.Reset();
        }
    }

    public void DisposeNavigationSessionShouldDisposeDiaSession()
    {
        try
        {
            DiaSessionOperations.Initialize(
                typeof(MockDiaSession).AssemblyQualifiedName,
                typeof(MockDiaNavigationData).AssemblyQualifiedName);

            object diaSession = _fileOperations.CreateNavigationSession(typeof(DiaSessionOperationsTests).GetTypeInfo().Assembly.Location);
            _fileOperations.DisposeNavigationSession(diaSession);

            Verify(MockDiaSession.IsDisposeInvoked);
        }
        finally
        {
            MockDiaSession.Reset();
        }
    }
}

public class MockDiaSession : IDisposable
{
    static MockDiaSession()
    {
        IsConstructorInvoked = false;
        IsGetNavigationDataInvoked = false;
        IsDisposeInvoked = false;
    }

    public MockDiaSession(string source)
    {
        IsConstructorInvoked = true;
        if (string.IsNullOrEmpty(source))
        {
            throw new Exception();
        }
    }

    public static bool IsConstructorInvoked { get; set; }

    public static IDiaNavigationData DiaNavigationData { get; set; }

    public static bool IsGetNavigationDataInvoked { get; set; }

    public static bool IsDisposeInvoked { get; set; }

    public static void Reset()
    {
        IsConstructorInvoked = false;
        IsGetNavigationDataInvoked = false;
    }

    public object GetNavigationData(string className, string methodName)
    {
        IsGetNavigationDataInvoked = true;
        return DiaNavigationData;
    }

    public void Dispose() => IsDisposeInvoked = true;
}

public interface IDiaNavigationData;

public class MockDiaNavigationData : IDiaNavigationData
{
    public string FileName { get; set; }

    public int MinLineNumber { get; set; }
}

public class MockDiaNavigationData2 : IDiaNavigationData
{
    public string FileName { get; set; }
}

public class MockDiaNavigationData3 : IDiaNavigationData
{
    public int MinLineNumber { get; set; }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

#endif
