<script setup>
import AppIcon from "../ui/AppIcon.vue";

const protecciones = Object.freeze([
  {
    icono: "facial", titulo: "Verificación facial",
    descripcion: "Un paso adicional de identificación para proteger el acceso y la entrega de pertenencias."
  },
  {
    icono: "pin", titulo: "Códigos de acceso",
    descripcion: "Validación por PIN de un solo uso para reforzar la seguridad en los retiros."
  },
  {
    icono: "alarma", titulo: "Alertas ante incidentes",
    descripcion: "Sistemas de aviso pensados para responder ante incidencias y situaciones fuera de lo habitual."
  },
  {
    icono: "sensores", titulo: "Monitoreo con sensores",
    descripcion: "Supervisión de condiciones y eventos para apoyar el cuidado de los objetos almacenados."
  },
  {
    icono: "braille", titulo: "Identificación en braille",
    descripcion: "Identificación táctil bajo el código de cada casillero para facilitar su reconocimiento."
  },
  {
    icono: "control-acceso", titulo: "Control de acceso",
    descripcion: "Procesos de autorización que ayudan a que cada entrega llegue a la persona indicada."
  },
  {
    icono: "trazabilidad", titulo: "Registro de operaciones",
    descripcion: "Seguimiento de las acciones realizadas para dar claridad a la administración del servicio."
  },
  {
    icono: "sensor-temperatura", titulo: "Cuidado de la temperatura",
    descripcion: "Espacios preparados para necesidades específicas de conservación en frío o calor."
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
          Así conectamos servicios con necesidades reales.
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
