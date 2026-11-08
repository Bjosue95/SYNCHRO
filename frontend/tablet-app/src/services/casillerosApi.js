/*
 * SYNCHRO
 * Servicio de consulta de casilleros
 */

// Consultar los casilleros almacenados en SQL Server
export async function obtenerCasilleros() {
  const respuesta = await fetch("/api/casilleros-bd", {
    headers: {
      Accept: "application/json",
    },
  });

  if (!respuesta.ok) {
    throw new Error(
      `No se pudieron consultar los casilleros (${respuesta.status})`,
    );
  }

  const datos = await respuesta.json();

  // Admitir una lista o una respuesta con propiedad value
  const casilleros = Array.isArray(datos) ? datos : datos?.value;

  if (!Array.isArray(casilleros)) {
    throw new Error("La API devolvió un formato de casilleros inesperado");
  }

  return casilleros;
}
