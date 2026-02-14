// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.SelfRealExamples.UnitTests;

[TestClass]
public class STATestMethodSyncContext
{
    [STATestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void STAByDefaultDoesNotUseSynchronizationContext()
    {
        Assert.IsNull(SynchronizationContext.Current);
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
    }

    [STATestMethod(UseSTASynchronizationContext = true)]
    [OSCondition(OperatingSystems.Windows)]
    public async Task STAWithSynchronizationContextIsCorrect()
    {
        Assert.IsNotNull(SynchronizationContext.Current);
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());

        await Task.Delay(100);

        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
    }
}
