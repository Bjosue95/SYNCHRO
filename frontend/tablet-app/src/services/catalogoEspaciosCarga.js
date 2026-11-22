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