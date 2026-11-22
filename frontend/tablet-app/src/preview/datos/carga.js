/*
 * Ejemplos exclusivamente visuales para /vista-previa.
 * Son datos ficticios: no contienen PIN, tokens, rostro ni
 * referencias a cobros o sesiones del backend.
 */
export const espaciosCargaEjemplo = Object.freeze([
  { codigo: "W1", estado: "Disponible" },
  { codigo: "W2", estado: "Disponible" },
  { codigo: "C1", estado: "Disponible" },
  { codigo: "C2", estado: "Disponible" },
  { codigo: "C3", estado: "Disponible" },
  { codigo: "C4", estado: "Disponible" },
  { codigo: "L1", estado: "Disponible" },
  { codigo: "L2", estado: "Disponible" }
]);

export const ordenCargaEjemplo = Object.freeze({
  codigoEspacio: "W2",
  dispositivo: "Celular",
  modalidad: "Inalambrica",
  minutosUtilizados: 24,
  importeHnl: 10,
  descripcionTarifa: "Carga inalámbrica · primera hora"
});
