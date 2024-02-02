#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0.0-alpine3.18 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0.100-1-alpine3.18 AS build
WORKDIR /src
COPY ["src/nuget.config", "src/"]
COPY ["src/SyslogFilesToSql.Npgsql.Migrations/SyslogFilesToSql.Npgsql.Migrations.csproj", "src/SyslogFilesToSql.Npgsql.Migrations/"]
RUN dotnet restore "src/SyslogFilesToSql.Npgsql.Migrations/SyslogFilesToSql.Npgsql.Migrations.csproj"
COPY ["src/SyslogFilesToSql.Npgsql.Datalayer/SyslogFilesToSql.Npgsql.Datalayer.csproj", "src/SyslogFilesToSql.Npgsql.Datalayer/"]
RUN dotnet restore "src/SyslogFilesToSql.Npgsql.Datalayer/SyslogFilesToSql.Npgsql.Datalayer.csproj"
COPY ["src/SyslogFilesToSql/SyslogFilesToSql.csproj", "src/SyslogFilesToSql/"]
RUN dotnet restore "src/SyslogFilesToSql/SyslogFilesToSql.csproj"
COPY src/ src
WORKDIR "/src"
RUN dotnet build "src/SyslogFilesToSql/SyslogFilesToSql.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/SyslogFilesToSql/SyslogFilesToSql.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SyslogFilesToSql.dll"]
