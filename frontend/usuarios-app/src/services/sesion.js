import { reactive } from "vue";
import { apiClientes, ErrorApi } from "./clientesApi.js";

export const estadoSesion = reactive({
  cliente: null,
  comprobando: false
});

export async function comprobarSesion() {
  estadoSesion.comprobando = true;
  try {
    estadoSesion.cliente = await apiClientes.sesion();
    return estadoSesion.cliente;
  } catch (error) {
    if (error instanceof ErrorApi && error.estado === 401) {
      estadoSesion.cliente = null;
      return null;
    }
    throw error;
  } finally {
    estadoSesion.comprobando = false;
  }
}

export async function iniciarSesion(credenciales) {
  const cliente = await apiClientes.ingresar(credenciales);
  estadoSesion.cliente = cliente;
  return cliente;
}

export async function crearCuenta(datos) {
  const cliente = await apiClientes.registrar(datos);
  estadoSesion.cliente = cliente;
  return cliente;
}

export async function cerrarSesion() {
  await apiClientes.salir();
  estadoSesion.cliente = null;
}
