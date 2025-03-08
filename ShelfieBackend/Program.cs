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
using System.Reflection;
using ShelfieBackend.Repositories.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Settings of Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.Debug()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Swagger settings
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "My API", Version = "v1" });
    options.EnableAnnotations();
    options.IncludeXmlComments(xmlPath);
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// JWT settings
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

builder.Services.AddScoped<IAccount, Account>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<AuthenticationStateProvider, AuthenticationProvider>();
builder.Services.AddScoped<IProductRepo, ProductRepo>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryRepo, CategoryRepo>();
builder.Services.AddScoped<IHistoryRepo, HistoryRepo>();
builder.Services.AddHttpClient<IAccountService, AccountService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5111");
});
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

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

// while dev
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Создаем администратора, если его нет
        if (!dbContext.Users.Any(u => u.Role == UserRole.Admin))
        {
            var adminUser = new ApplicationUser
            {
                Name = "Admin",
                Email = "admin@example.com",
                Role = UserRole.Admin,
                Phone = "1234567890",
                DateOfBirth = new DateOnly(2001, 1, 1),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("AdminPassword123!")
            };

            dbContext.Users.Add(adminUser);
            dbContext.SaveChanges();
        }

        // Добавляем предопределенные категории, если их еще нет
        if (!dbContext.Categories.Any(c => c.Name == "Продукты"))
        {
            dbContext.Categories.Add(new Category
            {
                Name = "Продукты",
                Type = "Product",
                UserId = null
            });
        }

        if (!dbContext.Categories.Any(c => c.Name == "Медикаменты"))
        {
            dbContext.Categories.Add(new Category
            {
                Name = "Медикаменты",
                Type = "Medication",
                UserId = null
            });
        }

        if (!dbContext.Categories.Any(c => c.Name == "Книги"))
        {
            dbContext.Categories.Add(new Category
            {
                Name = "Книги",
                Type = "Book",
                UserId = null
            });
        }

        dbContext.SaveChanges();
    }
}


app.Run();

Log.CloseAndFlush();
