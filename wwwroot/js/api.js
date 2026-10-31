// Funciones compartidas por todas las pantallas.

function cerrarSesion() {
  sessionStorage.clear();
  window.location.href = '/index.html';
}

// Llama a la API con el token de la sesión. Devuelve los datos o lanza un Error con el mensaje.
async function api(url, opciones = {}) {
  const cabeceras = { 'Content-Type': 'application/json' };
  const token = sessionStorage.getItem('token');
  if (token) cabeceras['Authorization'] = 'Bearer ' + token;

  let r;
  try {
    r = await fetch(url, {
      method: opciones.method || 'GET',
      headers: cabeceras,
      body: opciones.body ? JSON.stringify(opciones.body) : undefined
    });
  } catch {
    throw new Error('No hay conexión con el servidor.');
  }

  if (r.status === 401) {
    cerrarSesion();
    throw new Error('Tu sesión venció. Ingresa de nuevo.');
  }

  const datos = await r.json().catch(() => null);
  if (!r.ok) throw new Error((datos && datos.mensaje) || 'Ocurrió un error. Intenta de nuevo.');
  return datos;
}

// 1250.5 -> "S/ 1,250.50"
function soles(n) {
  return 'S/ ' + Number(n).toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

// "2026-11-18" -> "18 nov" (agrega el año si no es el actual)
function fechaCorta(iso) {
  const [a, m, d] = iso.split('-').map(Number);
  const meses = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];
  const anio = a !== new Date().getFullYear() ? ' ' + a : '';
  return d + ' ' + meses[m - 1] + anio;
}


// ---- Diseño compartido y barra superior ----

const ESTILO = `
:root { --acento: #0F6B5B; --texto: #14201C; --suave: #5A6B66; --borde: #BFCBC7; --fondo: #F2F5F4; --verde: #BFE3DA; --verde-texto: #0B5445; }
* { box-sizing: border-box; }
body { margin: 0; background: var(--fondo); color: var(--texto); font-family: 'DM Sans', system-ui, sans-serif; }
.barra { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 24px; padding: 14px 24px; background: #fff; border-bottom: 1px solid #DDE5E2; }
.barra .marca { font-size: 20px; font-weight: 700; color: var(--acento); }
.barra a { color: #3F5650; text-decoration: none; font-weight: 500; }
.barra a.activa { color: var(--acento); font-weight: 700; }
.barra .espacio { flex: 1; }
.barra button { height: 40px; padding: 0 14px; border: 1px solid var(--borde); border-radius: 8px; background: #fff; color: #3F5650; font-family: inherit; font-size: 14px; font-weight: 500; cursor: pointer; }
main.pagina { max-width: 1200px; margin: 0 auto; padding: 24px; display: flex; flex-direction: column; gap: 20px; }
h1 { margin: 0; font-size: 30px; }
.sub { font-size: 14px; color: var(--suave); }
.tarjeta { background: #fff; border: 1px solid #DDE5E2; border-radius: 12px; padding: 20px; }
.resumen { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; }
.caja { border-radius: 8px; padding: 14px; background: #fff; border: 1px solid #DDE5E2; }
.caja .l { font-size: 13px; color: var(--suave); }
.caja .v { font-size: 24px; font-weight: 700; margin-top: 4px; }
.tabla { overflow-x: auto; }
table { width: 100%; min-width: 640px; border-collapse: collapse; }
th { text-align: left; padding: 10px 8px; font-size: 13px; font-weight: 500; color: var(--suave); }
td { padding: 12px 8px; border-top: 1px solid #E6ECEA; }
.chip { display: inline-block; padding: 4px 10px; border-radius: 999px; font-size: 13px; font-weight: 700; white-space: nowrap; }
.ok { background: var(--verde); color: var(--verde-texto); }
.mal { background: #FDE7E4; color: #9B1C12; }
.neutro { background: #EEF1F0; color: #5A6B66; }
.vacio { padding: 24px 8px; color: var(--suave); text-align: center; }
`;

(function () {
  const s = document.createElement('style');
  s.textContent = ESTILO;
  document.head.appendChild(s);
})();

// Dibuja la barra superior dentro de <div id="barra">. 'activa' es la pestaña resaltada.
function pintarBarra(activa) {
  const paginas = [
    { id: 'panel', texto: 'Panel', url: '/panel.html' },
    { id: 'clientes', texto: 'Clientes', url: '/clientes.html' },
    { id: 'prestamos', texto: 'Préstamos', url: '/prestamos.html' },
    { id: 'capital', texto: 'Capital', url: '/capital.html' }
  ];
  const contenedor = document.getElementById('barra');
  contenedor.className = 'barra';
  contenedor.innerHTML =
    '<span class="marca">RegistroPagos</span>' +
    paginas.map(p => '<a href="' + p.url + '"' + (p.id === activa ? ' class="activa"' : '') + '>' + p.texto + '</a>').join('') +
    '<span class="espacio"></span><span id="barra-nombre" class="sub"></span><button type="button" id="barra-salir">Salir</button>';
  document.getElementById('barra-nombre').textContent = sessionStorage.getItem('nombre') || '';
  document.getElementById('barra-salir').addEventListener('click', cerrarSesion);
}


// ---- Botones y formularios ----

const ESTILO_FORMULARIOS = ` [hidden] { display: none !important; }
.btn { height: 44px; padding: 0 18px; border: 0; border-radius: 8px; background: var(--acento); color: #fff; font-family: inherit; font-size: 15px; font-weight: 700; cursor: pointer; }
.btn:disabled { opacity: .6; cursor: default; }
.btn-sec { height: 44px; padding: 0 18px; border: 1px solid var(--borde); border-radius: 8px; background: #fff; color: #3F5650; font-family: inherit; font-size: 15px; font-weight: 500; cursor: pointer; }
.btn-chico { height: 34px; padding: 0 12px; border: 1px solid var(--borde); border-radius: 8px; background: #fff; color: var(--acento); font-family: inherit; font-size: 14px; font-weight: 700; cursor: pointer; }
.campo { display: flex; flex-direction: column; gap: 6px; }
.campo label { font-size: 13px; color: var(--suave); }
.campo { min-width: 0; }
.campo input, .campo select { width: 100%; min-width: 0; height: 48px; padding: 0 14px; border: 1px solid var(--borde); border-radius: 8px; font-family: inherit; font-size: 16px; color: var(--texto); background: #fff; }
.campo input:focus, .campo select:focus { outline: 2px solid var(--acento); outline-offset: 1px; }
dialog { width: min(440px, 92vw); border: 1px solid #DDE5E2; border-radius: 16px; padding: 24px; color: var(--texto); font-family: 'DM Sans', system-ui, sans-serif; }
dialog::backdrop { background: rgba(20, 32, 28, .45); }
.error-form { min-height: 20px; font-size: 14px; color: #9B1C12; }
.cabecera { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; }
`;

(function () {
  const s = document.createElement('style');
  s.textContent = ESTILO_FORMULARIOS;
  document.head.appendChild(s);
})();