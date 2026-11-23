<script setup>
import { onMounted, onUnmounted, ref } from "vue";
import { RouterLink } from "vue-router";
import { estadoSesion } from "../services/sesion.js";
import AppIcon from "../components/ui/AppIcon.vue";
import modeloMultiple from "../assets/multiple.webp";
import modeloPertenencias from "../assets/pertenencias.webp";
import escenaEntrega from "../assets/entrega.webp";
import escenaEstacion from "../assets/estacion.webp";
import escenaTermica from "../assets/termico.webp";
import escenaCarga from "../assets/carga.webp";

const servicios = [
  {
    id: "encomiendas", nombre: "Encomiendas", icono: "encomienda",
    titulo: "Tu paquete, camino a otra persona",
    descripcion: "Indica el tamaño del paquete y el teléfono de quien lo recibirá. Desde tu cuenta podrás consultar el estado y preparar el consentimiento por WhatsApp.",
    imagen: escenaEntrega, alt: "Una persona deposita un paquete de cartón en un compartimento"
  },
  {
    id: "pertenencias", nombre: "Almacenamiento personal", icono: "casillero",
    titulo: "Deja la mochila y sigue tu día",
    descripcion: "Un compartimento para tu bolso, mochila o compras cuando necesitas tener las manos libres.",
    imagen: modeloPertenencias, alt: "Una mochila dentro de un compartimento abierto del modelo SYNCHRO",
    estado: "Reserva web próximamente", nota: "Modelo de exhibición"
  },
  {
    id: "temperatura", nombre: "Temperatura controlada", icono: "temperatura",
    titulo: "Frío y calor, cada uno en su lugar",
    descripcion: "Compartimentos separados para resguardar productos compatibles que necesitan frío o calor.",
    imagen: escenaTermica, alt: "Compartimentos abiertos con un recipiente en la zona fría y una bolsa en la zona cálida",
    estado: "Reserva web próximamente"
  },
  {
    id: "carga", nombre: "Carga de dispositivos", icono: "carga",
    titulo: "Una pausa para cargar",
    descripcion: "Conecta tu celular o laptop en una estación de carga en el sitio y paga al finalizar.",
    imagen: escenaCarga, alt: "Dispositivos conectados dentro de una estación de carga",
    estado: "Servicio presencial"
  }
];
const recorrido = ref(null);
const servicioMarcado = ref("");
let observador;

function leerAncla() {
  servicioMarcado.value = window.location.hash.slice(1);
}

onMounted(() => {
  leerAncla();
  window.addEventListener("hashchange", leerAncla);
  if (!("IntersectionObserver" in window)) return;
  observador = new IntersectionObserver((entradas, observer) => {
    for (const entrada of entradas) {
      if (!entrada.isIntersecting) continue;
      entrada.target.classList.add("escena-visible");
      observer.unobserve(entrada.target);
    }
  }, { threshold: 0.12 });
  recorrido.value.querySelectorAll(".escena-servicio").forEach(escena => observador.observe(escena));
});

onUnmounted(() => {
  observador?.disconnect();
  window.removeEventListener("hashchange", leerAncla);
});
</script>

<template>
  <div class="inicio-servicios">
    <section id="inicio" class="portada" aria-labelledby="titulo-portada">
      <div class="contenedor montaje-portada">
        <div class="portada-texto">
          <p class="portada-contexto">Estaciones de casilleros · Comayagua</p>
          <h1 id="titulo-portada">De tu mochila <span>a tu próximo envío</span></h1>
          <p class="portada-intro">
            Guarda tus pertenencias, carga tus dispositivos y prepara encomiendas en tu cuenta SYNCHRO.
          </p>
          <div class="botones-portada">
            <a href="#servicios" class="boton boton-portada">Recorrer la estación <span aria-hidden="true">↓</span></a>
            <RouterLink to="/reservar/encomienda" class="boton boton-portada-secundario">
              Solicitar pre-reserva <AppIcon nombre="flecha"/>
            </RouterLink>
          </div>
        </div>
        <div class="portada-escena">
          <figure class="portada-modelo">
            <img :src="escenaEstacion" width="1680" height="946" fetchpriority="high"
              alt="Concepto de una estación SYNCHRO con casilleros modulares en un espacio de tránsito">
            <figcaption>Visualización del concepto SYNCHRO</figcaption>
          </figure>
          <a href="#modelo-synchro" class="acceso-modelo">
            <img :src="modeloMultiple" width="1400" height="788" alt="">
            <span>Ver el modelo modular</span>
            <AppIcon nombre="flecha"/>
          </a>
        </div>
      </div>
    </section>

    <section id="servicios" class="contenedor bloque-servicios" aria-labelledby="titulo-servicios">
      <header class="cabecera-servicios">
        <p class="rotulo-seccion">Nuestros servicios</p>
        <h2 id="titulo-servicios">Una estación para lo que lleves</h2>
        <p>Deja tu mochila, prepara un envío o recarga antes de seguir.</p>
      </header>

      <div ref="recorrido" class="recorrido-servicios">
        <nav class="indice-servicios" aria-label="Servicios de la estación">
          <a v-for="item in servicios" :key="item.id" :href="'#' + item.id"
            :aria-current="servicioMarcado === item.id ? 'location' : undefined">
            <AppIcon :nombre="item.icono"/> {{ item.nombre }}
          </a>
        </nav>
        <div class="escenas-servicio">
          <article v-for="item in servicios" :id="item.id" :key="item.id" class="escena-servicio"
            :aria-labelledby="item.id + '-titulo'">
            <figure class="foto-servicio" :class="'foto-' + item.id">
              <img :src="item.imagen" width="1600" height="900" loading="lazy" decoding="async" :alt="item.alt">
              <figcaption>{{ item.nota || 'Escena ilustrativa' }}</figcaption>
            </figure>
            <div class="ficha-servicio">
              <p class="nombre-servicio"><AppIcon :nombre="item.icono"/> {{ item.nombre }}</p>
              <h3 :id="item.id + '-titulo'">{{ item.titulo }}</h3>
              <p>{{ item.descripcion }}</p>
              <RouterLink v-if="item.id === 'encomiendas'" to="/reservar/encomienda" class="boton boton-primario">
                Nueva pre-reserva <AppIcon nombre="flecha"/>
              </RouterLink>
              <p v-else class="servicio-estado">{{ item.estado }}</p>
            </div>
          </article>
        </div>
      </div>
    </section>

    <section class="banda-encomiendas" aria-labelledby="titulo-guia">
      <div class="contenedor guia-encomiendas">
        <div>
          <p class="rotulo-seccion">Gestión desde tu cuenta</p>
          <h2 id="titulo-guia">Prepara tu encomienda</h2>
          <p>La pre-reserva registra tu solicitud. El casillero se asigna tras las verificaciones correspondientes.</p>
          <div class="acciones-guia">
            <RouterLink to="/reservar/encomienda" class="boton boton-primario">Crear pre-reserva <AppIcon nombre="flecha"/></RouterLink>
            <RouterLink to="/consultar" class="boton boton-secundario">Consultar solicitud</RouterLink>
          </div>
        </div>
        <ol class="pasos-guia">
          <li><strong>Indica el tamaño y el destinatario</strong><span>Elige el compartimento según el paquete y escribe el teléfono de quien lo recibe.</span></li>
          <li><strong>Guarda la referencia y la clave privada</strong><span>Con ambas podrás consultar el estado desde tu cuenta.</span></li>
          <li><strong>Prepara el consentimiento por WhatsApp</strong><span>El destinatario debe confirmar el consentimiento para continuar.</span></li>
        </ol>
      </div>
    </section>

    <section id="historia" class="contenedor historia-synchro" aria-labelledby="titulo-historia">
      <div class="historia-texto">
        <p class="rotulo-seccion">Nuestra historia</p>
        <h2 id="titulo-historia">Nacimos en Comayagua</h2>
        <p class="historia-intro">
          Seis estudiantes de Administración y Generación de Empresas de la UNAH, Campus Comayagua,
          iniciamos SYNCHRO en 2026 en la asignatura Generación de Negocios.
        </p>
        <p>
          La idea partió de situaciones cotidianas: cargar con la mochila y las compras todo el día,
          buscar un lugar para recargar el celular o coordinar una entrega cuando los horarios no coinciden.
          Diseñamos una estación que reúne esos servicios en un mismo lugar.
        </p>
      </div>
      <figure id="modelo-synchro" class="historia-modelo">
        <img :src="modeloMultiple" width="1400" height="788" loading="lazy" decoding="async"
          alt="Diseño del modelo SYNCHRO con varios tamaños, zonas térmicas y módulo de carga">
        <figcaption>Modelo de exhibición · SYNCHRO</figcaption>
      </figure>
      <div class="historia-proposito">
        <div>
          <h3>Misión</h3>
          <p>Facilitar la vida cotidiana en Comayagua con almacenamiento, carga de dispositivos y gestión de encomiendas.</p>
        </div>
        <div>
          <h3>Visión</h3>
          <p>Llevar las estaciones inteligentes SYNCHRO a distintas ciudades de Honduras.</p>
        </div>
      </div>
    </section>

    <div class="contenedor acceso-final">
      <div>
        <h2>Tu cuenta SYNCHRO</h2>
        <p>Crea y consulta solicitudes de encomienda en el mismo portal.</p>
      </div>
      <RouterLink :to="estadoSesion.cliente ? '/mi-cuenta' : { name: 'acceso', query: { registro: '1' } }"
        class="boton boton-primario">{{ estadoSesion.cliente ? "Mi espacio" : "Crear cuenta" }} <AppIcon nombre="flecha"/></RouterLink>
    </div>
  </div>
</template>

<style src="../styles/portada.css"></style>
