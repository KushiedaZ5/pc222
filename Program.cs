using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Hubs;
using PlataformaCreditos.Models;
using PlataformaCreditos.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de Base de Datos SQLite (EF Core)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") 
    ?? "DataSource=app.db;Cache=Shared";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// 2. Configuración de Identity con Roles y credenciales simplificadas
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// 3. Configuración de Redis / Distributed Cache (Pregunta 4 y 8)
var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
    ?? builder.Configuration["Redis__ConnectionString"]
    ?? Environment.GetEnvironmentVariable("Redis__ConnectionString");

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "PlataformaCreditos:";
    });
}
else
{
    // Respaldo en memoria para entorno de desarrollo local sin Redis
    builder.Services.AddDistributedMemoryCache();
}

// Configuración de Sesiones con respaldo en caché distribuida (Redis-backed)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 4. Servicio de Caché de solicitudes
builder.Services.AddScoped<ISolicitudCacheService, SolicitudCacheService>();

// 5. Configuración de SignalR / WebSocket Hub (Pregunta 6)
builder.Services.AddSignalR();

// 6. Configuración de Cloud MQ / RabbitMQ (Pregunta 7 y 8)
builder.Services.Configure<RabbitMqSettings>(options =>
{
    builder.Configuration.GetSection(RabbitMqSettings.SectionName).Bind(options);

    var envConn = Environment.GetEnvironmentVariable("RabbitMq__ConnectionString");
    if (!string.IsNullOrWhiteSpace(envConn)) options.ConnectionString = envConn;

    var envQueue = Environment.GetEnvironmentVariable("RabbitMq__QueueName");
    if (!string.IsNullOrWhiteSpace(envQueue)) options.QueueName = envQueue;

    var envConsumer = Environment.GetEnvironmentVariable("RabbitMq__ConsumerEnabled");
    if (bool.TryParse(envConsumer, out var consumerEnabled)) options.ConsumerEnabled = consumerEnabled;
});

builder.Services.AddSingleton<IRabbitMqProducer, RabbitMqProducer>();
builder.Services.AddHostedService<RabbitMqConsumerService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Inicialización de la base de datos, roles y datos semilla (Pregunta 1)
using (var scope = app.Services.CreateScope())
{
    try
    {
        await DbInitializer.SeedAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error al ejecutar el sembrado inicial de la base de datos.");
    }
}

// Configuración del pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// Endpoint de SignalR WebSocket (Pregunta 6)
app.MapHub<SolicitudesHub>("/hubs/solicitudes");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
