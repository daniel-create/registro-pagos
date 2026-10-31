namespace PrestamosApi.Models;

public class Auditoria
{
    public long Id {get;set; }
    public DateTime FechaUtc {get;set; }=DateTime.UtcNow;
    public string? UsuarioId {get;set; }
    public string? UsuarioNombre {get;set; } // ? : indica que la propiedad puede contener null, es decir, no tener un valor.
    public string Tabla {get;set; } = "";
    public string Accion {get;set; } = "";
    public string? RegistroId {get;set; }
    public string? Cambios {get;set; }
}