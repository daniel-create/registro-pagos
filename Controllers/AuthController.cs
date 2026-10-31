using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

public record LoginRequest(string DNI, string Clave);

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<Usuario> usuarios, IConfiguration config) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var usuario = await usuarios.FindByNameAsync(req.DNI);
        if (usuario is null)
            return Unauthorized(new{mensaje = "DNI o clave incorrectos"});

        if (await usuarios.IsLockedOutAsync(usuario))
            return StatusCode(423,new{mensaje = "Cuenta bloqueada por intentos fallidos. Espera 5 minutos"});

        if (!await usuarios.CheckPasswordAsync(usuario, req.Clave))
        {
            await usuarios.AccessFailedAsync(usuario);
            return Unauthorized(new {mensaje = "DNI o clave incorrectos"});
        }

        await usuarios.ResetAccessFailedCountAsync(usuario); //reinicia a cero el contador de intentos fallidos de sesion

        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Clave"]!)); // obtener clave de conf y pasarla a byte
        var descriptor = new SecurityTokenDescriptor // construir clave simetrica
        {
            Subject = new ClaimsIdentity(new[] //crea identidad compuesta por varios claim 
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id),
                new Claim(ClaimTypes.Name, usuario.NombreCompleto),
                new Claim("dni",usuario.UserName ?? "")
            }),

            Issuer = "PrestamosApi", //quien creo el token
            Audience = "PrestamosApi", //para quien es el token
            Expires = DateTime.UtcNow.AddHours(12), //cuando caduca el token
            SigningCredentials = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256) //firma del token
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return Ok(new {token, nombre = usuario.NombreCompleto});


    }
}

