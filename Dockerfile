FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar
COPY ["PlataformaCreditos.csproj", "./"]
RUN dotnet restore "./PlataformaCreditos.csproj"

# Copiar el resto del código y compilar para Release
COPY . .
RUN dotnet publish "PlataformaCreditos.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa final de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Crear directorio de datos para persistencia de base de datos SQLite en disco de Render
RUN mkdir -p /data && chmod 777 /data

COPY --from=build /app/publish .

# Script de entrada para expandir la variable PORT dinámicamente en Render
COPY entrypoint.sh .
RUN chmod +x entrypoint.sh

ENV ASPNETCORE_ENVIRONMENT=Production

# Comando de inicio usando entrypoint.sh para expandir PORT
ENTRYPOINT ["./entrypoint.sh"]
