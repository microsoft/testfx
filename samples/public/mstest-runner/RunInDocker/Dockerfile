# Build MyServer application in multi-stage build.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

## Use .NET SDK to build the application.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MyServer/MyServer.csproj", "MyServer/"]
RUN dotnet restore "./MyServer/MyServer.csproj"
COPY . .
WORKDIR "/src/MyServer"
RUN dotnet build "./MyServer.csproj" -c $BUILD_CONFIGURATION -o /app/build

## Publish the application.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MyServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

## Copy the built artifacts into image that is based on aspnet runtime.
## SDK is not needed to run this application.
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyServer.dll"]

# Build MyServer.Tests.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS buildTest
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MyServer.Tests/MyServer.Tests.csproj", "MyServer.Tests/"]
RUN dotnet restore "./MyServer.Tests/MyServer.Tests.csproj"
COPY . .
WORKDIR "/src/MyServer.Tests"
RUN dotnet build "./MyServer.Tests.csproj" -c $BUILD_CONFIGURATION -o /app/buildTest

## Create a test image that depends on the application,
## and copy the test artifacts into it.
FROM final as test
WORKDIR /test
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
COPY --from=buildTest /app/buildTest ./test
ENTRYPOINT ["dotnet", "./test/MyServer.Tests.dll", "--results-directory", "/home/app/TestResults"]