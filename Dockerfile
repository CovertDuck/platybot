### Build ###
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
WORKDIR /src
COPY Platybot/*.csproj .
RUN dotnet restore
COPY Platybot .
RUN dotnet publish -c Release -o /app

### Install ###
FROM mcr.microsoft.com/dotnet/aspnet:7.0 as runtime
WORKDIR /app
COPY --from=build-env /app .

### Environment ###
ARG now
ARG platybot_token
ENV BUILD_DATE $now
ENV PLATYBOT_TOKEN $platybot_token
ENV ASPNETCORE_ENVIRONMENT Production
ENV FONTCONFIG_PATH /app/Resources/Fonts

### Volumes ###
VOLUME /app/data
VOLUME /app/logs

### Init ###
ENTRYPOINT ["dotnet", "Platybot.dll"]
