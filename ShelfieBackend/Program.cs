using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ShelfieBackend.Data;
using ShelfieBackend.Middleware;
using ShelfieBackend.Repositories;
using ShelfieBackend.Services;
using ShelfieBackend.States;
using ShelfieBackend.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.Debug()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Добавляем сервисы в контейнер.
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Настройка Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "My API", Version = "v1" });
    options.EnableAnnotations();
});

// Настройка подключения к базе данных
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Настройка аутентификации JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.Request.Cookies.ContainsKey("AuthToken"))
            {
                context.Token = context.Request.Cookies["AuthToken"];
            }
            return Task.CompletedTask;
        }
    };
});

// Регистрация сервисов приложения
builder.Services.AddScoped<IAccount, Account>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<AuthenticationStateProvider, AuthenticationProvider>();

// Регистрация HttpClient с базовым адресом (можно вынести в конфигурацию)
builder.Services.AddHttpClient<IApplicationService, ApplicationService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5111");
});
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Конфигурация middleware для обработки ошибок и безопасности
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Конфигурация CORS
app.UseCors(policy =>
{
    policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader();
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseGlobalExceptionMiddleware();


app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "My API");
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

// Seed-данные для режима разработки: добавление администратора, если его нет.
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Применяем миграции (если требуется)
        //dbContext.Database.Migrate();

        // Проверяем наличие администратора
        if (!dbContext.Users.Any(u => u.Role == UserRole.Admin))
        {
            var adminUser = new ApplicationUser
            {
                Name = "Admin",
                Email = "admin@example.com",
                Role = UserRole.Admin,
                Phone = "1234567890",
                DateOfBirth = new DateOnly(2001, 1, 1), // Можно указать другую дату по необходимости
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("AdminPassword123!")
            };

            dbContext.Users.Add(adminUser);
            dbContext.SaveChanges();
        }
    }
}

app.Run();

Log.CloseAndFlush();
