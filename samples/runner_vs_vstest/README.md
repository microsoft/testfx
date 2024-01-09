# MSTest runner vs VSTest

This folder contains a couple of examples of performance differences between MSTest runner vs VSTest.

## Procedure of comparison

The folder contains multiple projects that are created following the `<XXX>C_<YYY>M` pattern where `<XXX>C` is the number of test classes and `<YYY>M` is the number of test methods on each test class.

Each project is configured to use Microsoft Code Coverage and to produce a TRX file so that the setup matches common use case.

Each project is first built using `dotnet build`, and then the test execution is run 3 times to allow some warm-up of the execution.

We are measuring the process execution time using the PowerShell `Measure-Command { ... }`.

Running tests:

- with runner: `..\..\artifacts\bin\<FOLDER_TO_TEST>\Debug\net8.0\10C100M.exe --coverage --report-trx`
- with VSTest: `dotnet test <FOLDER_TO_TEST> --no-restore --no-build --logger trx --collect "Code Coverage"`

NOTE: Values are reported from our work machines as examples, you may see differences depending on performance and load of your own machine.

## Results of comparison

| Project   | Number of tests | Configuration        | Machine          | VSTest time (in ms) | MSTest runner time (in ms) | Gain (%) |
|-----------|-----------------|----------------------|------------------|---------------------|----------------------------|----------|
| 10C100M   | 1K              | Plugged, performance | @Evangelink      | 3090.915            | 1181.1558                  | 262      |
| 100C100M  | 10K             | Plugged, performance | @Evangelink      | 8962.4006           | 3452.7159                  | 259      |
| 1000C100M | 100K            | Plugged, performance | @Evangelink      | 36813.3982          | 20099.726                  | 183      |
| 10C100M   | 1K              | Plugged, performance | @MarcoRossignoli | 3592.1288           | 1369.9419                  | 262      |
| 100C100M  | 10K             | Plugged, performance | @MarcoRossignoli | 10484.8188          | 3693.4448                  | 284      |
| 1000C100M | 100K            | Plugged, performance | @MarcoRossignoli | 43045.2458          | 22567.1521                 | 191      |
| 10C100M   | 1K              | Plugged, performance | @jakubch1        | 3059.7666           | 1223.2776                  | 250      |
| 100C100M  | 10K             | Plugged, performance | @jakubch1        | 8897.0455           | 3675.4061                  | 242      |
| 1000C100M | 100K            | Plugged, performance | @jakubch1        | 40240.6792          | 25915.2722                 | 155      |
| 10C100M   | 1K              | Plugged, performance | @nohwnd          | 3959                | 1012                       | 391      |
| 100C100M  | 10K             | Plugged, performance | @nohwnd          | 11989               | 3940                       | 304      |
| 1000C100M | 100K            | Plugged, performance | @nohwnd          | 57055               | 22100                      | 258      |
| 10C100M   | 1K              | Battery, Balanced    | @nohwnd          | 7508                | 1795                       | 418      |
| 100C100M  | 10K             | Battery, Balanced    | @nohwnd          | 20904               | 6179                       | 338      |
| 1000C100M | 100K            | Battery, Balanced    | @nohwnd          | 126123              | 37845                      | 333      |
