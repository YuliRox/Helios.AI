FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props Directory.Packages.props
COPY LumiRise.MockDimmerDevice/LumiRise.MockDimmerDevice.csproj LumiRise.MockDimmerDevice/LumiRise.MockDimmerDevice.csproj
RUN dotnet restore LumiRise.MockDimmerDevice/LumiRise.MockDimmerDevice.csproj 

COPY LumiRise.MockDimmerDevice LumiRise.MockDimmerDevice
RUN dotnet publish LumiRise.MockDimmerDevice/LumiRise.MockDimmerDevice.csproj -c Release -o /app/publish --no-restore --nologo

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "LumiRise.MockDimmerDevice.dll"]
