using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PrestamosApi.Data;
using PrestamosApi.Models;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor(); //el vigilante puede ver quien hizo la peticion
builder.Services.AddScoped<AuditoriaInterceptor>(); //crea un vigilante nuevo para cada peticion y no se mezcle con otra persona

builder.Services.AddDbContext<AppDbContext>((sp, o) => o
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    .AddInterceptors(sp.GetRequiredService<AuditoriaInterceptor>())); //AddInterceptors(...) es lo que engancha al vigilante al guardado de la base de datos.

builder.Services.AddIdentityCore<Usuario>(o =>
{
    o.Password.RequiredLength = 10;
    o.Password.RequireDigit = true;
    o.Password.RequireUppercase = false;
    o.Password.RequireNonAlphanumeric = false;
    o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
}).AddEntityFrameworkStores<AppDbContext>(); 

var jwtClave = builder.Configuration["Jwt:Clave"]
    ?? throw new InvalidOperationException("Falta la configuracion Jwt:Clave");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "PrestamosApi",
            ValidateAudience = true,
            ValidAudience = "PrestamosApi",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtClave)),
            ClockSkew = TimeSpan.Zero
        };
    });

var app = builder.Build();

// Comando para crear cuentas: dotnet run -- crear-usuario
if (args.Length > 0 && args[0] == "crear-usuario")
{
    using var scope = app.Services.CreateScope();
    var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

    Console.Write("DNI: ");
    var dni = (Console.ReadLine() ?? "").Trim(); //?? "": si el resultado es null, utiliza una cadena vacía.
    Console.Write("Nombre Completo: ");
    var nombre = (Console.ReadLine() ?? "").Trim();//.Trim(): elimina los espacios en blanco al principio y al final.
    Console.Write("Correo: ");
    var correo = (Console.ReadLine() ?? "").Trim();
    Console.Write("Clave (no se muestra): ");

    var clave = "";
    ConsoleKeyInfo tecla;
    while ((tecla = Console.ReadKey(true)).Key != ConsoleKey.Enter)
    {
        if (tecla.Key == ConsoleKey.Backspace && clave.Length > 0)
            clave = clave[..^1];//obtiene toda la cadena excepto el último carácter.
        else if (!char.IsControl(tecla.KeyChar)) //si el carácter NO es un carácter de control(tab,Enter)
            clave += tecla.KeyChar;
    }
    Console.WriteLine();

    var nuevo = new Usuario { UserName = dni, NombreCompleto = nombre, Email = correo};
    var resultado = await usuarios.CreateAsync(nuevo, clave);

    Console.WriteLine(resultado.Succeeded
        ? "Usuario creado. "
        : "Error: " + string.Join("; ", resultado.Errors.Select(e => e.Description))); //.Select(e => e.Description) : extrae la descripción de cada error. e representa cada error de la lista
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}   

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
