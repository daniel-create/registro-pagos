using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;
using PrestamosApi.Services;

namespace PrestamosApi.Controllers;

public record SimulacionDatos(decimal Capital, decimal InteresMayor, decimal InteresMenor, int Cuotas);
public record PrestamoFijoDatos(int ClienteId, decimal Capital, decimal InteresMayor, decimal InteresMenor, int Cuotas, DateOnly? FechaInicio);
public record PagoDatos(decimal Monto);
public record RegistrarPagoDatos(decimal Monto, string Metodo, Guid ClaveUnica);
public record PrestamoMensualDatos(int ClienteId, decimal Capital, decimal InteresMayor, decimal InteresMenor, DateOnly? FechaInicio);
public record EditarPrestamoDatos(int ClienteId, decimal Capital, decimal InteresMayor, decimal InteresMenor, int? Cuotas, DateOnly FechaInicio);

[ApiController]
[Authorize]
[Route("api/prestamos")]
public class PrestamosController(AppDbContext db) : ControllerBase
{
    [HttpPost("simular")]
    public IActionResult Simular(SimulacionDatos datos)
    {
        try
        {
            return Ok(Calculadora.CuotasFijas(datos.Capital, datos.InteresMayor, datos.InteresMenor, datos.Cuotas));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }
    [HttpGet]
    public async Task<IActionResult> Listar(string? estado)
    {
        var consulta = db.Prestamos.AsNoTracking()//Crear la consulta
            .Include(p => p.Cliente)//Incluir los datos del cliente
            .Include(p => p.Periodos)//Incluir los períodos del préstamo
            .AsQueryable();

        if (Enum.TryParse<EstadoPrestamo>(estado, true, out var e))//permite filtrar los préstamos según el estado que se recibe como parámetro, siempre que sea válido.
            consulta = consulta.Where(p => p.Estado == e);
        var prestamos = await consulta.OrderByDescending(p => p.Id).ToListAsync();
        var conPagos = await db.Pagos.Select(x => x.PrestamoId).Distinct().ToListAsync();

        return Ok(prestamos.Select(p =>
        {
            var cuota = CuotaActual(p);
            return new
            {
                p.Id,
                cliente = p.Cliente.Nombre,
                tipo = p.Tipo.ToString(),
                estado = p.Estado.ToString(),
                p.CapitalOriginal,
                p.CapitalPendiente,
                p.TasaMensual,
                p.FechaInicio,
                proximoVence = p.Estado == EstadoPrestamo.Activo ? cuota?.FechaVence : null,
                proximoInteres = p.Estado == EstadoPrestamo.Activo ? cuota?.InteresGenerado : null,
                tienePagos = conPagos.Contains(p.Id)
            };
        }));

    }


    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var p = await db.Prestamos.AsNoTracking()
            .Include(x => x.Cliente)
            .Include(x => x.Periodos)
            .FirstOrDefaultAsync(x => x.Id == id);

        return p is null ? NotFound(new { mensaje = "Préstamo no encontrado" }) : Ok(Respuesta(p));
    }

    [HttpPost]
    public async Task<IActionResult> Crear(PrestamoFijoDatos datos)
    {
        var cliente = await db.Clientes.FindAsync(datos.ClienteId);
        if (cliente is null)
            return BadRequest(new { mensaje = "El cliente no existe" });

        if (datos.InteresMenor < 0 || datos.InteresMayor < datos.InteresMenor)
            return BadRequest(new { mensaje = "El interés mayor debe ser igual o mayor que el menor" });

        if (datos.Cuotas > 120)
            return BadRequest(new { mensaje = "Máximo 120 cuotas" });

        CronogramaFijo plan;
        try
        {
            plan = Calculadora.CuotasFijas(datos.Capital, datos.InteresMayor, datos.InteresMenor, datos.Cuotas);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }

        var inicio = datos.FechaInicio ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

        var prestamo = new Prestamo
        {
            Cliente = cliente,
            Tipo = TipoPrestamo.CuotasFijas,
            CapitalOriginal = datos.Capital,
            CapitalPendiente = datos.Capital,
            TasaMensual = datos.InteresMayor,
            TasaMenor = datos.InteresMenor,
            NumeroCuotas = datos.Cuotas,
            DiaPago = inicio.Day,
            FechaInicio = inicio,
            Estado = EstadoPrestamo.Activo,
            Periodos = plan.Cuotas.Select(c => new Periodo
            {
                Numero = c.Numero,
                FechaVence = inicio.AddMonths(c.Numero - 1),
                InteresGenerado = c.InteresMenor + c.Diferencia,
                InteresMenor = c.InteresMenor,
                Diferencia = c.Diferencia,
                CapitalProgramado = c.Capital
            }).ToList()
        };

        db.Prestamos.Add(prestamo);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Obtener), new { id = prestamo.Id }, Respuesta(prestamo));
    }

    [HttpPost("mensual")]
    public async Task<IActionResult> CrearMensual(PrestamoMensualDatos datos)
    {
        var cliente = await db.Clientes.FindAsync(datos.ClienteId);
        if (cliente is null)
            return BadRequest(new { mensaje = "El cliente no existe" });
        if (datos.Capital <= 0)
            return BadRequest(new { mensaje = "El capital debe ser mayor que cero" });
        if (decimal.Round(datos.Capital, 2) != datos.Capital)
            return BadRequest(new { mensaje = "El capital debe tener máximo 2 decimales" });
        if (datos.InteresMenor < 0 || datos.InteresMayor < datos.InteresMenor)
            return BadRequest(new { mensaje = "El interés mayor debe ser igual o mayor que el menor" });

        var inicio = datos.FechaInicio ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
        var primera = Calculadora.CuotaSobre(datos.Capital, datos.InteresMayor, datos.InteresMenor);

        var prestamo = new Prestamo
        {
            Cliente = cliente,
            Tipo = TipoPrestamo.MensualFlexible,
            CapitalOriginal = datos.Capital,
            CapitalPendiente = datos.Capital,
            TasaMensual = datos.InteresMayor,
            TasaMenor = datos.InteresMenor,
            DiaPago = inicio.Day,
            FechaInicio = inicio,
            Estado = EstadoPrestamo.Activo,
            Periodos = new List<Periodo>
            {
                new Periodo
                {
                    Numero = 1,
                    FechaVence = inicio,
                    InteresGenerado = primera.InteresMenor + primera.Diferencia,
                    InteresMenor = primera.InteresMenor,
                    Diferencia = primera.Diferencia,
                    CapitalProgramado = 0
                }
            }
        };

        db.Prestamos.Add(prestamo);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Obtener), new { id = prestamo.Id }, Respuesta(prestamo));
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Editar(int id, EditarPrestamoDatos datos)
    {
        var p = await db.Prestamos.Include(x => x.Periodos).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null)
            return NotFound(new { mensaje = "Préstamo no encontrado" });
        if (await db.Pagos.AnyAsync(x => x.PrestamoId == id))
            return Conflict(new { mensaje = "Este préstamo ya tiene pagos y no se puede editar" });
        if (p.Estado != EstadoPrestamo.Activo)
            return BadRequest(new { mensaje = "El préstamo no está activo" });

        var cliente = await db.Clientes.FindAsync(datos.ClienteId);
        if (cliente is null)
            return BadRequest(new { mensaje = "El cliente no existe" });
        if (datos.Capital <= 0)
            return BadRequest(new { mensaje = "El capital debe ser mayor que cero" });
        if (decimal.Round(datos.Capital, 2) != datos.Capital)
            return BadRequest(new { mensaje = "El capital debe tener máximo 2 decimales" });
        if (datos.InteresMenor < 0 || datos.InteresMayor < datos.InteresMenor)
            return BadRequest(new { mensaje = "El interés mayor debe ser igual o mayor que el menor" });

        var inicio = datos.FechaInicio;
        int? numeroCuotas = null;
        List<Periodo> nuevos;

        if (p.Tipo == TipoPrestamo.CuotasFijas)
        {
            var cuotas = datos.Cuotas ?? 0;
            if (cuotas > 120)
                return BadRequest(new { mensaje = "Máximo 120 cuotas" });

            CronogramaFijo plan;
            try
            {
                plan = Calculadora.CuotasFijas(datos.Capital, datos.InteresMayor, datos.InteresMenor, cuotas);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }

            numeroCuotas = cuotas;
            nuevos = plan.Cuotas.Select(c => new Periodo
            {
                Numero = c.Numero,
                FechaVence = inicio.AddMonths(c.Numero - 1),
                InteresGenerado = c.InteresMenor + c.Diferencia,
                InteresMenor = c.InteresMenor,
                Diferencia = c.Diferencia,
                CapitalProgramado = c.Capital
            }).ToList();
        }
        else
        {
            var primera = Calculadora.CuotaSobre(datos.Capital, datos.InteresMayor, datos.InteresMenor);
            nuevos = new List<Periodo>
            {
                new Periodo
                {
                    Numero = 1,
                    FechaVence = inicio,
                    InteresGenerado = primera.InteresMenor + primera.Diferencia,
                    InteresMenor = primera.InteresMenor,
                    Diferencia = primera.Diferencia,
                    CapitalProgramado = 0
                }
            };
        }

        await using var transaccion = await db.Database.BeginTransactionAsync();

        db.Periodos.RemoveRange(p.Periodos);
        p.ClienteId = cliente.Id;
        p.CapitalOriginal = datos.Capital;
        p.CapitalPendiente = datos.Capital;
        p.TasaMensual = datos.InteresMayor;
        p.TasaMenor = datos.InteresMenor;
        p.NumeroCuotas = numeroCuotas;
        p.DiaPago = inicio.Day;
        p.FechaInicio = inicio;
        await db.SaveChangesAsync();

        foreach (var n in nuevos)
        {
            n.PrestamoId = p.Id;
            db.Periodos.Add(n);
        }
        await db.SaveChangesAsync();
        await transaccion.CommitAsync();

        var actualizado = await db.Prestamos.AsNoTracking()
            .Include(x => x.Cliente)
            .Include(x => x.Periodos)
            .FirstAsync(x => x.Id == id);

        return Ok(Respuesta(actualizado));
    }

    [HttpPost("{id:int}/pagos/vista-previa")]
    public async Task<IActionResult> VistaPreviaPago(int id, PagoDatos datos)
    {
        var p = await db.Prestamos.AsNoTracking()
            .Include(x => x.Periodos)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p is null)
            return NotFound(new { mensaje = "Préstamo no encontrado" });
        if (p.Estado != EstadoPrestamo.Activo)
            return BadRequest(new { mensaje = "El préstamo no está activo" });

        var cuota = CuotaActual(p);
        if (cuota is null)
            return BadRequest(new { mensaje = "No hay cuotas pendientes" });

        try
        {
            var r = Calculadora.RepartirPago(
                datos.Monto, cuota.InteresMenor, cuota.Diferencia, cuota.CapitalProgramado,
                p.CapitalPendiente, p.TasaMensual, p.TasaMenor ?? 0);

            var efecto = r.Efecto.ToString();
            var cuotaNueva = r.CuotaNueva;

            if (p.Tipo == TipoPrestamo.MensualFlexible)
            {
                var cancela = r.Efecto == EfectoPago.Cancela;
                efecto = cancela ? "Cancela" : "Sigue";
                cuotaNueva = cancela ? null : Calculadora.CuotaSobre(r.CapitalRestante, p.TasaMensual, p.TasaMenor ?? 0);
            }

            return Ok(new
            {
                cuota = cuota.Numero,
                vence = cuota.FechaVence,
                minimo = cuota.InteresMenor + cuota.Diferencia,
                interes = r.Interes,
                interesMenor = r.InteresMenor,
                diferencia = r.Diferencia,
                capital = r.Capital,
                capitalRestante = r.CapitalRestante,
                efecto,
                cuotaNueva
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPost("{id:int}/pagos")]
    public async Task<IActionResult> RegistrarPago(int id, RegistrarPagoDatos datos)
    {
        if (decimal.Round(datos.Monto, 2) != datos.Monto)
            return BadRequest(new { mensaje = "El monto debe tener máximo 2 decimales" });
        if (!Enum.TryParse<MetodoPago>(datos.Metodo, true, out var metodo) || !Enum.IsDefined(metodo))
            return BadRequest(new { mensaje = "Método de pago no válido (Efectivo, Transferencia, Yape, Plin u Otro)" });
        if (datos.ClaveUnica == Guid.Empty)
            return BadRequest(new { mensaje = "Falta la clave única del pago" });
        if (await db.Pagos.AnyAsync(x => x.ClaveUnica == datos.ClaveUnica))
            return Conflict(new { mensaje = "Este pago ya fue registrado" });

        var p = await db.Prestamos.Include(x => x.Periodos).FirstOrDefaultAsync(x => x.Id == id);

        if (p is null)
            return NotFound(new { mensaje = "Préstamo no encontrado" });
        if (p.Estado != EstadoPrestamo.Activo)
            return BadRequest(new { mensaje = "El préstamo no está activo" });

        var cuota = CuotaActual(p);
        if (cuota is null)
            return BadRequest(new { mensaje = "No hay cuotas pendientes" });

        RepartoPago r;
        try
        {
            r = Calculadora.RepartirPago(
                datos.Monto, cuota.InteresMenor, cuota.Diferencia, cuota.CapitalProgramado,
                p.CapitalPendiente, p.TasaMensual, p.TasaMenor ?? 0);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }

        var nombre = User.FindFirstValue(ClaimTypes.Name) ?? "desconocido";
        var dni = User.FindFirstValue("dni");
        var registradoPor = dni is null ? nombre : $"{nombre} ({dni})";

        db.Pagos.Add(new Pago
        {
            PrestamoId = p.Id,
            PeriodoId = cuota.Id,
            Fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)),
            Monto = datos.Monto,
            AMora = 0,
            AInteres = r.Interes,
            AInteresMenor = r.InteresMenor,
            ADiferencia = r.Diferencia,
            ACapital = r.Capital,
            Metodo = metodo,
            RegistradoPor = registradoPor,
            ClaveUnica = datos.ClaveUnica
        });

        var mensual = p.Tipo == TipoPrestamo.MensualFlexible;
        var siguienteNumero = p.Periodos.Max(c => c.Numero) + 1;
        var futuras = p.Periodos.Where(c => c.InteresPagado == 0 && c.Id != cuota.Id).ToList();
        var capitalProgramado = cuota.CapitalProgramado;

        cuota.InteresPagado = r.Interes;
        cuota.CapitalPagado = r.Capital;
        p.CapitalPendiente = r.CapitalRestante;

        if (mensual)
        {
            cuota.CapitalProgramado = r.Capital;
            if (r.Efecto == EfectoPago.Cancela)
            {
                p.Estado = EstadoPrestamo.Cancelado;
            }
            else
            {
                var siguiente = Calculadora.CuotaSobre(r.CapitalRestante, p.TasaMensual, p.TasaMenor ?? 0);
                p.Periodos.Add(new Periodo
                {
                    Numero = siguienteNumero,
                    FechaVence = cuota.FechaVence.AddMonths(1),
                    InteresGenerado = siguiente.InteresMenor + siguiente.Diferencia,
                    InteresMenor = siguiente.InteresMenor,
                    Diferencia = siguiente.Diferencia,
                    CapitalProgramado = 0
                });
            }
        }
        else
        {
            switch (r.Efecto)
            {
                case EfectoPago.Desplaza:
                    cuota.CapitalProgramado = 0;
                    foreach (var f in futuras)
                        f.FechaVence = f.FechaVence.AddMonths(1);
                    p.Periodos.Add(new Periodo
                    {
                        Numero = siguienteNumero,
                        FechaVence = cuota.FechaVence.AddMonths(1),
                        InteresGenerado = cuota.InteresGenerado,
                        InteresMenor = cuota.InteresMenor,
                        Diferencia = cuota.Diferencia,
                        CapitalProgramado = capitalProgramado
                    });
                    break;

                case EfectoPago.Continua:
                    break;

                case EfectoPago.Desarma:
                    cuota.CapitalProgramado = r.Capital;
                    db.Periodos.RemoveRange(futuras);
                    var nueva = r.CuotaNueva!;
                    p.Periodos.Add(new Periodo
                    {
                        Numero = siguienteNumero,
                        FechaVence = cuota.FechaVence.AddMonths(1),
                        InteresGenerado = nueva.InteresMenor + nueva.Diferencia,
                        InteresMenor = nueva.InteresMenor,
                        Diferencia = nueva.Diferencia,
                        CapitalProgramado = nueva.Capital
                    });
                    break;

                case EfectoPago.Cancela:
                    cuota.CapitalProgramado = r.Capital;
                    db.Periodos.RemoveRange(futuras);
                    p.Estado = EstadoPrestamo.Cancelado;
                    break;
            }
        }

        await db.SaveChangesAsync();

        var actualizado = await db.Prestamos.AsNoTracking()
            .Include(x => x.Cliente)
            .Include(x => x.Periodos)
            .FirstAsync(x => x.Id == id);

        var efectoTexto = mensual
            ? (r.Efecto == EfectoPago.Cancela ? "Cancela" : "Sigue")
            : r.Efecto.ToString();

        return Ok(new { efecto = efectoTexto, prestamo = Respuesta(actualizado) });
    }

    // La cuota que se está pagando: la primera, por fecha, cuyo interés todavía no se pagó.
    private static Periodo? CuotaActual(Prestamo p) =>
        p.Periodos.OrderBy(c => c.FechaVence).ThenBy(c => c.Numero).FirstOrDefault(c => c.InteresPagado == 0);

    private static object Respuesta(Prestamo p) => new
    {
        p.Id,
        p.ClienteId,
        Cliente = p.Cliente?.Nombre,
        Tipo = p.Tipo.ToString(),
        p.CapitalOriginal,
        p.CapitalPendiente,
        p.TasaMensual,
        p.TasaMenor,
        p.NumeroCuotas,
        p.DiaPago,
        p.FechaInicio,
        Estado = p.Estado.ToString(),
        Cuotas = p.Periodos.OrderBy(c => c.FechaVence).ThenBy(c => c.Numero).Select(c => new
        {
            c.Numero,
            c.FechaVence,
            c.CapitalProgramado,
            c.InteresMenor,
            c.Diferencia,
            c.InteresGenerado,
            c.InteresPagado,
            c.CapitalPagado
        })
    };
}
