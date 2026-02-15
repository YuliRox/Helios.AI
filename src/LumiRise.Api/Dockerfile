FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props src/Directory.Packages.props
COPY LumiRise.Api/LumiRise.Api.csproj src/LumiRise.Api/LumiRise.Api.csproj
RUN dotnet restore src/LumiRise.Api/LumiRise.Api.csproj -v q --nologo

COPY  LumiRise.Api/ src/LumiRise.Api/
RUN dotnet publish src/LumiRise.Api/LumiRise.Api.csproj -c Release -o /app/publish --no-restore -v q --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

EXPOSE 8080
ENTRYPOINT ["dotnet", "LumiRise.Api.dll"]
