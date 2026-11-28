const catalogo = Object.freeze({
  W1: { dispositivo: "Celular", modalidad: "Inalambrica" },
  W2: { dispositivo: "Celular", modalidad: "Inalambrica" },
  C1: { dispositivo: "Celular", modalidad: "Cable" },
  C2: { dispositivo: "Celular", modalidad: "Cable" },
  C3: { dispositivo: "Celular", modalidad: "Cable" },
  C4: { dispositivo: "Celular", modalidad: "Cable" },
  L1: { dispositivo: "Laptop", modalidad: "Laptop" },
  L2: { dispositivo: "Laptop", modalidad: "Laptop" },
});
export function infoCarga(codigo) {
  return catalogo[String(codigo ?? "").trim().toUpperCase()] ?? null;
}
export function espaciosPara(lista, dispositivo, modalidad) {
  if (!Array.isArray(lista)) return [];
  if (!["Celular", "Laptop"].includes(dispositivo)) return [];
  if (dispositivo === "Celular" && !["Inalambrica", "Cable"].includes(modalidad)) return [];
  if (dispositivo === "Laptop" && modalidad !== "Laptop") return [];
  return lista.filter(item => {
    const info = infoCarga(item?.codigo);
    return item?.estado === "Disponible" && info?.dispositivo === dispositivo && info?.modalidad === modalidad;
  }).sort((a, b) => a.codigo.localeCompare(b.codigo, "es", { numeric: true }));
}

// Una sola consulta de inventario para seleccionar, verificar y colocar.
export async function consultarEspaciosCarga(solicitar = globalThis.fetch) {
  const respuesta = await solicitar("/api/terminales/carga/espacios", {
    credentials: "same-origin", cache: "no-store"
  });
  if (!respuesta.ok) throw new Error(`No se pudo consultar la disponibilidad (HTTP ${respuesta.status}).`);
  const datos = await respuesta.json();
  if (!Array.isArray(datos)) throw new Error("Formato de espacios inesperado.");
  return datos.map(x => ({
    codigo: String(x?.codigo ?? x?.Codigo ?? "").trim().toUpperCase(),
    estado: String(x?.estado ?? x?.Estado ?? "").trim()
  })).filter(x => infoCarga(x.codigo));
}

export async function comprobarEspacioCarga(codigo, solicitar = globalThis.fetch) {
  const lista = await consultarEspaciosCarga(solicitar);
  if (!lista.some(item => item.codigo === codigo && item.estado === "Disponible"))
    throw new Error("El espacio ya no está disponible.");
}
