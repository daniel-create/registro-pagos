namespace PrestamosApi.Models;

public enum TipoPrestamo {CuotasFijas = 1, MensualFlexible = 2}
public enum EstadoPrestamo {Activo = 1, Cancelado = 2}
public enum MetodoPago { Efectivo = 1, Transferencia = 2, Yape = 3, Plin = 4, Otro = 5 }

public class Cliente
{
    public int Id {get;set; }
    public string Nombre {get; set; } = "";
    public string? Documento {get;set; }
    public string? Telefono {get;set; }
    public string? Direccion {get;set; }
    public string? Referido { get; set; }
    public DateTime CreadoEn {get; set; } = DateTime.UtcNow;
    public List<Prestamo> Prestamos {get; set; } = new();
}
public class Prestamo
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public TipoPrestamo Tipo { get; set; }
    public decimal CapitalOriginal { get; set; }
    public decimal CapitalPendiente { get; set; }
    public decimal TasaMensual { get; set; }   // interés que paga el cliente: 6 significa 6% (en cuotas fijas es el interés mayor)
    public decimal? TasaMenor { get; set; }    // solo cuotas fijas: tu parte del interés
    public int? NumeroCuotas { get; set; }     // solo cuotas fijas
    public int DiaPago { get; set; }
    public DateOnly FechaInicio { get; set; }
    public EstadoPrestamo Estado { get; set; } = EstadoPrestamo.Activo;
    public int? PrestamoAnteriorId { get; set; }  // si nació de una renovación, el préstamo que reemplazó
    public List<Periodo> Periodos { get; set; } = new();
    public List<Pago> Pagos { get; set; } = new();
}
public class Periodo
{
    public int Id {get;set; }
    public int PrestamoId {get;set; }
    public Prestamo Prestamo {get;set; } = null!;
    public int Numero {get;set; } //numero de cuota
    public DateOnly FechaVence {get;set; }
    public decimal InteresGenerado {get;set; }
     public decimal InteresMenor { get; set; }     // solo cuotas fijas: tu parte
    public decimal Diferencia { get; set; }       // solo cuotas fijas: parte de la otra persona
    public decimal InteresPagado {get;set; }
    public decimal CapitalProgramado {get;set; }
    public decimal CapitalPagado {get; set; }
}
public class Pago
{
    public int Id {get;set; }
    public int PrestamoId {get;set; }   
    public Prestamo Prestamo {get;set; } = null!;
    public int? PeriodoId {get; set; } // cuota a la que se aplicó
    public DateOnly Fecha {get;set; }
    public decimal Monto {get;set; }
    public decimal AMora {get;set; }
    public decimal AInteres {get;set; }
    public decimal AInteresMenor { get; set; }
    public decimal ADiferencia { get; set; }
    public decimal ACapital {get;set; }
    public MetodoPago Metodo {get;set; } = MetodoPago.Efectivo;
    public string RegistradoPor {get;set; } = "";
    public int? ReversoDe { get; set; }
    public Guid ClaveUnica { get; set; }
}

public class Aporte
{
    public int Id {get; set; }
    public string Nombre {get; set; } = "";
    public decimal Monto {get; set; }
    public DateOnly Fecha {get; set; }
}