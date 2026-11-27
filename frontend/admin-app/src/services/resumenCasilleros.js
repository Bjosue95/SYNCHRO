// Ambos catálogos deben estar disponibles para presentar un total completo.
export function resumirCasilleros(casilleros, espaciosCarga) {
  if (!Array.isArray(casilleros) || !Array.isArray(espaciosCarga)) return null;
  const espacios = [...casilleros, ...espaciosCarga];
  return {
    total: espacios.length,
    disponibles: espacios.filter(x => x.estado === "Disponible").length,
    ocupados: espacios.filter(x => x.estado === "Ocupado").length,
    resguardo: casilleros.length,
    carga: espaciosCarga.length
  };
}
