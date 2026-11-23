<script setup>
import { computed, onMounted, onUnmounted, ref } from "vue";
import { RouterLink } from "vue-router";
import { estadoSesion } from "../services/sesion.js";
import AppIcon from "../components/ui/AppIcon.vue";
import modeloMultiple from "../assets/multiple.webp";
import modeloPertenencias from "../assets/pertenencias.webp";
import modeloTemperatura from "../assets/temperatura.webp";
import escenaEntrega from "../assets/entrega.webp";

const servicios = [
  {
    id: "encomiendas", nombre: "Encomiendas", icono: "encomienda",
    titulo: "Prepara el envío de tu paquete",
    descripcion: "Crea una pre-reserva, indica el tamaño del paquete y coordina el siguiente paso con el destinatario. Puedes consultar el estado desde tu cuenta.",
    imagen: escenaEntrega, alt: "Una persona coloca un paquete de cartón en un compartimento abierto",
    nota: "Escena ilustrativa del servicio"
  },
  {
    id: "pertenencias", nombre: "Almacenamiento personal", icono: "casillero",
    titulo: "Un espacio para lo que llevas",
    descripcion: "Guarda mochilas, bolsos y objetos de uso diario en compartimentos de distintos tamaños.",
    imagen: modeloPertenencias, alt: "Casillero SYNCHRO con una mochila en un compartimento abierto",
    estado: "Reserva web próximamente", nota: "Modelo de exhibición"
  },
  {
    id: "temperatura", nombre: "Temperatura controlada", icono: "temperatura",
    titulo: "Frío o calor según el producto",
    descripcion: "Compartimentos de temperatura controlada para productos compatibles que necesitan frío o calor.",
    imagen: modeloTemperatura, alt: "Casillero SYNCHRO con zonas diferenciadas de frío y calor",
    estado: "Reserva web próximamente", nota: "Modelo de exhibición"
  },
  {
    id: "carga", nombre: "Carga de dispositivos", icono: "carga",
    titulo: "Carga tu celular o laptop",
    descripcion: "Usa una estación de carga en el sitio y paga al finalizar.",
    imagen: modeloMultiple, alt: "Estaciones de carga integradas al modelo modular SYNCHRO",
    estado: "Servicio presencial", nota: "Detalle del modelo de exhibición"
  }
];
const servicioElegido = ref("encomiendas");
const servicio = computed(() => servicios.find(item => item.id === servicioElegido.value));
const explorador = ref(null);
const exploradorVisible = ref(false);
let observador;

onMounted(() => {
  if (!("IntersectionObserver" in window)) return;
  observador = new IntersectionObserver(([entrada], observer) => {
    if (!entrada.isIntersecting) return;
    exploradorVisible.value = true;
    observer.disconnect();
  }, { threshold: 0.15 });
  observador.observe(explorador.value);
});

onUnmounted(() => observador?.disconnect());
</script>

<template>
  <div class="inicio-servicios">
    <section class="portada" aria-labelledby="titulo-portada">
      <div class="contenedor portada-grid">
        <div class="portada-texto">
          <p class="portada-contexto">Casilleros modulares SYNCHRO</p>
          <h1 id="titulo-portada">Un lugar para guardar.<br><span>Un punto para enviar.</span></h1>
          <p class="portada-intro">
            Prepara una encomienda desde tu cuenta o explora los servicios de almacenamiento, temperatura y carga.
          </p>
          <div class="botones-portada">
            <RouterLink to="/reservar/encomienda" class="boton boton-portada">
              Solicitar pre-reserva <AppIcon nombre="flecha"/>
            </RouterLink>
            <a href="#servicios" class="enlace-portada">Explorar servicios</a>
          </div>
        </div>
        <figure class="portada-modelo">
          <img :src="modeloMultiple" width="1400" height="788" fetchpriority="high"
            alt="Casillero modular SYNCHRO con compartimentos, pantalla central y estaciones de carga">
          <figcaption>Modelo múltiple de exhibición</figcaption>
        </figure>
      </div>
    </section>

    <section ref="explorador" id="servicios" class="contenedor bloque-servicios" aria-labelledby="titulo-servicios">
      <header class="cabecera-servicios" :class="{ 'en-vista': exploradorVisible }">
        <h2 id="titulo-servicios">Encuentra el servicio que necesitas</h2>
        <p>Elige un servicio para ver cómo funciona y qué puedes hacer desde el portal.</p>
      </header>

      <div class="selector-servicios" role="group" aria-label="Elegir servicio">
        <button v-for="item in servicios" :key="item.id" type="button" class="selector-servicio"
          :aria-pressed="servicioElegido === item.id" aria-controls="detalle-servicio"
          @click="servicioElegido = item.id">
          <AppIcon :nombre="item.icono"/> {{ item.nombre }}
        </button>
      </div>
      <div id="detalle-servicio" class="detalle-servicio" aria-live="polite">
        <Transition name="cambio-servicio" @before-leave="elemento => elemento.inert = true">
          <article :key="servicio.id" class="escena-servicio">
            <figure class="foto-servicio" :class="'foto-' + servicio.id">
              <img :src="servicio.imagen" width="1600" height="900" decoding="async" :alt="servicio.alt">
              <figcaption>{{ servicio.nota }}</figcaption>
            </figure>
            <div class="ficha-servicio">
              <AppIcon :nombre="servicio.icono" :tamano="24"/>
              <div>
                <h3>{{ servicio.titulo }}</h3>
                <p>{{ servicio.descripcion }}</p>
              </div>
              <RouterLink v-if="servicio.id === 'encomiendas'" to="/reservar/encomienda" class="enlace-texto">
                Nueva pre-reserva <AppIcon nombre="flecha"/>
              </RouterLink>
              <p v-else class="servicio-estado">{{ servicio.estado }}</p>
            </div>
          </article>
        </Transition>
      </div>
    </section>

    <section class="contenedor panel-aclaracion">
      <AppIcon nombre="seguridad"/>
      <div>
        <h2>Cuándo se asigna un casillero</h2>
        <p>La pre-reserva registra tu solicitud. La asignación requiere verificaciones posteriores.</p>
      </div>
      <RouterLink :to="estadoSesion.cliente ? '/mi-cuenta' : { name: 'acceso', query: { registro: '1' } }"
        class="enlace-texto">{{ estadoSesion.cliente ? "Mi espacio" : "Crear cuenta" }}</RouterLink>
    </section>
  </div>
</template>

<style src="../styles/portada.css"></style>
