using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

[ApiController]
[Authorize]
[Route("api/panel")]
public class PanelController(AppDbContext db) : ControllerBase
{
    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen()
    {
        var aportado = await db.Aportes.SumAsync(a => a.Monto);
        var prestado = await db.Prestamos
            .Where(p => p.Estado == EstadoPrestamo.Activo)
            .SumAsync(p => p.CapitalPendiente);
        var interesesGanados = await db.Pagos.SumAsync(p => p.AInteresMenor);
        var cobradoAClientes = await db.Pagos.SumAsync(p => p.AInteres);

        return Ok(new
        {
            capitalAportado = aportado,
            capitalPrestado = prestado,
            capitalDisponible = aportado - prestado,
            interesesGanados,
            cobradoAClientes,
            capitalMasGanancia = aportado + interesesGanados
        });
    }
}