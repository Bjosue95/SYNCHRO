<script setup>
import { ref, onBeforeUnmount } from "vue";
import { RouterLink, useRouter } from "vue-router";
import { useTema } from "../composables/useTema";

const { oscuro } = useTema();
const router = useRouter();
const codigoTerminal=ref("");
const solicitud=ref(null);
const procesando=ref(false);
const error=ref("");
const aviso=ref("");
let tokenTemporal=null;
let viva=true;

async function consultar(url,cuerpo){
  const respuesta=await fetch(url,{
    method:"POST", credentials:"same-origin",cache:"no-store",
    headers:{"Content-Type":"application/json"},
    body:JSON.stringify(cuerpo)
  });
  const datos=await respuesta.json().catch(()=>null);
  if(!respuesta.ok){
    const fallo=new Error(datos?.mensaje||datos?.title||"No se pudo completar la autorización.");
    fallo.status=respuesta.status;
    throw fallo;
  }
  return datos;
}

async function solicitar(){
  if(procesando.value||!codigoTerminal.value.trim()||solicitud.value)return;
  error.value="";aviso.value="";procesando.value=true;
  try{
    const d=await consultar("/api/terminales/vinculacion/solicitar",
      {codigoTerminal:codigoTerminal.value.trim()});
    if(!Number.isInteger(d?.solicitudId)||!/^[0-9]{8}$/.test(d?.codigoVisual||"")||typeof d?.token!=="string")
      throw new Error("La solicitud no tiene datos válidos.");
    if(!viva)return;
    tokenTemporal=d.token;
    solicitud.value={id:d.solicitudId,codigo:d.codigoVisual};
  }catch(e){if(viva)error.value=e.message||"No se pudo crear la solicitud.";}
  finally{if(viva)procesando.value=false;}
}

async function confirmar(){
  if(procesando.value||!solicitud.value||!tokenTemporal)return;
  error.value="";aviso.value="";procesando.value=true;
  try{
    const d=await consultar("/api/terminales/vinculacion/confirmar",
      {token:tokenTemporal});
    if(!Number.isInteger(d?.terminalId)||d.terminalId<=0)
      throw new Error("El servidor no confirmó la sesión.");
    if(!viva)return;
    tokenTemporal=null;
    solicitud.value=null;
    aviso.value="Estación autorizada.";
    await router.replace("/guardar/reserva");
  }catch(e){
    if(viva)error.value=e.status===401
      ?"La solicitud todavía no está aprobada o ya venció. Comprueba la aprobación del administrador."
      :e.message||"No se pudo confirmar la terminal.";
  }finally{if(viva)procesando.value=false;}
}

onBeforeUnmount(()=>{viva=false;tokenTemporal=null;});
</script>

<template>
  <main class="pantalla-autorizacion" :class="{'tema-oscuro':oscuro}">
    <RouterLink to="/guardar/reserva" class="volver">
      <img src="/iconos/complementarios/28_regresar.svg" alt="" class="icono-claro" />
      <img src="/iconos/complementarios/28_regresar-oscuro.svg" alt="" class="icono-oscuro" />
      <span>Volver</span>
    </RouterLink>
    <section class="contenido">
      <h1>Autorizar estación</h1>
      <div class="tarjeta">
        <template v-if="!solicitud">
          <p>Introduce el código asignado a esta estación. Puedes consultarlo en el apartado Terminales del administrador.</p>
          <label for="codigo-terminal">Código de estación</label>
          <input id="codigo-terminal" v-model.trim="codigoTerminal" maxlength="30" autocomplete="off" spellcheck="false" placeholder="Código de terminal" />
          <button type="button" class="accion" :disabled="procesando||!codigoTerminal.trim()" @click="solicitar">
            {{ procesando?'Solicitando…':'Solicitar autorización' }}
          </button>
        </template>
        <template v-else>
          <p>Comunica al administrador este número de solicitud y el código de aprobación. El token secreto permanece únicamente en esta pantalla.</p>
          <div class="datos"><span>Solicitud <strong>#{{ solicitud.id }}</strong></span>
            <span>Código de aprobación <strong class="codigo">{{ solicitud.codigo }}</strong></span></div>
          <p>En Administración → Terminales, el administrador debe aprobar esta solicitud. Después pulsa «Comprobar aprobación».</p>
          <button type="button" class="accion" :disabled="procesando" @click="confirmar">
            {{ procesando?'Comprobando…':'Comprobar aprobación' }}
          </button>
          <small>La autorización vence en 5 minutos. No cierres ni actualices esta pantalla mientras se aprueba.</small>
        </template>
        <p v-if="error" role="alert" class="error">{{ error }}</p>
        <p v-if="aviso" role="status" class="aviso">{{ aviso }}</p>
      </div>
    </section>
  </main>
</template>

<style scoped>
.pantalla-autorizacion{display:grid;grid-template-rows:auto minmax(0,1fr);gap:clamp(8px,1.4vh,16px);width:100%;height:100%;padding:clamp(10px,1.7vh,20px) clamp(12px,2.4vw,28px);background:#f2f7fa;color:#01305c;box-sizing:border-box;overflow:auto;}
.volver{display:inline-flex;align-items:center;gap:10px;justify-self:start;position:relative;min-height:52px;padding:8px 18px;border:1.5px solid #c7e2e9;border-radius:13px;background:#e4f2f6;color:inherit;font-weight:700;text-decoration:none;}
.volver img{width:26px;height:26px;object-fit:contain;}
.volver .icono-oscuro{display:none;}
.contenido{width:min(100%,680px);place-self:center;display:grid;gap:18px;min-height:0;}
.contenido h1{font-size:clamp(24px,3vw,38px);margin:0;text-align:center;}
.tarjeta{background:white;border:2px solid #dfeaf0;border-radius:22px;padding:clamp(17px,3vw,35px);display:grid;gap:15px;box-shadow:0 3px 12px rgba(1,48,92,.05);}
.tarjeta p{margin:0;font-size:clamp(14px,1.8vw,19px);line-height:1.5;}
.tarjeta label{font-weight:750;}
.tarjeta input{width:100%;min-width:0;height:56px;padding:12px 15px;border:2px solid #b8d9e2;border-radius:14px;background:inherit;color:inherit;font-size:19px;box-sizing:border-box;}
.tarjeta .accion{min-height:56px;padding:10px 20px;border:2px solid #00a8b8;border-radius:14px;background:#00b9c7;color:#01305c;font:inherit;font-size:18px;font-weight:800;cursor:pointer;}
.accion:disabled{opacity:.55;cursor:wait;}
.datos{display:flex;flex-wrap:wrap;gap:15px;align-items:center;}
.datos span{flex:1 1 200px;display:grid;gap:5px;font-size:14px;}
.datos strong{font-size:24px;letter-spacing:.025em;}
.datos .codigo{font-size:clamp(26px,4vw,39px);font-variant-numeric:tabular-nums;letter-spacing:.12em;}
.tarjeta small{font-size:13px;line-height:1.4;opacity:.9;}
.error{color:#b13d2c;font-weight:650;}.aviso{color:#007781;font-weight:700;}
.tema-oscuro{background:#061f36;color:#fff;}
.tema-oscuro .volver{background:#204766;border-color:#42647e;}
.tema-oscuro .icono-claro{display:none;}.tema-oscuro .volver .icono-oscuro{display:block;}
.tema-oscuro .tarjeta{background:#103956;border-color:#315573;}
.tema-oscuro .error{color:#ffcfba;}.tema-oscuro .aviso{color:#82e4ed;}
:focus-visible{outline:3px solid #00b9c7;outline-offset:3px;}
@media(max-height:570px){.pantalla-autorizacion{padding:7px 12px;gap:7px;}.volver{min-height:42px;}.contenido{gap:8px;}.tarjeta{gap:10px;padding:12px;}.tarjeta input,.tarjeta .accion{min-height:46px;height:auto;}}
</style>
