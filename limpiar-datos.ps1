# Borra TODOS los datos de clientes, prestamos, cuotas, pagos y auditoria.
# No toca las cuentas de usuario. No se puede deshacer.

$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"

$seg = Read-Host "Clave del usuario prestamos (de la base de datos)" -AsSecureString
$env:PGPASSWORD = [System.Net.NetworkCredential]::new("", $seg).Password

try {
    Write-Host ""
    Write-Host "Esto va a borrar TODOS los datos de clientes, prestamos, cuotas, pagos y auditoria." -ForegroundColor Yellow
    Write-Host "Tu cuenta de inicio de sesion no se toca. No se puede deshacer."
    Write-Host ""
    Write-Host "Ahora hay:"

    $conteo = @'
SELECT (SELECT count(*) FROM "Clientes") AS clientes, (SELECT count(*) FROM "Prestamos") AS prestamos, (SELECT count(*) FROM "Pagos") AS pagos;
'@
    $conteo | & $psql -U prestamos -d prestamos -h localhost

    $respuesta = Read-Host "Para continuar escribe BORRAR (en mayusculas)"
    if ($respuesta -cne "BORRAR") {
        Write-Host "Cancelado. No se borro nada." -ForegroundColor Green
    }
    else {
        $borrar = @'
TRUNCATE TABLE "Pagos", "Periodos", "Prestamos", "Clientes", "Auditorias" RESTART IDENTITY;
'@
        $borrar | & $psql -U prestamos -d prestamos -h localhost
        Write-Host "Listo: todo quedo en cero." -ForegroundColor Green
    }
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}