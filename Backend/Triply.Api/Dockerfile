# Backend/Triply.Api/Dockerfile
# Multi-stage build: compile with the SDK image, run with the smaller ASP.NET runtime image.

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only the csproj first so `dotnet restore` is cached and doesn't
# re-run every time you change a .cs file.
COPY Backend/Triply.Api/Triply.Api.csproj ./
RUN dotnet restore "Triply.Api.csproj"

# Now copy the rest of the project and publish it.
COPY Backend/Triply.Api/. .
RUN dotnet publish "Triply.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY AI/01-Dataset/curated-data/Extra_AI_Context.csv /app/AI/01-Dataset/curated-data/Extra_AI_Context.csv

# Listen on 8080 inside the container (mapped to a host port in docker-compose.yml)
ENV ASPNETCORE_URLS=http://+:8080
ENV AI__ExtraAiContextPath=/app/AI/01-Dataset/curated-data/Extra_AI_Context.csv
EXPOSE 8080

ENTRYPOINT ["dotnet", "Triply.Api.dll"]
