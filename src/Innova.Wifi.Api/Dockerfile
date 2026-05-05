FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Innova.Wifi.Api.csproj", "."]
RUN dotnet restore "Innova.Wifi.Api.csproj"
COPY . .
RUN dotnet build "Innova.Wifi.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Innova.Wifi.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Innova.Wifi.Api.dll"]