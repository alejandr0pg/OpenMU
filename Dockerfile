FROM munique/openmu:latest

EXPOSE 8080 44405 55901 55980

ENTRYPOINT ["dotnet", "MUnique.OpenMU.Startup.dll"]
CMD ["-autostart"]
