# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first (layer caches while only source changes).
COPY apiship.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish apiship.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
# Render provides the real port via $PORT (Program.cs binds to it); this is a
# sensible default for local `docker run` and documents the exposed port.
EXPOSE 10000

ENTRYPOINT ["dotnet", "apiship.dll"]
