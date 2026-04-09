# HangDump extension design and implementation details

This documents explains how this extension works.

## Flow

1. When running `--hangdump`, the following extensions get enabled:
    - `HangDumpProcessLifetimeHandler` (test host controller extension)
    - `HangDumpEnvironmentVariableProvider` (test host controller extension)
    - `HangDumpActivityIndicator` (test host extension)
2. Because there are test host controller extensions enabled, MTP core will consider the starting process as the test host controller, and start a child process that acts as the test host.
3. In the parent process (test host controller), `HangDumpEnvironmentVariableProvider` provides env variables to the child test host process.
    - `TESTINGPLATFORM_HANGDUMP_PIPENAME` set to random guid.
4. In the parent process (test host controller), `HangDumpProcessLifetimeHandler` creates a named pipe server before the test host starts (using the pipe name pointed out to by the env vars). This pipe handles two requests: `ConsumerPipeNameRequest` and `ActivitySignalRequest`
5. The test host process creates another pipe server with a new random pipe name, and sends the pipe name via `ConsumerPipeNameRequest` to the controller
    - NOTE: For the first named pipe that is pointed out to by env vars, the server is the test host controller. But for the second named pipe, the server is the test host.
    - The second pipe handles one request which is `GetInProgressTestsRequest`.
6. The test host controller receives `ConsumerPipeNameRequest` and connects to that pipe.
7. The test host keeps sending `ActivitySignalRequest` as long as tests are progressing.
8. The test host controller detects if `ActivitySignalRequest` isn't received for a period longer than the hang timeout, it dumps the process and kills it.

The pipe communication can be visualized as follows:

1. TestHost -----pipe1-----> TestHostController: `ConsumerPipeNameRequest`
2. TestHost -----pipe1-----> TestHostController: Repeatedly sends heartbeat messages (`ActivitySignalRequest`)
3. TestHostController -----pipe2-----> TestHost: On timeout, sends `GetInProgressTestsRequest`. We print these tests to show what tests might be blamed for the hang. Also this request causes the testhost to stop sending heartbeat. We are already about to take a dump and kill the process.
