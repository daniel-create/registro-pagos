using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Models;

namespace PrestamosApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<Usuario>(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Prestamo> Prestamos => Set<Prestamo>();
    public DbSet<Periodo> Periodos => Set<Periodo>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<Aporte> Aportes => Set<Aporte>();

    protected override void OnModelCreating(ModelBuilder b)
    {   
        base.OnModelCreating(b);
        b.Entity<Cliente>().Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        b.Entity<Cliente>().Property(x => x.Referido).HasMaxLength(150);
        

        b.Entity<Prestamo>().Property(x => x.Tipo).HasConversion<string>();
        b.Entity<Prestamo>().Property(x => x.Estado).HasConversion<string>();
        b.Entity<Prestamo>().Property(x => x.CapitalOriginal).HasPrecision(18,2);
        b.Entity<Prestamo>().Property(x => x.CapitalPendiente).HasPrecision(18,2);
        b.Entity<Prestamo>().Property(x => x.TasaMensual).HasPrecision(7,4);
        b.Entity<Prestamo>().Property(x => x.TasaMenor).HasPrecision(7, 4);

        b.Entity<Periodo>().Property(x => x.InteresGenerado).HasPrecision(18,2);
        b.Entity<Periodo>().Property(x => x.InteresMenor).HasPrecision(18, 2);
        b.Entity<Periodo>().Property(x => x.Diferencia).HasPrecision(18, 2);
        b.Entity<Periodo>().Property(x => x.InteresPagado).HasPrecision(18,2);
        b.Entity<Periodo>().Property(x => x.CapitalProgramado).HasPrecision(18,2);
        b.Entity<Periodo>().Property(x => x.CapitalPagado).HasPrecision(18, 2);
        b.Entity<Periodo>().HasIndex(x => new { x.PrestamoId, x.Numero }).IsUnique();


        b.Entity<Pago>().Property(x => x.Metodo).HasConversion<string>();
        b.Entity<Pago>().Property(x => x.Monto).HasPrecision(18,2);
        b.Entity<Pago>().Property(x => x.AMora).HasPrecision(18,2);
        b.Entity<Pago>().Property(x => x.AInteres).HasPrecision(18,2);
        b.Entity<Pago>().Property(x => x.AInteresMenor).HasPrecision(18, 2);
        b.Entity<Pago>().Property(x => x.ADiferencia).HasPrecision(18, 2);
        b.Entity<Pago>().Property(x => x.ACapital).HasPrecision(18,2);
        b.Entity<Pago>().HasIndex(x => x.ClaveUnica).IsUnique();
        b.Entity<Aporte>().Property(x => x.Nombre).HasMaxLength(150).IsRequired(); 
        b.Entity<Aporte>().Property(x => x.Monto).HasPrecision(18,2);
    }
}
