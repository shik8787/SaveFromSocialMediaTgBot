FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Install Chromium for PuppeteerSharp in the target runtime image.
RUN apt-get update \
    && apt-get install -y --no-install-recommends chromium \
    && rm -rf /var/lib/apt/lists/*

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
WORKDIR /src
COPY ["SaveFromSocialMediaTgBot.csproj", "./"]
RUN dotnet restore "SaveFromSocialMediaTgBot.csproj" -a $TARGETARCH
COPY . .
RUN dotnet build "SaveFromSocialMediaTgBot.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore -a $TARGETARCH

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
RUN dotnet publish "SaveFromSocialMediaTgBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore -a $TARGETARCH /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SaveFromSocialMediaTgBot.dll"]
