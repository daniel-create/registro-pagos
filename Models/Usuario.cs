using Microsoft.AspNetCore.Identity;

namespace PrestamosApi.Models;

public class Usuario : IdentityUser
{
    public string NombreCompleto { get; set; } = "";
}