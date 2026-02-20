FROM munique/openmu:latest

USER root
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh
USER $APP_UID

EXPOSE 8080 44405 55901 55980

ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["-autostart"]
