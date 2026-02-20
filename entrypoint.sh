#!/bin/sh
set -e

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-openmu}"
DB_ADMIN_USER="${DB_ADMIN_USER:-postgres}"
DB_ADMIN_PW="${DB_ADMIN_PW:-admin}"
DB_SSL="${DB_SSL:-Prefer}"
DB_TIMEOUT="${DB_TIMEOUT:-120}"

CONN_BASE="Server=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Command Timeout=${DB_TIMEOUT};SSL Mode=${DB_SSL};Trust Server Certificate=true"
ADMIN_CONN="${CONN_BASE};User Id=${DB_ADMIN_USER};Password=${DB_ADMIN_PW}"

cat > /app/ConnectionSettings.xml <<XMLEOF
<?xml version="1.0" encoding="utf-8" ?>
<ConnectionSettings xmlns="http://www.munique.net/ConnectionSettings">
  <Connections>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.EntityDataContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.TypedContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.ConfigurationContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.AccountContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.TradeContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.FriendContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.GuildContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
  </Connections>
</ConnectionSettings>
XMLEOF

echo "ConnectionSettings.xml generated: Host=${DB_HOST} Port=${DB_PORT} SSL=${DB_SSL}"

exec dotnet MUnique.OpenMU.Startup.dll "$@"
