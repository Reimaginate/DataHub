FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["samples/Reimaginate.DataHub.ReferenceHost/Reimaginate.DataHub.ReferenceHost.csproj", "samples/Reimaginate.DataHub.ReferenceHost/"]
COPY ["framework/Reimaginate.DataHub/Reimaginate.DataHub.csproj", "framework/Reimaginate.DataHub/"]
COPY ["framework/Reimaginate.DataHub.AspNetCore/Reimaginate.DataHub.AspNetCore.csproj", "framework/Reimaginate.DataHub.AspNetCore/"]
COPY ["framework/Reimaginate.DataHub.SharedModels/Reimaginate.DataHub.SharedModels.csproj", "framework/Reimaginate.DataHub.SharedModels/"]
COPY ["framework/Reimaginate.DataHub.Abstractions/Reimaginate.DataHub.Abstractions.csproj", "framework/Reimaginate.DataHub.Abstractions/"]
RUN dotnet restore "samples/Reimaginate.DataHub.ReferenceHost/Reimaginate.DataHub.ReferenceHost.csproj"
COPY . .
RUN dotnet publish "samples/Reimaginate.DataHub.ReferenceHost/Reimaginate.DataHub.ReferenceHost.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Reimaginate.DataHub.ReferenceHost.dll"]
