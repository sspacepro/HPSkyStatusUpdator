# ============================
# Build stage
# ============================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore

RUN dotnet publish \
    -c Release \
    -o /app/publish


# ============================
# Runtime stage
# ============================

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app


ENV ASPNETCORE_ENVIRONMENT=Production

ENV ASPNETCORE_URLS=http://+:80

ENV DATA_PATH=/app/Data


COPY --from=build /app/publish .


RUN mkdir -p /app/Data


EXPOSE 80


ENTRYPOINT ["dotnet", "HPSkyStatusUpdator.dll"]