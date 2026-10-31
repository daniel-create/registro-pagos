using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PrestamosApi.Models;

namespace PrestamosApi.Data;

public class AuditoriaInterceptor(IHttpContextAccessor http) : SaveChangesInterceptor //Sirve para obtener información de la solicitud web, como el usuario que está autenticado.
{
    private readonly List<Pendiente> _pendientes = new(); //crea una lista para almacenar objetos de tipo Pendiente
    private bool _guardando;

    private class Pendiente //private: significa que esta clase solo puede utilizarse directamente dentro de la clase que la contiene, en este caso, AuditoriaInterceptor
    {
        public EntityEntry Entrada {get; init; } = null!;
        public string Tabla {get; init; } = "";
        public string Accion {get; init; } = "";
        public string? RegistroId {get; init; }
        public string? Cambios {get; init; }
    }

    private static string? IdDe(EntityEntry e) => e.Property("Id").CurrentValue?.ToString(); //crea un método llamado IdDe que sirve para obtener el identificador (Id) de una entidad que Entity Framework Core está rastreando y convertirlo en texto.

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync( //guarda los cambios antes de que los cambios se guarden en la base de datos.
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        _pendientes.Clear();
        var contexto = eventData.Context; // comprueba si existe Entity Framework Core y evita el reproceso de cambios
        if (contexto is null || _guardando) // _guardando evita que se pueda volver a ejecutar otra auditoria de la auditoria
            return base.SavingChangesAsync(eventData, result, ct);

        foreach (var e in contexto.ChangeTracker.Entries()) //recorre cada linea para revisar que pasa antes de guardar cambios
        {
            if(e.Entity is Auditoria && (e.State == EntityState.Modified || e.State == EntityState.Deleted)) //comprueba si es un registro y si esta siendo modificado o eliminado
                throw new InvalidOperationException("Los registros de auditoría no se pueden modificar ni borrar.");

            if (e.Entity is not (Cliente or Prestamo or Periodo or Pago or Aporte)) continue;  //si la entidad no es cliente, ... pasa a la sgte
            if (e.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue; // si la entidad no esta siendo agregada pasa a la sgte

            var cambios = new Dictionary<string, object?>(); //creamos diccionario
            foreach (var p in e.Properties) //recorre una por una propiedad de entidad actual
            {
                if(e.State == EntityState.Added)//se pregunta si la entidad se agrega por primera vez a la bd
                {
                    if(p.Metadata.IsPrimaryKey()) continue; //comprueba si la entidad es clave primaria de la entidad, si es devuelve true
                    cambios[p.Metadata.Name] = p.CurrentValue;//guarda el nombre y el valor en cambios
                }
                else if (e.State == EntityState.Deleted) //si la entidad esta marcada para ser eliminada , ejecuta el codigo
                {
                    cambios[p.Metadata.Name] = p.OriginalValue; //guarda el valor original en cambios
                }
                else if (p.IsModified)// si fue modificada ejecuta el codigo dentro de llaves
                {
                    cambios[p.Metadata.Name] = new { antes = p.OriginalValue, despues = p.CurrentValue}; //crea objeto anonimo que contiene 2 valores
                }
            }
            if (e.State == EntityState.Modified && cambios.Count == 0) continue; // si esta marcada como modificada pero no se registro cambios

            _pendientes.Add(new Pendiente
            {
                Entrada = e,
                Tabla = e.Metadata.GetTableName() ?? e.Metadata.ClrType.Name,
                Accion = e.State == EntityState.Added ? "Crear"
                       : e.State == EntityState.Modified ? "Modificar" : "Borrar",
                RegistroId = e.State == EntityState.Added ? null : IdDe(e),
                Cambios = JsonSerializer.Serialize(cambios)       

            });

        }
        return base.SavingChangesAsync(eventData, result, ct);

    }

    public override async ValueTask<int> SavedChangesAsync(
       SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        var contexto = eventData.Context;
        if (contexto is not null && !_guardando && _pendientes.Count > 0)
        {
            var usuario = http.HttpContext?.User;//obtener usuario actual
            var usuarioId = usuario?.FindFirstValue(ClaimTypes.NameIdentifier);//obtener identificador
            var usuarioNombre = usuario?.FindFirstValue(ClaimTypes.Name);//obtener el nombre

            foreach (var p in _pendientes)
            {
                contexto.Set<Auditoria>().Add(new Auditoria
                {
                    UsuarioId = usuarioId,
                    UsuarioNombre = usuarioNombre,
                    Tabla = p.Tabla,
                    Accion = p.Accion,
                    RegistroId = p.RegistroId ?? IdDe(p.Entrada),
                    Cambios = p.Cambios 
                });
            }

            _pendientes.Clear();
            _guardando = true;
            try { await contexto.SaveChangesAsync(ct); }
            finally { _guardando = false; }
        }

        return await base.SavedChangesAsync(eventData, result, ct);
    }
    public override Task SaveChangesCanceledAsync(
        DbContextEventData eventData, CancellationToken ct=default)
    {
        _pendientes.Clear();
        return base.SaveChangesCanceledAsync(eventData, ct); 
    }
}