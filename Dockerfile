# syntax=docker/dockerfile:1

# ---- Stage 1: build the React frontend ----
FROM node:22-alpine AS frontend
WORKDIR /app/frontend
COPY frontend/package*.json ./
RUN npm install
COPY frontend/ ./
RUN npm run build

# ---- Stage 2: publish the .NET backend ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/ ./
RUN dotnet publish src/SteamFarmer.Api/SteamFarmer.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /app/publish ./
COPY --from=frontend /app/frontend/dist ./wwwroot
ENV PORT=5080 \
    FARMER_DATA_DIR=/data \
    FARMER_DEVICE_NAME=SteamIdleFarmer
EXPOSE 5080
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "SteamFarmer.Api.dll"]
