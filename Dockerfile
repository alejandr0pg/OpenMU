# Stage 1: Build OpenMU from source
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY OpenMU/src/Directory.Packages.props .
COPY OpenMU/src/Directory.Build.props .
COPY OpenMU/src/Startup/MUnique.OpenMU.Startup.csproj Startup/
RUN dotnet restore "Startup/MUnique.OpenMU.Startup.csproj"
COPY OpenMU/src/ .
WORKDIR /src/Startup
RUN dotnet publish "MUnique.OpenMU.Startup.csproj" -c Release -o /app/publish -p:ci=true

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh && \
    mkdir /app/logs && \
    chmod 777 /app/logs && \
    chmod 777 /app/ConnectionSettings.xml

EXPOSE 8080 44405 55901 55980

USER $APP_UID
ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["-autostart"]
