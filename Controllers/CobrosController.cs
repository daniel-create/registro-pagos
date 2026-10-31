using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;
[ApiController]
[Authorize]
[Route("api/cobros")]
public class CobrosController(AppDbContext db) : ControllerBase
{
    //sin parametros: lo que vence hoy o esta atrasado. Con ?hasta=2026-12-31: lo que vence hasta esa fecha.
    [HttpGet]
    public async Task<IActionResult> Listar(DateOnly? hasta)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)); //Obtener la fecha de hoy
        var limite = hasta ?? hoy; //Definir una fecha límite

        var prestamos = await db.Prestamos.AsNoTracking() //Consultar los préstamos
            .Include(p => p.Cliente) //Incluir los datos del cliente
            .Include(p => p.Periodos)//Incluir los períodos del préstamo
            .Where(p => p.Estado == EstadoPrestamo.Activo) //Filtrar los préstamos activos
            .ToListAsync(); //Ejecutar la consulta y obtener la lista
        
        var lista = prestamos //var lista = prestamos
            .Select(p => new
            {
                Prestamo = p, //Guardar el préstamo original
                Cuota = p.Periodos //Buscar la cuota del préstamo
                    .OrderBy(c => c.FechaVence).ThenBy(c => c.Numero)//Ordenar por fecha de vencimiento//thenby:Si las fechas son iguales, ordenar por número
                    .FirstOrDefault(c => c.InteresPagado == 0)
            })
            .Where(x => x.Cuota is not null && x.Cuota.FechaVence <= limite)
            .OrderBy(x => x.Cuota!.FechaVence)//Considera que Cuota no es nula en este punto
            .Select(x => new
            {
                prestamoId = x.Prestamo.Id,
                cliente = x.Prestamo.Cliente.Nombre,
                telefono = x.Prestamo.Cliente.Telefono,
                tipo = x.Prestamo.Tipo.ToString(),
                vence = x.Cuota!.FechaVence,
                diasAtraso = Math.Max(0, hoy.DayNumber - x.Cuota.FechaVence.DayNumber),
                interes = x.Cuota.InteresGenerado,
                capitalProgramado = x.Cuota.CapitalProgramado,
                totalCuota = x.Cuota.InteresGenerado + x.Cuota.CapitalProgramado,
                capitalPendiente = x.Prestamo.CapitalPendiente
            })
            .ToList();
        return Ok(lista);
    }
}