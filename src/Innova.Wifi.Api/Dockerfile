# 1. Aşama: Çalışma Ortamı (Base)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# --- KRİTİK EKLEME: PostgreSQL sürücüsünün (Npgsql) beklediği kütüphaneyi yüklüyoruz ---
# Bu satır dün çalışan ama bugün imaj güncellenince uçan o bağlantı hatasını çözer.
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
# -----------------------------------------------------------------------------------

# 2. Aşama: Derleme (Build)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Innova.Wifi.Api.csproj", "./"]
RUN dotnet restore "Innova.Wifi.Api.csproj"
COPY . .
RUN dotnet build "Innova.Wifi.Api.csproj" -c Release -o /app/build

# 3. Aşama: Yayınlama (Publish)
FROM build AS publish
RUN dotnet publish "Innova.Wifi.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 4. Aşama: Final (Final)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Innova.Wifi.Api.dll"]