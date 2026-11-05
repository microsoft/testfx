// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class ThreadOperationsTests : TestContainer
{
    private const int CompletionTimeoutMilliseconds = 30_000;

    private readonly ThreadOperations _asyncOperations;

    public ThreadOperationsTests() => _asyncOperations = new ThreadOperations();

    public void ExecuteShouldStartTheActionOnANewThread()
    {
        int callingThreadId = Environment.CurrentManagedThreadId;
        int actionThreadId = callingThreadId;
        void Action() => actionThreadId = Environment.CurrentManagedThreadId;

        _asyncOperations.Execute(Action, CompletionTimeoutMilliseconds, CancellationToken.None).Should().BeTrue();
        actionThreadId.Should().NotBe(callingThreadId);
    }

    public void ExecuteShouldReturnFalseIfTheActionTimesOut()
    {
        using ManualResetEventSlim actionStarted = new();
        using ManualResetEventSlim releaseAction = new();
        using Task action = new(() =>
        {
            actionStarted.Set();
            releaseAction.Wait();
        });

        Task<bool> execution = Task.Run(() => _asyncOperations.Execute(
            () => action.RunSynchronously(TaskScheduler.Default), 1, CancellationToken.None));

        try
        {
            actionStarted.Wait(CompletionTimeoutMilliseconds).Should().BeTrue("the action must start before its timeout result is checked");
            execution.Wait(CompletionTimeoutMilliseconds).Should().BeTrue("Execute must return while the action is blocked");
            execution.GetAwaiter().GetResult().Should().BeFalse();
        }
        finally
        {
            releaseAction.Set();
            Task.WhenAll(execution, action).Wait(CompletionTimeoutMilliseconds).Should().BeTrue("all workers must finish before the synchronization gates are disposed");
        }
    }
}
