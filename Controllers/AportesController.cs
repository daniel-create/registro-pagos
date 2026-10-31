using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;
public record AporteDatos(string Nombre, decimal Monto, DateOnly Fecha);

[ApiController]
[Authorize]
[Route("api/aportes")]

public class AportesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var aportes = await db.Aportes.AsNoTracking()
            .OrderBy(a => a.Fecha).ThenBy(a => a.Id)
            .Select(a => new{ a.Id, a.Nombre, a.Monto, a.Fecha})
            .ToListAsync(); 
        return Ok(new {total = aportes.Sum(a => a.Monto), aportes});
    }

    [HttpPost]
    public async Task<IActionResult> Crear(AporteDatos datos)
    {
        var error = Validar(datos);
        if (error is not null)
            return BadRequest(new {mensaje = error});
        var aporte = new Aporte {Nombre = datos.Nombre.Trim(), Monto = datos.Monto, Fecha = datos.Fecha};
        db.Aportes.Add(aporte);
        await db.SaveChangesAsync();

        return Ok(new {aporte.Id, aporte.Nombre, aporte.Monto, aporte.Fecha});

    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Modificar(int id, AporteDatos datos)
    {
        var aporte = await db.Aportes.FindAsync(id);
        if(aporte is null)
            return NotFound(new {mensaje = "Aporte no encontrado"});
        
        var error = Validar(datos);
        if(error is not null)
            return BadRequest(new {mensaje = error});

        aporte.Nombre = datos.Nombre.Trim();
        aporte.Monto = datos.Monto;
        aporte.Fecha = datos.Fecha;
        await db.SaveChangesAsync();

        return Ok(new { aporte.Id, aporte.Nombre, aporte.Monto, aporte.Fecha });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var aporte = await db.Aportes.FindAsync(id);
        if (aporte is null)
            return NotFound(new { mensaje = "Aporte no encontrado" });

        db.Aportes.Remove(aporte);
        await db.SaveChangesAsync();

        return Ok(new { mensaje = "Aporte eliminado" });
    }
    private static string? Validar(AporteDatos datos)
    {
        var nombre = datos.Nombre?.Trim() ?? "";
        if (nombre.Length == 0 || nombre.Length > 150)
            return "El nombre es obligatorio (máximo 150 caracteres)";
        if (datos.Monto <= 0)
            return "El monto debe ser mayor que cero";
        if (decimal.Round(datos.Monto, 2) != datos.Monto)
            return "El monto debe tener máximo 2 decimales";
        if (datos.Fecha == default)
            return "La fecha es obligatoria";
        return null;
    }
}
