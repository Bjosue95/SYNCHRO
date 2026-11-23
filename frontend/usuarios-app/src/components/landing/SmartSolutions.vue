<script setup>
import AppIcon from "../ui/AppIcon.vue";

const protecciones = Object.freeze([
  {
    "icono": "facial",
    "titulo": "Verificación facial",
    "descripcion": "Identificación para proteger depósitos, accesos y retiros."
  },
  {
    "icono": "pin",
    "titulo": "Códigos de un solo uso",
    "descripcion": "Validación con PIN temporal para reforzar la seguridad de las operaciones."
  },
  {
    "icono": "control-acceso",
    "titulo": "Control de acceso",
    "descripcion": "Autorizaciones para que cada entrega llegue a la persona indicada."
  },
  {
    "icono": "alarma",
    "titulo": "Alarmas y alertas",
    "descripcion": "Avisos ante incidencias, aperturas no autorizadas o situaciones de riesgo."
  },
  {
    "icono": "sensores",
    "titulo": "Sensores inteligentes",
    "descripcion": "Monitoreo de aperturas, ocupación y condiciones dentro de las estaciones."
  },
  {
    "icono": "integridad",
    "titulo": "Integridad de casilleros",
    "descripcion": "Supervisión de eventos y del estado físico de los compartimentos."
  },
  {
    "icono": "cifrado",
    "titulo": "Protección de información",
    "descripcion": "Seguridad de identidad, permisos y datos de las operaciones."
  },
  {
    "icono": "trazabilidad",
    "titulo": "Registro y trazabilidad",
    "descripcion": "Seguimiento de operaciones para una administración responsable."
  },
  {
    "icono": "braille",
    "titulo": "Identificación en braille",
    "descripcion": "Código de casillero con identificación táctil para una experiencia más inclusiva."
  },
  {
    "icono": "sensor-temperatura",
    "titulo": "Control térmico",
    "descripcion": "Monitoreo de las condiciones de los módulos de frío y calor."
  },
  {
    "icono": "notificacion",
    "titulo": "Notificaciones",
    "descripcion": "Avisos que mantienen informadas a las personas sobre sus operaciones."
  },
  {
    "icono": "resiliencia",
    "titulo": "Continuidad operativa",
    "descripcion": "Arquitectura orientada a reducir interrupciones y recuperarse de fallos."
  }
]);

const energia = Object.freeze([
  {
    "icono": "solar",
    "titulo": "Paneles solares",
    "descripcion": "Aprovechamiento de energía renovable como fuente complementaria."
  },
  {
    "icono": "bateria-respaldo",
    "titulo": "Respaldo de energía",
    "descripcion": "Almacenamiento eléctrico para mantener servicios esenciales ante cortes."
  },
  {
    "icono": "eficiencia",
    "titulo": "Eficiencia energética",
    "descripcion": "Optimización del consumo de equipos, iluminación y climatización."
  },
  {
    "icono": "energia",
    "titulo": "Supervisión energética",
    "descripcion": "Seguimiento de energía disponible y consumo para anticipar necesidades."
  }
]);

const beneficios = Object.freeze([
  {
    icono: "autonomia", titulo: "Más autonomía",
    descripcion: "Soluciones de autoservicio que facilitan las actividades diarias."
  },
  {
    icono: "accesibilidad", titulo: "Acceso más inclusivo",
    descripcion: "Recursos de identificación pensados para distintas necesidades."
  },
  {
    icono: "comunidad", titulo: "Tiempo para las personas",
    descripcion: "Menos complicaciones al guardar, entregar o retirar objetos."
  }
]);
</script>

<template>
  <section id="seguridad" class="syn-solutions">
    <div class="contenedor">
      <div class="syn-solutions-heading">
        <span class="syn-solutions-kicker">MÁS QUE ALMACENAMIENTO</span>
        <h2>La tecnología tiene sentido cuando resuelve problemas.</h2>
        <p>
          Una experiencia segura también debe ser accesible, clara y fácil de utilizar.
          Así combinamos protección, tecnología y atención a distintas necesidades.
        </p>
      </div>

      <div class="syn-solutions-grid">
        <article v-for="(item, i) in protecciones" :key="item.icono"
          class="syn-solutions-card" :class="{ 'syn-solutions-first': i === 0 }">
          <span class="syn-solutions-icon"><AppIcon :nombre="item.icono" :tamano="35" /></span>
          <h3>{{ item.titulo }}</h3>
          <p>{{ item.descripcion }}</p>
        </article>
      </div>

      <div id="energia" class="syn-energy">
        <div class="syn-energy-heading">
          <div>
            <span class="syn-solutions-kicker">ENERGÍA Y CONTINUIDAD</span>
            <h2>Energía inteligente para seguir conectados.</h2>
            <p>
              SYNCHRO se diseña para integrar paneles solares, respaldo eléctrico
              y gestión del consumo que aporten resiliencia y sostenibilidad.
            </p>
          </div>
          <span class="syn-energy-emblem"><AppIcon nombre="solar" :tamano="62"/></span>
        </div>
        <div class="syn-energy-flow" aria-label="Esquema de alimentación energética">
          <span><AppIcon nombre="solar" :tamano="27"/> Captación solar</span>
          <i aria-hidden="true"></i>
          <span><AppIcon nombre="bateria-respaldo" :tamano="27"/> Respaldo</span>
          <i aria-hidden="true"></i>
          <span><AppIcon nombre="casillero" :tamano="27"/> Estación SYNCHRO</span>
        </div>
        <div class="syn-energy-grid">
          <article v-for="item in energia" :key="item.icono" class="syn-energy-card">
            <AppIcon :nombre="item.icono" :tamano="31"/>
            <h3>{{ item.titulo }}</h3>
            <p>{{ item.descripcion }}</p>
          </article>
        </div>
      </div>

      <div class="syn-social-impact">
        <div class="syn-impact-intro">
          <span class="syn-solutions-kicker">NUESTRO COMPROMISO</span>
          <h2>Una solución útil para la comunidad.</h2>
          <p>
            La innovación no se mide solamente en tecnología. También importa que
            las personas puedan aprovecharla con confianza y mayor comodidad.
          </p>
        </div>
        <div class="syn-impact-items">
          <div v-for="item in beneficios" :key="item.icono" class="syn-impact-item">
            <span><AppIcon :nombre="item.icono" :tamano="30" /></span>
            <div>
              <h3>{{ item.titulo }}</h3>
              <p>{{ item.descripcion }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.syn-energy-flow { display:flex; flex-wrap:wrap; align-items:center; gap:14px; padding:20px 0 0; }
.syn-energy-flow span { display:inline-flex; align-items:center; gap:9px; font-size:12px; font-weight:700; color:#DDF4FA; }
.syn-energy-flow span :deep(svg) { filter:brightness(0) invert(1); }
.syn-energy-flow i { display:block; width:45px; height:3px; border-radius:3px; background:linear-gradient(90deg,#224D6D 0%,#5EC6E8 50%,#224D6D 100%); background-size:200% 100%; animation:syn-energy-signal 3s linear infinite; }
@keyframes syn-energy-signal { to { background-position:-200% 0; } }
@media(max-width:560px) { .syn-energy-flow { gap:9px; } .syn-energy-flow i { width:20px; } .syn-energy-flow span { font-size:10px; } }
@media(prefers-reduced-motion:reduce) { .syn-energy-flow i { animation:none; background:#5EC6E8; } }

.syn-energy { margin-top:clamp(45px,6vw,76px); padding:clamp(25px,4vw,46px); border-radius:20px; background:#01305C; color:#ECFAFF; }
.syn-energy-heading { display:flex; align-items:flex-start; justify-content:space-between; gap:24px; }
.syn-energy-heading .syn-solutions-kicker { color:#8CE5EC; }
.syn-energy-heading h2 { font-size:clamp(29px,3.4vw,42px); font-weight:650; line-height:1.16; letter-spacing:-.035em; margin:12px 0; color:#fff; max-width:580px; }
.syn-energy-heading p { color:#D0E7F0; font-size:14px; line-height:1.75; max-width:570px; margin:0; }
.syn-energy-emblem { display:grid; place-items:center; width:92px; height:92px; flex:0 0 92px; border-radius:20px; background:#174967; border:1px solid #4C8FA5; }
.syn-energy-emblem :deep(svg) { filter:brightness(0) invert(1); }
.syn-energy-grid { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:1px; background:#476A81; border:1px solid #476A81; border-radius:12px; overflow:hidden; margin-top:31px; }
.syn-energy-card { min-width:0; padding:21px 18px; background:#123D60; }
.syn-energy-card :deep(svg) { filter:brightness(0) invert(1); }
.syn-energy-card h3 { font-size:14px; color:#fff; font-weight:700; margin:14px 0 7px; }
.syn-energy-card p { color:#D0E7F0; font-size:12px; line-height:1.65; margin:0; }
@media(max-width:960px) { .syn-energy-grid { grid-template-columns:repeat(2,minmax(0,1fr)); } }
@media(max-width:560px) { .syn-energy-grid { grid-template-columns:1fr; } .syn-energy-card { display:grid; grid-template-columns:36px 1fr; gap:2px 12px; } .syn-energy-card > .icono-ui { grid-row:span 2; } .syn-energy-card h3 { margin:0; } .syn-energy-emblem { width:58px; height:58px; flex-basis:58px; } .syn-energy-emblem :deep(svg) { width:38px; height:38px; } }

.syn-solutions { padding: clamp(66px, 8vw, 110px) 0; background: var(--surface-soft); border-block: 1px solid var(--border); }
.syn-solutions-heading { max-width: 780px; margin-bottom: 35px; }
.syn-solutions-kicker { display: block; color: var(--accent-text); font-size: 12px; font-weight: 720; letter-spacing: .095em; }
.syn-solutions-heading h2, .syn-impact-intro h2 {
  color: var(--text-strong); font-size: clamp(33px, 3.6vw, 48px); font-weight: 650;
  letter-spacing: -.04em; line-height: 1.14; margin: 13px 0 17px;
}
.syn-solutions-heading p, .syn-impact-intro p { color: var(--text-muted); font-size: 15px; line-height: 1.75; margin: 0; }
.syn-solutions-grid { display: grid; grid-template-columns: repeat(4, minmax(0,1fr)); border: 1px solid var(--border); border-radius: 18px; overflow: hidden; background: var(--surface); }
.syn-solutions-card { min-width: 0; padding: clamp(19px, 2.2vw, 30px); border-right: 1px solid var(--border); border-bottom: 1px solid var(--border); }
.syn-solutions-card:nth-child(4n) { border-right: 0; }
.syn-solutions-card:nth-last-child(-n+4) { border-bottom: 0; }
.syn-solutions-icon { display: grid; place-items: center; height: 57px; width: 57px; border-radius: 14px; background: var(--surface-accent); margin-bottom: 22px; transition: transform .25s ease; }
.syn-solutions-card:hover .syn-solutions-icon { transform: translateY(-4px); }
.syn-solutions-card h3 { margin: 0 0 9px; font-size: 17px; line-height: 1.33; font-weight: 700; letter-spacing: -.018em; color: var(--text-strong); }
.syn-solutions-card p { margin: 0; font-size: 13px; line-height: 1.7; color: var(--text-muted); }
.syn-social-impact {
  margin-top: clamp(45px, 6vw, 76px); display: grid; grid-template-columns: .95fr 1.05fr;
  gap: clamp(25px, 5vw, 70px); align-items: center;
}
.syn-impact-intro { max-width: 445px; }
.syn-impact-items { display: grid; gap: 2px; }
.syn-impact-item { padding: 18px 0; display: flex; align-items: center; gap: 19px; border-bottom: 1px solid var(--border); }
.syn-impact-item:last-child { border-bottom: none; }
.syn-impact-item > span { width: 52px; height: 52px; flex: 0 0 52px; border-radius: 14px; background: var(--surface-accent); display: grid; place-items: center; }
.syn-impact-item h3 { color: var(--text-strong); font-size: 15px; margin: 0 0 4px; font-weight: 700; }
.syn-impact-item p { margin: 0; color: var(--text-muted); font-size: 12px; line-height: 1.65; }
@media (max-width: 960px) {
 .syn-solutions-grid { grid-template-columns: repeat(2,minmax(0,1fr)); }
 .syn-solutions-card:nth-child(4n) { border-right: 1px solid var(--border); }
 .syn-solutions-card:nth-child(2n) { border-right: none; }
 .syn-solutions-card:nth-last-child(-n+4) { border-bottom: 1px solid var(--border); }
 .syn-solutions-card:nth-last-child(-n+2) { border-bottom: none; }
}
@media (max-width: 720px) {
 .syn-social-impact { grid-template-columns: 1fr; gap: 20px; }
 .syn-impact-intro { max-width: 100%; }
}
@media (max-width: 560px) {
 .syn-solutions-grid { grid-template-columns: 1fr; }
 .syn-solutions-card { border-right: 0 !important; border-bottom: 1px solid var(--border) !important; }
 .syn-solutions-card:last-child { border-bottom: none !important; }
 .syn-solutions-card { display: grid; grid-template-columns: 55px minmax(0,1fr); column-gap: 14px; padding: 21px; }
 .syn-solutions-icon { grid-row: span 2; margin: 0; width: 50px; height: 50px; }
 .syn-solutions-card h3 { font-size: 16px; }
}
@media (prefers-reduced-motion: reduce) { .syn-solutions-icon { transition: none; } }
</style>
