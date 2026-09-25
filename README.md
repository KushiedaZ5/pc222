# Examen Parcial: Plataforma Financiera de Gestión de Créditos

Plataforma web interna desarrollada con **ASP.NET Core MVC (.NET 10)** para la evaluación, aprobación y gestión de solicitudes de crédito financieras con **Identity**, **Entity Framework Core (SQLite)**, **Redis Cache / Sesión**, **WebSockets en tiempo real (SignalR)** y **Cloud MQ (RabbitMQ en CloudAMQP)**.

---

## 📋 Arquitectura y Stack Tecnológico

- **Framework Web:** ASP.NET Core MVC (.NET 10)
- **Seguridad e Identidad:** ASP.NET Core Identity con Roles (`Analista`, `Cliente`)
- **Base de Datos & ORM:** SQLite + Entity Framework Core 10 (con índices únicos condicionales)
- **Caché y Sesiones:** Redis (StackExchange.Redis) / Fallback de memoria distribuida
- **Comunicación en Tiempo Real:** SignalR Hub con transporte WebSocket forzado en `/hubs/solicitudes`
- **Mensajería Asíncrona:** Cloud MQ gestionado con RabbitMQ (CloudAMQP) con confirmación de publicación (Publisher Confirms), entrega persistente, ACK manual y consumidor desacoplado en `BackgroundService`
- **Contenedorización & Despliegue:** Docker multi-etapa + Render.com Web Service con disco persistente

---

## 👥 Cuentas de Acceso Preconfiguradas (Seed Data)

La base de datos SQLite se inicializa automáticamente al arrancar la aplicación con los siguientes usuarios:

| Rol | Correo Electrónico | Contraseña | Ingresos Mensuales | Estado Inicial |
| :--- | :--- | :--- | :--- | :--- |
| **Analista** | `analista@creditos.com` | `Password123!` | N/A | Acceso al panel `/Analista` |
| **Cliente 1** | `cliente1@creditos.com` | `Password123!` | \$3,000.00 | 1 solicitud inicial en estado **Pendiente** (\$6,000.00) |
| **Cliente 2** | `cliente2@creditos.com` | `Password123!` | \$5,000.00 | 1 solicitud inicial en estado **Aprobado** (\$15,000.00) |

---

## 🌳 Estructura de Ramas y Pull Requests en GitHub

Para cumplir estrictamente con los criterios de evaluación del examen, las preguntas deben ser versionadas en ramas independientes y cerradas mediante Pull Requests hacia `main`:

```bash
# Pregunta 1: Bootstrap + Modelo de datos
git checkout -b feature/bootstrap-dominio
# (Comitear modelos Cliente, SolicitudCredito, DbContext, migraciones y seed data)
# Abrir PR -> main

# Pregunta 2: Catálogo de solicitudes y filtros
git checkout -b feature/catalogo-solicitudes
# (Comitear Mis Solicitudes, filtros server-side, vista Detalle)
# Abrir PR -> main

# Pregunta 3: Registro y validaciones de solicitud
git checkout -b feature/solicitudes
# (Comitear formulario Crear, validación 10x ingresos, sólo 1 pendiente activa)
# Abrir PR -> main

# Pregunta 4: Sesiones y Redis
git checkout -b feature/sesion-redis
# (Comitear Redis distributed cache 60s, invalidación, sesión de última solicitud)
# Abrir PR -> main

# Pregunta 5: Panel de Analista (rol)
git checkout -b feature/panel-analista
# (Comitear /Analista con [Authorize(Roles="Analista")], validación 5x, rechazo con motivo)
# Abrir PR -> main

# Pregunta 6: Notificaciones con WebSocket
git checkout -b feature/websocket-notificaciones
# (Comitear Hub /hubs/solicitudes, WebSocket transport, reconexión, sincronización)
# Abrir PR -> main

# Pregunta 7: Mensajería asíncrona con Cloud MQ
git checkout -b feature/cloudmq-notificaciones
# (Comitear RabbitMqProducer, RabbitMqConsumerService, NotificacionesController, vista)
# Abrir PR -> main

# Pregunta 8: Despliegue en Render
git checkout -b deploy/render
# (Comitear Dockerfile, entrypoint.sh, render.yaml, documentación de disco)
# Abrir PR -> main
```

---

## 🚀 Ejecución en Entorno Local

### 1. Requisitos Previos
- .NET 10 SDK instalado.
- Opcional: Instancia de Redis (local o cuenta gratuita en [Redis Cloud](https://cloud.redis.io)).
- Opcional: Instancia de RabbitMQ (local o cuenta gratuita en [CloudAMQP](https://customer.cloudamqp.com)).

### 2. Configuración de Variables en `appsettings.json` o Variables de Entorno
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "DataSource=app.db;Cache=Shared"
  },
  "Redis": {
    "ConnectionString": "tu-redis-url:6379,password=tu_password,abortConnect=false"
  },
  "RabbitMq": {
    "ConnectionString": "amqps://usuario:password@servidor.cloudamqp.com/vhost",
    "QueueName": "solicitudes.notificaciones",
    "ConsumerEnabled": true
  }
}
```
*(Nota: Si Redis o RabbitMQ no tienen credenciales configuradas localmente, la aplicación utiliza DistributedMemoryCache de respaldo y advierte en consola sin caerse).*

### 3. Aplicar Migraciones de SQLite
```bash
dotnet ef database update
```

### 4. Ejecutar el Proyecto
```bash
dotnet run
```
La aplicación estará disponible en `http://localhost:5000` o `https://localhost:5001`.

---

## 🧪 Pruebas Paso a Paso por Pregunta

### Pregunta 1 — Bootstrap + Modelo de Datos
- **Restricciones en DB:** Se configuró un índice condicional único `WHERE Estado = 'Pendiente'` para que el cliente no pueda tener más de una solicitud pendiente a nivel de base de datos SQLite.
- **Validación:** Comprobar que en SQLite existen las tablas `Clientes`, `SolicitudesCredito`, `Notificaciones` y las tablas de Identity.
- **Seed Data:** Al iniciar la aplicación, se crean automáticamente 2 clientes y un analista.

### Pregunta 2 — Catálogo de Solicitudes y Filtros
- Iniciar sesión con `cliente1@creditos.com`.
- Navegar a **Mis Solicitudes**.
- Probar filtros server-side:
  - Ingresar Monto Mínimo: `-50` ➔ El servidor rechaza la consulta con mensaje de error: *"No se aceptan montos mínimos negativos"*.
  - Ingresar Fecha Desde `2026-12-31` y Fecha Hasta `2026-01-01` ➔ El servidor rechaza con *"La fecha de inicio no puede ser mayor a la fecha de fin"*.
  - Filtrar por estado `Pendiente` o `Aprobado` ➔ La lista se filtra correctamente.
- Dar clic en **Detalle** para inspeccionar los datos completos de la solicitud.

### Pregunta 3 — Registro y Validaciones de Solicitud
- Entrar como `cliente2@creditos.com` (Ingresos: \$5,000.00, Capacidad 10x: \$50,000.00).
- Ir a **Nueva Solicitud**.
- Probar reglas de negocio:
  1. Intentar solicitar `0` o negativo ➔ Error en la misma vista: *"El monto solicitado debe ser mayor a 0"*.
  2. Intentar solicitar `60000` (mayor a 10 veces sus ingresos) ➔ Error: *"El monto solicitado supera el límite de 10 veces sus ingresos mensuales (\$50,000.00)"*.
  3. Solicitar `8000` ➔ Se registra en estado `Pendiente`.
  4. Intentar enviar otra solicitud de inmediato ➔ Error en la vista: *"Ya tiene una solicitud en estado Pendiente. Debe esperar la evaluación antes de solicitar otra"*.

### Pregunta 4 — Sesiones y Redis
- **Sesión (Redis-backed):** Al visitar el detalle de una solicitud (por ejemplo la solicitud #1 de \$6,000.00), en la barra de navegación superior (layout) aparecerá inmediatamente el enlace:
  `Ver última solicitud ($6,000.00)`. Al hacer clic, redirige directamente al detalle de dicha solicitud.
- **Caché (Redis 60s):** La consulta sin filtros de **Mis Solicitudes** se almacena en caché durante 60 segundos. Si se registra una nueva solicitud o el analista cambia su estado, la caché se invalida inmediatamente para ese usuario.

### Pregunta 5 — Panel de Analista (Rol)
- Iniciar sesión como `analista@creditos.com`.
- Ir a **Panel Analista** (`/Analista`).
- Comprobar que sólo lista solicitudes con estado `Pendiente`.
- Si un usuario no autenticado o con rol `Cliente` intenta acceder a `/Analista`, recibe acceso denegado (HTTP 403 / Forbid).
- **Regla 5x de Aprobación:**
  - Si una solicitud excede 5 veces los ingresos mensuales del cliente, el botón **Aprobar** aparece deshabilitado y se muestra la etiqueta *"Excede 5x ingresos (No aprobable)"*. Si se intenta forzar por POST, el servidor rechaza la aprobación con mensaje de error en `TempData`.
- **Rechazo con Motivo Obligatorio:**
  - Al dar clic en **Rechazar**, se abre un modal de Bootstrap que exige ingresar el motivo del rechazo. No permite rechazar sin motivo (validación tanto en cliente como en servidor).

### Pregunta 6 — Notificaciones en Tiempo Real con WebSocket
- **Hub:** Endpoint `/hubs/solicitudes` con atributo `[Authorize]`. Si se intenta conectar anónimamente, es rechazado con HTTP 401.
- **Transporte:** Se utiliza estrictamente `signalR.HttpTransportType.WebSockets`.
- **Prueba de doble sesión simultánea:**
  1. Abrir una ventana normal del navegador con el usuario `cliente1@creditos.com` en **Mis Solicitudes** o en la vista **Detalle**.
  2. Verificar en la esquina superior el badge verde: `🟢 WebSocket: Conectado`.
  3. En una ventana de incógnito, iniciar sesión como `analista@creditos.com` y entrar a `/Analista`.
  4. El analista aprueba o rechaza la solicitud de `cliente1`.
  5. **Resultado inmediato:** En la pantalla del `cliente1`, sin recargar la página:
     - El badge de la solicitud cambia a **Aprobado** (verde) o **Rechazado** (rojo).
     - Si fue rechazado, aparece el texto del motivo.
     - Aparece un Toast flotante animado informando la actualización.
  6. Si un `cliente2` está conectado simultáneamente, comprobar que **NO** recibe el evento (aislamiento por `Clients.User(propietarioUsuarioId)`).
  7. **Reconexión y Sincronización:** Si se desactiva la red momentáneamente, el estado cambia a `Reconectando...` / `Desconectado`, y al reconectarse invoca automáticamente `/Solicitudes/ObtenerEstado` para recuperar el estado vigente actualizado.

### Pregunta 7 — Mensajería Asíncrona con Cloud MQ (RabbitMQ en CloudAMQP)
- **Cola durable:** `solicitudes.notificaciones` conectada mediante protocolo AMQPS (`amqps://...`).
- **Publicador con Publisher Confirms:** Cuando se registra una solicitud en `/Solicitudes/Crear`, se publica un mensaje JSON persistente de tipo `SolicitudRegistrada`:
  ```json
  {
    "MessageId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
    "SolicitudId": 3,
    "UsuarioId": "...",
    "FechaEventoUtc": "2026-09-25T03:00:00Z"
  }
  ```
  El canal espera la confirmación del broker (`publisherConfirmationsEnabled: true`). Si la publicación falla, la solicitud se conserva en SQLite y se muestra una advertencia al usuario con el `MessageId` para su reenvío.
- **Consumidor en `BackgroundService`:**
  - Consume de `solicitudes.notificaciones` con `autoAck: false`.
  - Verifica si el `MessageId` ya existe en SQLite (idempotencia ante reentregas/redeliveries). Si ya existe, emite `BasicAckAsync` inmediatamente sin duplicar el registro.
  - Guarda la notificación: *"Recibimos tu solicitud de crédito y está pendiente de evaluación"*.
  - Solo envía el `BasicAckAsync` manual después de guardar exitosamente en SQLite.
- **Vista Mis Notificaciones:** En `/Notificaciones`, el cliente consulta las notificaciones generadas asíncronamente.
- **Prueba con consumidor desactivado:**
  1. Configurar `RabbitMq__ConsumerEnabled=false` en variables de entorno o `appsettings.json`.
  2. Registrar una nueva solicitud como cliente.
  3. Ir a la consola web de CloudAMQP: se observa 1 mensaje encolado (*Ready*).
  4. Reactivar `RabbitMq__ConsumerEnabled=true` y reiniciar el servicio.
  5. La cola se vacía (*Acked*) y aparece la notificación en `/Notificaciones`.
  6. Reenviar un mensaje con el mismo `MessageId` desde la consola de CloudAMQP: el consumidor lo procesa, emite ACK y no duplica la fila en SQLite.

---

## ☁️ Pregunta 8 — Despliegue en Render.com

### Configuración del Servicio Web en Render:
- **Environment:** Docker
- **Instance Type:** Free (1 instancia)
- **Region:** Oregon (o la de su preferencia)

### Variables de Entorno en el Dashboard de Render:
| Variable de Entorno | Valor |
| :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://0.0.0.0:${PORT}` *(gestionado vía `entrypoint.sh`)* |
| `ConnectionStrings__DefaultConnection` | `DataSource=/data/app.db;Cache=Shared` |
| `Redis__ConnectionString` | `tu-servidor-redis.redislabs.com:6379,password=...` |
| `RabbitMq__ConnectionString` | `amqps://usuario:pass@servidor.cloudamqp.com/vhost` |
| `RabbitMq__QueueName` | `solicitudes.notificaciones` |
| `RabbitMq__ConsumerEnabled` | `true` |

### Persistencia de SQLite entre Despliegues y Reinicios
En Render, los contenedores son efímeros por defecto. Para asegurar que la base de datos SQLite `app.db` no se pierda durante reinicios o nuevos despliegues:
1. Ir a la sección **Disks** del Web Service en Render.
2. Añadir un disco persistente con:
   - **Name:** `credit_sqlite_data`
   - **Mount Path:** `/data`
   - **Size:** `1 GB`
3. La variable `ConnectionStrings__DefaultConnection` apunta a `DataSource=/data/app.db;Cache=Shared`. De este modo, la base de datos se almacena en el volumen persistente de Render.

### Manejo Dinámico del Puerto `$PORT`
Render asigna un puerto dinámico a través de la variable `$PORT`. Dado que en ciertas plataformas Linux `${PORT}` no se expande automáticamente dentro de la variable de entorno, se incluye el script ejecutable `entrypoint.sh`:
```sh
#!/bin/sh
PORT="${PORT:-8080}"
exec dotnet PlataformaCreditos.dll --urls "http://0.0.0.0:$PORT"
```
Esto garantiza que la aplicación escuche exactamente en el puerto asignado por Render.

---

## 🛡️ Control de Errores y Procedimiento de Reenvío en Mensajería

1. **Mensaje Inválido o Malformado:**
   - Si el consumidor recibe un payload que no cumple con el esquema JSON esperado o carece de `MessageId`/`UsuarioId`, el consumidor emite un `BasicNack(requeue: false)` para desecharlo inmediatamente y registrar el error en los logs, evitando bucles infinitos de reentrega (*poison messages*).
2. **Procedimiento de Reenvío Manual con el Mismo `MessageId`:**
   - Si la publicación en RabbitMQ falla (por ejemplo por pérdida de conectividad temporal con CloudAMQP), la solicitud queda guardada en SQLite y el sistema notifica en pantalla el `MessageId` generado.
   - Para reintentar manualmente, se puede publicar en la consola de CloudAMQP con el mismo `MessageId`. Debido a la restricción de clave única en SQLite y la validación de idempotencia en `RabbitMqConsumerService`, el mensaje será aceptado y confirmado sin riesgo de duplicar la notificación.

---

## 🌐 URL de Producción en Render
- **URL del Servicio:** `https://plataforma-creditos.onrender.com` *(reemplazar por la URL generada al desplegar)*
