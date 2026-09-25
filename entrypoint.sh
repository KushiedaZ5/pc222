#!/bin/sh
# Expande dinámicamente la variable PORT proporcionada por Render
PORT="${PORT:-8080}"
echo "========================================="
echo "Iniciando Plataforma de Créditos en puerto: $PORT"
echo "Ambiente: $ASPNETCORE_ENVIRONMENT"
echo "========================================="

exec dotnet PlataformaCreditos.dll --urls "http://0.0.0.0:$PORT"
