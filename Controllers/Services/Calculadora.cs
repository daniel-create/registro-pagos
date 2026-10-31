namespace PrestamosApi.Services;

public record CuotaFija(int Numero, decimal Capital, decimal InteresMenor, decimal Diferencia, decimal Total);

public record CronogramaFijo(decimal CapitalTotal, decimal TotalACobrar, decimal Ganancia, List<CuotaFija> Cuotas);

public enum EfectoPago {Desplaza, Continua, Desarma, Cancela}
public record RepartoPago(
    decimal Interes,
    decimal InteresMenor,
    decimal Diferencia,
    decimal Capital,
    decimal CapitalRestante,
    EfectoPago Efecto,
    CuotaFija? CuotaNueva);

public static class Calculadora
{
    private static decimal RedondearArriba(decimal valor) => Math.Ceiling(valor * 100) / 100;

    public static CronogramaFijo CuotasFijas(decimal capital, decimal interesMayor, decimal interesMenor, int cuotas)
    {
        if (capital <= 0) throw new ArgumentException("El capital debe ser mayor que cero.");
        if (cuotas < 1) throw new ArgumentException("Debe haber al menos una cuota.");

        decimal diferenciaInteres = interesMayor - interesMenor;
        decimal capitalRestante = capital;
        decimal totalGeneral = 0;
        decimal gananciaInteresMenor = 0;
        var lista = new List<CuotaFija>();

        for (int cuota = 1; cuota <= cuotas; cuota++)
        {
            decimal parte = cuota < cuotas ? RedondearArriba(capital / cuotas) : capitalRestante;

            decimal montoInteresMenor = RedondearArriba(capitalRestante * interesMenor / 100);
            decimal montoDiferencia = RedondearArriba(capitalRestante * diferenciaInteres / 100);
            decimal totalCuota = parte + montoInteresMenor + montoDiferencia;

            lista.Add(new CuotaFija(cuota, parte, montoInteresMenor, montoDiferencia, totalCuota));

            capitalRestante -= parte;
            totalGeneral += totalCuota;
            gananciaInteresMenor += montoInteresMenor;
        }

        return new CronogramaFijo(capital, totalGeneral, gananciaInteresMenor, lista);
    }

    // Una cuota con todo el capital indicado y el interés de ese capital (mismo redondeo que arriba).
     public static CuotaFija CuotaSobre(decimal capital, decimal interesMayor, decimal interesMenor, int numero = 0)
    {
        decimal menor = RedondearArriba(capital * interesMenor / 100);
        decimal diferencia = RedondearArriba(capital * (interesMayor - interesMenor) / 100);
        return new CuotaFija (numero, capital, menor, diferencia, capital + menor + diferencia);
    }
    // Reparte un pago: primero el interés completo de la cuota, lo que sobra va al capital.
    public static RepartoPago RepartirPago(
        decimal monto,
        decimal interesMenorCuota, decimal diferenciaCuota, decimal capitalCuota,
        decimal capitalPendiente, decimal interesMayor, decimal interesMenor)
    {
        decimal interes = interesMenorCuota + diferenciaCuota;
        if (monto < interes)
            throw new ArgumentException($"El monto mínimo es {interes:0.00}: el interés de la cuota.");
        
        decimal capitalPagado = monto - interes;

        if(capitalPagado > capitalPendiente)
            throw new ArgumentException($"El monto máximo es {capitalPendiente + interes:0.00}: el capital pendiente más el interés de la cuota.");
        
        decimal restante = capitalPendiente - capitalPagado;

        EfectoPago efecto;
        CuotaFija? cuotaNueva = null;

        if (restante == 0)
            efecto = EfectoPago.Cancela;
        else if (capitalPagado == 0)
            efecto = EfectoPago.Desplaza;   
        else if (capitalPagado == capitalCuota)
            efecto = EfectoPago.Continua;
        else
        {
            efecto = EfectoPago.Desarma;
            cuotaNueva = CuotaSobre(restante, interesMayor, interesMenor);
        }

        return new RepartoPago(interes, interesMenorCuota, diferenciaCuota, capitalPagado, restante, efecto, cuotaNueva);
        
    }
    
}