// Fuente única de información comercial facilitada por SYNCHRO.
// Los lugares de "ubicaciones" son propuestas; NO anuncian estaciones instaladas.
export const empresa = Object.freeze({
  nombre: "SYNCHRO",
  lema: "El punto donde todo se conecta",
  correo: "synchrohn@gmail.com",
  whatsapp: "+50433182642",
  whatsappDigitos: "50433182642",
  instagram: "https://www.instagram.com/synchrohn",
  tiktok: "https://www.tiktok.com/@synchro_hn?is_from_webapp=1&sender_device=pc",
  direccion: "Universidad Nacional Autónoma de Honduras (UNAH) – Centro Universitario Regional del Centro (CURC), Carretera salida a Tegucigalpa, Colonia San Miguel, contiguo a Ferromax, Comayagua, Honduras.",
  horario: "Lunes a viernes, 8:00 a. m. a 5:00 p. m."
});

export const soluciones = Object.freeze([
  { id: "casilleros", nombre: "Almacenamiento personal" },
  { id: "encomiendas", nombre: "Gestión de encomiendas" },
  { id: "temperatura", nombre: "Casilleros con temperatura controlada" },
  { id: "carga", nombre: "Estaciones de carga" },
  { id: "modulos", nombre: "Instalación de módulos SYNCHRO" },
  { id: "alianza", nombre: "Alianzas y colaboración" },
  { id: "otra", nombre: "Otra consulta" }
]);

// Coordenadas de referencia de recintos públicos, no ubicaciones de estaciones SYNCHRO.
// null = ubicación pendiente de un punto inequívoco; no inventar latitud o longitud.
// Referencias geográficas:
// CURC y otros campus: mapcarta.com/W1196631891 /W315846408;
// Palmerola: mapcarta.com/W276664595;
// León Alvarado: mapcarta.com/W485602820;
// Villa de San Antonio: mapcarta.com/es/W536950890;
// Ciudad Universitaria UNAH: estudio sobre flora urbana (2026), 14°05'04.96"N 87°09'43.57"O.
export const ubicaciones = Object.freeze([
  {
    id: "curc",
    nombre: "UNAH Campus Comayagua (CURC)",
    ciudad: "Comayagua",
    zona: "centro",
    direccion: "Carretera salida a Tegucigalpa, Colonia San Miguel, contiguo a Ferromax, Comayagua, Honduras",
    coordenadas: [14.42924, -87.63322]
  },
  {
    id: "ciudad-nueva",
    nombre: "Plaza Ciudad Nueva",
    ciudad: "Comayagua",
    zona: "centro",
    direccion: "Plaza Comercial Ciudad Nueva, frente a UNAH, Comayagua, Honduras",
    coordenadas: null
  },
  {
    id: "palmerola",
    nombre: "Aeropuerto Internacional Palmerola (XPL)",
    ciudad: "Comayagua",
    zona: "centro",
    direccion: "Carretera del Norte, Comayagua, Honduras",
    coordenadas: [14.3818, -87.6207]
  },
  {
    id: "villa-san-antonio",
    nombre: "Parque Central de Villa de San Antonio",
    ciudad: "Villa de San Antonio",
    zona: "centro",
    direccion: "Villa de San Antonio, Comayagua, Honduras",
    coordenadas: [14.32192, -87.6147]
  },
  {
    id: "leon-alvarado",
    nombre: "Parque Central León Alvarado",
    ciudad: "Comayagua",
    zona: "centro",
    direccion: "Cero Avenida Norte, frente a la Catedral, Centro Histórico de Comayagua, Honduras",
    coordenadas: [14.46018, -87.64152]
  },
  {
    id: "unah-cu",
    nombre: "Ciudad Universitaria UNAH",
    ciudad: "Tegucigalpa",
    zona: "tegucigalpa",
    direccion: "Bulevar Suyapa y Anillo Periférico, Tegucigalpa, M.D.C., Honduras",
    coordenadas: [14.084711, -87.162103]
  },
  {
    id: "unah-vs",
    nombre: "UNAH Valle de Sula (UNAH-VS)",
    ciudad: "San Pedro Sula",
    zona: "sps",
    direccion: "Sector Pedregal, contiguo a Residencial Villas del Sol, San Pedro Sula, Cortés, Honduras",
    coordenadas: [15.52975, -88.03744]
  }
]);

export function linkMaps(ubicacion) {
  return "https://www.google.com/maps/search/?api=1&query=" +
    encodeURIComponent(ubicacion.nombre + ", " + ubicacion.direccion);
}

export function linkWhatsapp(mensaje = "Hola, quisiera información sobre SYNCHRO.") {
  return "https://wa.me/" + empresa.whatsappDigitos + "?text=" +
    encodeURIComponent(mensaje);
}
