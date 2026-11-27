<script setup>
import { onMounted,ref } from "vue";
import { apiAdmin } from "../services/adminApi";
import SynButton from "../components/ui/SynButton.vue";

const terminales=ref([]);
const solicitudId=ref("");
const codigoVisual=ref("");
const cargando=ref(false);
const aprobando=ref(false);
const error=ref("");
const exito=ref("");

async function listar(){
  cargando.value=true;error.value="";
  try{
    const datos=await apiAdmin("/api/admin/terminales/vinculacion/listar");
    if(!Array.isArray(datos?.terminales))throw new Error("No se recibió el registro de estaciones.");
    terminales.value=datos.terminales;
  }catch(e){error.value=e.message||"No fue posible consultar las estaciones.";}
  finally{cargando.value=false;}
}
async function aprobar(){
  if(aprobando.value||!/^[1-9][0-9]*$/.test(solicitudId.value.trim())||
     !/^[0-9]{8}$/.test(codigoVisual.value.trim()))return;
  aprobando.value=true;error.value="";exito.value="";
  try{
    const datosCsrf=await apiAdmin("/api/admin/csrf-token");
    if(!datosCsrf?.csrfToken)throw new Error("No se pudo obtener el token de seguridad.");
    await apiAdmin("/api/admin/terminales/vinculacion/aprobar",{
      method:"POST",
      headers:{"Content-Type":"application/json","X-CSRF-TOKEN":datosCsrf.csrfToken},
      body:JSON.stringify({
        solicitudId:Number(solicitudId.value.trim()),
        codigoVisual:codigoVisual.value.trim()
      })
    });
    exito.value="Solicitud aprobada. En la tablet, pulsa «Comprobar aprobación».";
    solicitudId.value="";codigoVisual.value="";
    await listar();
  }catch(e){error.value=e.message||"No se pudo aprobar la solicitud.";}
  finally{aprobando.value=false;}
}
onMounted(listar);
</script>

<template>
  <div class="terminales-admin">
    <header class="page-heading">
      <div><p class="eyebrow">INFRAESTRUCTURA</p><h1>Terminales</h1>
      <p>Autoriza la primera vinculación o renueva la sesión de una tablet sin desactivar la estación.</p></div>
    </header>
    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <p v-if="exito" class="mensaje-aprobado" role="status">{{ exito }}</p>
    <section class="panel panel-terminales">
      <div class="panel-titulo">
        <div><h2>Estaciones registradas</h2><p>Consulta el código que debes introducir en la tablet.</p></div>
        <SynButton variante="secundario" :disabled="cargando" @click="listar">{{ cargando?'Actualizando…':'Actualizar' }}</SynButton>
      </div>
      <p v-if="cargando" role="status">Consultando estaciones…</p>
      <p v-else-if="!terminales.length">No hay estaciones registradas.</p>
      <div v-else class="listado">
        <div v-for="t in terminales" :key="t.id" class="estacion">
          <div><strong>{{ t.nombre }}</strong><small>ID: {{ t.id }} · Código: {{ t.codigo }}</small></div>
          <span class="estado" :class="{activa:t.activa}">{{ t.activa?'Activa':'Sin vincular' }}</span>
        </div>
      </div>
    </section>
    <section class="panel panel-terminales">
      <h2>Aprobar solicitud de autorización</h2>
      <p>Utiliza exclusivamente el número de solicitud y el código de 8 dígitos que aparecen en la pantalla de la tablet. La aprobación es válida durante cinco minutos.</p>
      <form class="formulario" @submit.prevent="aprobar">
        <label>Número de solicitud
          <input v-model.trim="solicitudId" type="text" inputmode="numeric" autocomplete="off" placeholder="Número de solicitud" required />
        </label>
        <label>Código de aprobación
          <input v-model.trim="codigoVisual" type="text" inputmode="numeric" maxlength="8" autocomplete="off" placeholder="8 dígitos" required />
        </label>
        <SynButton :disabled="aprobando||!/^[1-9][0-9]*$/.test(solicitudId)||!/^[0-9]{8}$/.test(codigoVisual)" tipo="submit">
          {{ aprobando?'Aprobando…':'Aprobar estación' }}
        </SynButton>
      </form>
      <p class="aviso-seguridad">Esta operación requiere permisos de administrador. No se utilizan el PIN del cliente ni datos faciales.</p>
    </section>
  </div>
</template>

<style scoped>
.terminales-admin{display:grid;gap:18px;}
.panel-terminales{padding:clamp(16px,2.5vw,26px);display:grid;gap:15px;}
.panel-terminales h2{margin:0;font-size:20px;}
.panel-terminales p{margin:0;line-height:1.5;color:var(--text-muted,#65778b);}
.panel-titulo{display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px;}
.listado{display:grid;gap:9px;}
.estacion{display:flex;align-items:center;justify-content:space-between;gap:16px;flex-wrap:wrap;padding:14px;border:1px solid var(--border,#d8e4ec);border-radius:13px;}
.estacion div{display:grid;gap:4px;}
.estacion small{opacity:.8;}
.estado{font-size:12px;border-radius:99px;padding:6px 10px;background:#f1f0e8;color:#695c32;font-weight:750;}
.estado.activa{background:#dff7f4;color:#075954;}
.formulario{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;align-items:end;}
.formulario label{display:grid;gap:6px;font-size:13px;font-weight:750;}
.formulario input{width:100%;min-height:47px;padding:10px 12px;background:var(--surface,#fff);color:inherit;border:1px solid var(--border,#bacfd9);border-radius:11px;box-sizing:border-box;}
.formulario button{min-height:47px;}
.mensaje-aprobado{padding:12px;border:1px solid #00b9c7;border-radius:11px;color:#00878b;font-weight:750;}
.aviso-seguridad{font-size:12px;}
@media(max-width:700px){.formulario{grid-template-columns:1fr;}}
</style>
