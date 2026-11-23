<script setup>
import { onMounted, onUnmounted, ref } from "vue";
import { RouterLink } from "vue-router";
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
    estado: "Reserva web próximamente"
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
    descripcion: "Conecta tu celular o laptop en el módulo de carga del casillero y paga al finalizar.",
    imagen: escenaCarga, alt: "Dispositivos conectados en el módulo de carga del casillero",
    estado: "Servicio presencial"
  }
];
const usos = [
  { icono: "casillero", titulo: "Estudiantes y trabajadores", descripcion: "Un lugar para dejar la mochila, la laptop o las compras entre clases, diligencias o jornadas de trabajo." },
  { icono: "encomienda", titulo: "Emprendedores y comercios", descripcion: "Un punto de entrega para pedidos locales cuando los horarios del comercio y del comprador no coinciden." },
  { icono: "carga", titulo: "Viajeros y visitantes", descripcion: "Espacio para resguardar el equipaje o recargar un dispositivo mientras aprovechas una pausa." }
];
const preguntas = [
  {
    pregunta: "¿Qué puedo gestionar desde la web?",
    respuesta: "Puedes crear y consultar pre-reservas de encomiendas desde tu cuenta. La reserva web de almacenamiento personal y temperatura controlada aún no está disponible. La carga de dispositivos es presencial."
  },
  {
    pregunta: "¿La pre-reserva ya me asigna un casillero?",
    respuesta: "La pre-reserva registra la solicitud durante 24 horas. La asignación y el depósito requieren las verificaciones correspondientes."
  },
  {
    pregunta: "¿Qué necesito para consultar una pre-reserva?",
    respuesta: "Entra con la cuenta que creó la solicitud y usa la referencia y la clave privada que recibiste al registrarla."
  },
  {
    pregunta: "¿Qué debe hacer el destinatario por WhatsApp?",
    respuesta: "Desde la pre-reserva puedes preparar las instrucciones. El destinatario debe iniciar la conversación desde su propio número y confirmar el consentimiento."
  }
];
const portada = ref(null);
let observador;

onMounted(() => {
  if (!("IntersectionObserver" in window)) return;
  observador = new IntersectionObserver((entradas, observer) => {
    for (const entrada of entradas) {
      if (!entrada.isIntersecting) continue;
      entrada.target.classList.add("escena-visible");
      observer.unobserve(entrada.target);
    }
  }, { threshold: 0.12 });
  portada.value.querySelectorAll(".escena-servicio, .modelo-synchro").forEach(escena => observador.observe(escena));
});

onUnmounted(() => observador?.disconnect());
</script>

<template>
  <div ref="portada" class="inicio-servicios">
    <section id="inicio" class="portada" aria-labelledby="titulo-portada">
      <div class="contenedor montaje-portada">
        <div class="portada-texto">
          <p class="portada-contexto">SYNCHRO · Comayagua</p>
          <h1 id="titulo-portada">Casilleros inteligentes <span>para tu día</span></h1>
          <p class="portada-intro">
            Guarda tus pertenencias, usa un compartimento con control de temperatura o recarga tus dispositivos. En el portal ya puedes crear y consultar pre-reservas de encomiendas.
          </p>
          <div class="botones-portada">
            <a href="#servicios" class="boton boton-portada">Explorar servicios <span aria-hidden="true">↓</span></a>
            <RouterLink to="/reservar/encomienda" class="boton boton-portada-secundario">
              Solicitar pre-reserva <AppIcon nombre="flecha"/>
            </RouterLink>
          </div>
        </div>
        <div class="portada-escena">
          <figure class="portada-modelo">
            <img :src="escenaEstacion" width="1680" height="946" fetchpriority="high"
              alt="Casillero inteligente SYNCHRO en un espacio de tránsito">
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
        <h2 id="titulo-servicios">Cuatro soluciones en un casillero</h2>
      </header>

      <div class="escenas-servicio">
        <article v-for="item in servicios" :id="item.id" :key="item.id" class="escena-servicio"
          :aria-labelledby="item.id + '-titulo'">
          <figure class="foto-servicio" :class="'foto-' + item.id">
            <img :src="item.imagen" width="1600" height="900" loading="lazy" decoding="async" :alt="item.alt">
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
    </section>

    <section class="contenedor casos-synchro" aria-labelledby="titulo-casos">
      <h2 id="titulo-casos">Pensado para moverte por la ciudad</h2>
      <div class="casos-rejilla">
        <article v-for="uso in usos" :key="uso.titulo">
          <AppIcon :nombre="uso.icono" :tamano="28"/>
          <h3>{{ uso.titulo }}</h3>
          <p>{{ uso.descripcion }}</p>
        </article>
      </div>
    </section>

    <section id="modelo-synchro" class="contenedor modelo-synchro" aria-labelledby="titulo-modelo">
      <figure class="modelo-imagen">
        <img :src="modeloMultiple" width="1400" height="788" loading="lazy" decoding="async"
          alt="Casillero SYNCHRO con compartimentos a la izquierda y módulo de carga a la derecha">
      </figure>
      <div class="modelo-descripcion">
        <h2 id="titulo-modelo">Conoce el casillero SYNCHRO</h2>
        <dl class="modelo-partes">
          <div><dt>Compartimentos de varios tamaños</dt><dd>Para pertenencias y encomiendas de distinto volumen.</dd></div>
          <div><dt>Zonas de frío y calor</dt><dd>Separadas según lo que necesita cada producto compatible.</dd></div>
          <div><dt>Módulo de carga</dt><dd>Para celulares y laptops durante una pausa.</dd></div>
        </dl>
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
          Diseñamos casilleros inteligentes que reúnen esos servicios en un mismo lugar.
        </p>
      </div>
      <div class="historia-proposito">
        <article>
          <h3>Misión</h3>
          <p>Optimizar el tiempo y la movilidad de las personas y los negocios mediante casilleros inteligentes automatizados para el resguardo de pertenencias, la gestión de encomiendas y la carga de dispositivos electrónicos, integrando tecnología innovadora, seguridad y espacios con temperatura controlada, con un compromiso de accesibilidad e inclusión que contribuya al bienestar de la comunidad.</p>
        </article>
        <article>
          <h3>Visión</h3>
          <p>Convertirnos en un emprendimiento tecnológico hondureño reconocido por transformar la manera en que las personas y los negocios acceden a servicios de almacenamiento y gestión autónoma, mediante una red de casilleros inteligentes que promueva la innovación, la seguridad y la accesibilidad, contribuyendo al desarrollo de comunidades más conectadas.</p>
        </article>
      </div>
    </section>

    <section id="preguntas" class="contenedor preguntas-synchro" aria-labelledby="titulo-preguntas">
      <h2 id="titulo-preguntas">Preguntas frecuentes</h2>
      <div class="preguntas-lista">
        <details v-for="item in preguntas" :key="item.pregunta" name="preguntas-synchro">
          <summary>{{ item.pregunta }}</summary>
          <p>{{ item.respuesta }}</p>
        </details>
      </div>
    </section>
  </div>
</template>

<style src="../styles/portada.css"></style>
