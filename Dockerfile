FROM munique/openmu:latest

COPY ConnectionSettings.xml /app/ConnectionSettings.xml

EXPOSE 8080 44405 55901 55980
