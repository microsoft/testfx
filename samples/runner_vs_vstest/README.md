# MSTest runner vs VSTest

This folder contains a couple of examples of performance differences between MSTest runner vs VSTest.

## Procedure of comparison

The folder contains multiple projects that are created following the `<XXX>C_<YYY>M` pattern where `<XXX>C` is the number of test classes and `<YYY>M` is the number of test methods on each test class.

Each project is configured to use Microsoft Code Coverage and to produce a TRX file so that the setup matches common use case.

Each project is first built using `dotnet build`, and then the test execution is run 3 times to allow some warm-up of the execution.

We are measuring the process execution time using the Powershell `Measure-Command { ... }`.

Running tests:

- with runner: `..\..\artifacts\bin\<FOLDER_TO_TEST>\Debug\net8.0\10C100M.exe --coverage --report-trx`
- with VSTest: `dotnet test <FOLDER_TO_TEST> --no-restore --no-build --logger trx --collect "Code Coverage"`

NOTE: Values are reported from our work machines as examples, you may see differences depending on performance and load of your own machine.

## Results of comparison

| Project   | Number of tests | Machine          | VSTest time (in ms) | MSTest runner time (in ms) |
|-----------|-----------------|------------------|---------------------|----------------------------|
| 10C100M   | 1K              | @Evangelink      | 3090.915            | 1181.1558                  |
| 100C100M  | 10K             | @Evangelink      | 8962.4006           | 3452.7159                  |
| 1000C100M | 100K            | @Evangelink      | 36813.3982          | 20099.726                  |
| 10C100M   | 1K              | @MarcoRossignoli | 3592.1288           | 1369.9419                  |
| 100C100M  | 10K             | @MarcoRossignoli | 10484.8188          | 3693.4448                  |
| 1000C100M | 100K            | @MarcoRossignoli | 43045.2458          | 22567.1521                 |
