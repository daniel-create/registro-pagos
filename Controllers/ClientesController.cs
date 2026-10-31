using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

public record ClienteDatos(string Nombre, string? Documento, string? Telefono, string? Direccion, string? Referido);

[ApiController]
[Authorize]
[Route("api/clientes")]
public class ClientesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(string? buscar, string? referido)
    {
        var consulta = db.Clientes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = buscar.Trim().ToLower();
            consulta = consulta.Where(c =>
                c.Nombre.ToLower().Contains(b) ||
                (c.Documento != null && c.Documento.Contains(b)) ||
                (c.Telefono != null && c.Telefono.Contains(b)) ||
                (c.Referido != null && c.Referido.ToLower().Contains(b)));
        }

        if (!string.IsNullOrWhiteSpace(referido))
        {
            var r = referido.Trim().ToLower();
            consulta = r == "propio"
                ? consulta.Where(c => c.Referido == null)
                : consulta.Where(c => c.Referido != null && c.Referido.ToLower() == r);
        }

        var lista = await consulta
            .OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Nombre, c.Documento, c.Telefono, c.Direccion, c.Referido, c.CreadoEn })
            .ToListAsync();

        return Ok(lista);
    }

    // Los nombres de referido que ya se usaron, para las sugerencias y el filtro.
    [HttpGet("referidos")]
    public async Task<IActionResult> Referidos()
    {
        var lista = await db.Clientes.AsNoTracking()
            .Where(c => c.Referido != null)
            .Select(c => c.Referido!)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();

        return Ok(lista);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var c = await db.Clientes.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Nombre, x.Documento, x.Telefono, x.Direccion, x.Referido, x.CreadoEn })
            .FirstOrDefaultAsync();

        return c is null ? NotFound(new { mensaje = "Cliente no encontrado" }) : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(ClienteDatos datos)
    {
        var nombre = datos.Nombre?.Trim() ?? "";
        if (nombre.Length == 0 || nombre.Length > 150)
            return BadRequest(new { mensaje = "El nombre es obligatorio (máximo 150 caracteres)" });

        var documento = string.IsNullOrWhiteSpace(datos.Documento) ? null : datos.Documento.Trim();
        if (documento is not null && await db.Clientes.AnyAsync(c => c.Documento == documento))
            return Conflict(new { mensaje = "Ya existe un cliente con ese documento" });

        var (referido, errorReferido) = await NormalizarReferido(datos.Referido);
        if (errorReferido is not null)
            return BadRequest(new { mensaje = errorReferido });

        var cliente = new Cliente
        {
            Nombre = nombre,
            Documento = documento,
            Telefono = string.IsNullOrWhiteSpace(datos.Telefono) ? null : datos.Telefono.Trim(),
            Direccion = string.IsNullOrWhiteSpace(datos.Direccion) ? null : datos.Direccion.Trim(),
            Referido = referido
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Obtener), new { id = cliente.Id },
            new { cliente.Id, cliente.Nombre, cliente.Documento, cliente.Telefono, cliente.Direccion, cliente.Referido, cliente.CreadoEn });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Modificar(int id, ClienteDatos datos)
    {
        var cliente = await db.Clientes.FindAsync(id);
        if (cliente is null)
            return NotFound(new { mensaje = "Cliente no encontrado" });

        var nombre = datos.Nombre?.Trim() ?? "";
        if (nombre.Length == 0 || nombre.Length > 150)
            return BadRequest(new { mensaje = "El nombre es obligatorio (máximo 150 caracteres)" });

        var documento = string.IsNullOrWhiteSpace(datos.Documento) ? null : datos.Documento.Trim();
        if (documento is not null && await db.Clientes.AnyAsync(c => c.Documento == documento && c.Id != id))
            return Conflict(new { mensaje = "Ya existe otro cliente con ese documento" });

        var (referido, errorReferido) = await NormalizarReferido(datos.Referido);
        if (errorReferido is not null)
            return BadRequest(new { mensaje = errorReferido });

        cliente.Nombre = nombre;
        cliente.Documento = documento;
        cliente.Telefono = string.IsNullOrWhiteSpace(datos.Telefono) ? null : datos.Telefono.Trim();
        cliente.Direccion = string.IsNullOrWhiteSpace(datos.Direccion) ? null : datos.Direccion.Trim();
        cliente.Referido = referido;

        await db.SaveChangesAsync();
        return Ok(new { cliente.Id, cliente.Nombre, cliente.Documento, cliente.Telefono, cliente.Direccion, cliente.Referido, cliente.CreadoEn });
    }

    // Quita espacios de más y reutiliza la escritura de un referido que ya existe (sin importar mayúsculas).
    // Vacío o "Propio" significa que el cliente es tuyo.
    private async Task<(string? valor, string? error)> NormalizarReferido(string? entrada)
    {
        var limpio = string.Join(' ', (entrada ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (limpio.Length == 0 || limpio.Equals("propio", StringComparison.OrdinalIgnoreCase))
            return (null, null);
        if (limpio.Length > 150)
            return (null, "El referido puede tener máximo 150 caracteres");

        var minuscula = limpio.ToLower();
        var existente = await db.Clientes
            .Where(c => c.Referido != null && c.Referido.ToLower() == minuscula)
            .Select(c => c.Referido)
            .FirstOrDefaultAsync();

        return (existente ?? limpio, null);
    }
}