<script setup>
import {computed,inject,onMounted,ref,watch} from "vue";
import {useRoute} from "vue-router";
import {apiAdmin} from "../services/adminApi.js";
import AdminIcon from "../components/AdminIcon.vue";
const tabs=[["usuarios","Usuarios y permisos"],["datos","Gestión de datos"],["tarifas","Tarifas"],["respaldos","Respaldos"],["estacion","Estación y seguridad"]];
const ruta=useRoute();
const tab=ref("usuarios"),ocupado=ref(false),error=ref(""),aviso=ref("");
const terminalPrincipal=ref(null),ubicacionEstacion=ref(""),errorEstacion=ref("");
const autorizacionTablet=inject("autorizacionTablet");
const solicitudTablet=computed(()=>autorizacionTablet?.solicitud.value||null);
const codigoVisualTablet=ref("");
watch(()=>solicitudTablet.value?.solicitudId,()=>{codigoVisualTablet.value="";});
function aprobarTablet(){return ejecutar(async()=>{
 const id=solicitudTablet.value?.solicitudId;
 if(!id||!/^\d{8}$/.test(codigoVisualTablet.value))return;
 await mutar("/api/admin/terminales/vinculacion/aprobar","POST",{solicitudId:id,codigoVisual:codigoVisualTablet.value});
 codigoVisualTablet.value="";await autorizacionTablet?.actualizar();
 aviso.value="Autorización aprobada. En la tablet, pulsa Comprobar aprobación.";
});}
const usuarios=ref([]),nuevo=ref(false),nombre=ref(""),clave=ref(""),rolesNuevos=ref(["Operador"]);
const editar=ref(null),activo=ref(true),rolesEditados=ref([]),passwordId=ref(null),password=ref("");
const roles=["Administrador","Operador","Auditor"];
const explicacion={Administrador:"Acceso completo, configuración y usuarios.",Operador:"WhatsApp y PIN, cobros y operaciones.",Auditor:"Consulta de reportes sin modificar operaciones."};
const tarifas=ref(null),grupo=ref("personal"),valores=ref({});
const grupos=[["personal","Resguardo personal"],["carga","Carga de dispositivos"],["web","Reservas web normales"],["M04","Refrigeración · M04"],["M09","Conservación caliente · M09"]];
const tamanos=["Pequeno","Mediano","Grande"],modalidades=["Cable","Inalambrica","Laptop"],duraciones=[60,120,180,240];
const limpiar=ref(null),seleccion=ref([]),confirmacion=ref("");
const categorias=[["whatsapp","Solicitudes WhatsApp vencidas sin uso asociado","Solo solicitudes pendientes o canceladas sin depósito ni reserva vinculada."],["rostros","Referencias faciales de usos terminados","Referencias de resguardo cuyo retiro ya se registró."],["fotografias","Fotografías con conservación vencida","Operaciones cerradas, sin prórroga de conservación vigente."]];
const respaldos=ref([]);
const total= computed(()=>seleccion.value.reduce((n,k)=>n+(limpiar.value?.cantidades[k]||0),0));
const clavesValidas=computed(()=>Object.keys(valores.value).length>0&&Object.values(valores.value).every(v=>v!==""&&v!==null&&Number.isFinite(Number(v))&&Number(v)>0));
const nombreTamano=t=>t==="Pequeno"?"Pequeño":t==="Inalambrica"?"Inalámbrica":t;
async function mutar(ruta,metodo,body){
 const s=await apiAdmin("/api/admin/csrf-token");if(!s?.csrfToken)throw new Error("No se obtuvo el token de seguridad.");
 return apiAdmin(ruta,{method:metodo,headers:{"Content-Type":"application/json","X-CSRF-TOKEN":s.csrfToken},body:JSON.stringify(body)});
}
async function ejecutar(fn){
 if(ocupado.value)return;ocupado.value=true;error.value="";aviso.value="";
 try{await fn();}catch(e){error.value=e?.message||"No se pudo completar la operación.";}finally{ocupado.value=false;}
}
const usuarioSesion=ref("");
const esCuentaPropia=u=>u.nombreUsuario?.toLocaleLowerCase()===usuarioSesion.value.toLocaleLowerCase();
const usuariosNormales=computed(()=>usuarios.value.filter(u=>!u.esPrueba));
const cuentasPrueba=computed(()=>usuarios.value.filter(u=>u.esPrueba).length);
async function cargarUsuarios(){
 const s=await apiAdmin("/api/admin/mi-sesion");
 usuarioSesion.value=s.usuario||"";
 usuarios.value=await apiAdmin("/api/admin/usuarios/");
}
function retirarCuenta(u){return ejecutar(async()=>{
 if(esCuentaPropia(u)||!usuarioSesion.value)return;
 if(!window.confirm("¿Retirar definitivamente la cuenta "+u.nombreUsuario+"? Perderá sus permisos, contraseña y sesiones. No podrás reactivarla; el historial se conserva."))return;
 await mutar("/api/admin/usuarios/"+u.id+"/archivar","POST",{confirmacion:"RETIRAR CUENTA"});
 cancelar();await cargarUsuarios();aviso.value="Cuenta retirada del listado.";
});}
function archivarPruebas(){return ejecutar(async()=>{
 const cantidad=cuentasPrueba.value;if(!cantidad)return;
 if(!window.confirm("¿Retirar "+cantidad+" cuentas de pruebas? Se desactivarán y perderán todos sus permisos. Se conservarán únicamente sus referencias históricas. Las cuentas normales no se modificarán."))return;
 const r=await mutar("/api/admin/usuarios/archivar-pruebas","POST",{cantidadEsperada:cantidad,confirmacion:"ARCHIVAR PRUEBAS"});
 await cargarUsuarios();aviso.value=r.archivadas+" cuentas de pruebas retiradas.";
});}
function abrirUsuario(u){editar.value=u.id;activo.value=u.activo;rolesEditados.value=[...u.roles];passwordId.value=null;nuevo.value=false;clave.value="";password.value="";}
function abrirNuevo(){nuevo.value=true;editar.value=null;passwordId.value=null;password.value="";nombre.value="";clave.value="";rolesNuevos.value=["Operador"];}
function cancelar(){nuevo.value=false;editar.value=null;passwordId.value=null;clave.value="";password.value="";}
function crear(){return ejecutar(async()=>{
 if(clave.value.length<12||!rolesNuevos.value.length)throw new Error("Usa una contraseña de al menos 12 caracteres y selecciona un rol.");
 try{await mutar("/api/admin/usuarios/","POST",{nombreUsuario:nombre.value.trim(),contrasena:clave.value,roles:rolesNuevos.value});}finally{clave.value="";}
 cancelar();await cargarUsuarios();aviso.value="Usuario creado. Puede ingresar con su propia cuenta.";
});}
function guardarUsuario(){return ejecutar(async()=>{
 if(!window.confirm("¿Guardar los permisos? Se revocarán las sesiones anteriores de esta cuenta."))return;
 await mutar("/api/admin/usuarios/"+editar.value,"PUT",{activo:activo.value,roles:rolesEditados.value});cancelar();await cargarUsuarios();aviso.value="Permisos actualizados.";
});}
function abrirPassword(u){cancelar();passwordId.value=u.id;}
function restablecer(){return ejecutar(async()=>{
 if(password.value.length<12)throw new Error("La contraseña requiere 12 caracteres como mínimo.");
 if(!window.confirm("¿Restablecer la contraseña y revocar las sesiones de esta cuenta?"))return;
 try{await mutar("/api/admin/usuarios/"+passwordId.value+"/password","POST",{contrasena:password.value});}finally{password.value="";}
 cancelar();aviso.value="Contraseña restablecida.";
});}
function cargarGrupo(){
 if(!tarifas.value){valores.value={};return;}
 const t=tarifas.value;
 if(grupo.value==="personal"||grupo.value==="web")valores.value={...t[grupo.value]};
 else if(grupo.value==="carga"){
  valores.value={};for(const m of t.carga){for(const [k,v] of [["MinutosBase",m.minutosBase],["PrecioBase",m.precioBase],["MinutosAdicionales",m.minutosAdicionales],["PrecioAdicional",m.precioAdicional]])valores.value[m.modalidad+":"+k]=v;}
 }else{
  const e=t.especiales.find(x=>x.codigo===grupo.value);
  valores.value=e?Object.fromEntries(duraciones.map(m=>[String(m),e.tarifas[String(m)]??null])):{};
 }
}
async function cargarTarifas(){tarifas.value=await apiAdmin("/api/admin/configuracion/tarifas");cargarGrupo();}
function guardarTarifas(){return ejecutar(async()=>{
 if(!clavesValidas.value)return;
 if(!window.confirm("¿Actualizar las tarifas de este servicio? Los cobros ya preparados y las reservas con tarifa registrada conservarán su importe."))return;
 await mutar("/api/admin/configuracion/tarifas/"+grupo.value,"PUT",{valores:Object.fromEntries(Object.entries(valores.value).map(([k,v])=>[k,Number(v)]))});
 await cargarTarifas();aviso.value="Tarifas guardadas.";
});}
async function revisarLimpieza(){limpiar.value=null;seleccion.value=[];confirmacion.value="";limpiar.value=await apiAdmin("/api/admin/configuracion/limpieza");}
function borrar(){return ejecutar(async()=>{
 if(!limpiar.value||total.value<=0||confirmacion.value!=="ELIMINAR")return;
 const r=await mutar("/api/admin/configuracion/limpieza","POST",{categorias:seleccion.value,corteUtc:limpiar.value.corteUtc,cantidades:limpiar.value.cantidades,confirmacion:confirmacion.value});
 const n=Object.values(r.eliminados).reduce((a,b)=>a+b,0);
 await revisarLimpieza();aviso.value=n+" registros eliminados. No se modificaron casilleros ni cobros.";
});}
async function cargarRespaldos(){const d=await apiAdmin("/api/admin/configuracion/respaldos");respaldos.value=d.archivos;}
function respaldo(){return ejecutar(async()=>{
 if(!window.confirm("¿Crear una copia local de la base de pruebas? Puede contener información confidencial. No la compartas."))return;
 const r=await mutar("/api/admin/configuracion/respaldos","POST",{});await cargarRespaldos();aviso.value="Respaldo verificado: "+r.nombre;
});}
async function cargarEstacion(){
 void autorizacionTablet?.actualizar();
 terminalPrincipal.value=null;ubicacionEstacion.value="";errorEstacion.value="";
 const [registro,config]=await Promise.allSettled([
   apiAdmin("/api/admin/terminales/vinculacion/listar"),
   apiAdmin("/api/admin/configuracion/tarifas")
 ]);
 if(registro.status==="fulfilled"&&Array.isArray(registro.value?.terminales)){
   const principales=registro.value.terminales.filter(t=>t.codigo==="SYNCHRO-DEMO-01");
   if(principales.length===1)terminalPrincipal.value=principales[0];
   else errorEstacion.value="No se encontró un registro único para la estación principal.";
 }else errorEstacion.value="No fue posible consultar el registro de la estación.";
 if(config.status==="fulfilled")ubicacionEstacion.value=config.value?.ubicacion||"";
}
function cambiar(id){if(ocupado.value)return;cancelar();tab.value=id;void ejecutar(async()=>{
 if(id==="usuarios")await cargarUsuarios();
 if(id==="tarifas")await cargarTarifas();
 if(id==="estacion")await cargarEstacion();
 if(id==="datos")await revisarLimpieza();
 if(id==="respaldos")await cargarRespaldos();
});}
onMounted(()=>{if(ruta.query.seccion==="estacion")cambiar("estacion");else void ejecutar(cargarUsuarios);});
watch(()=>[ruta.query.seccion,ruta.query.solicitud],([seccion])=>{if(seccion==="estacion"&&tab.value!=="estacion")cambiar("estacion");});
const fecha=v=>new Date(v).toLocaleString("es-HN",{timeZone:"America/Tegucigalpa"});
const mb=v=>(v/1048576).toFixed(2)+" MB";
</script>

<template>
 <div class="cfg">
  <header class="page-heading"><p class="eyebrow">GESTIÓN · SYNCHRO</p><h1>Configuración</h1><p>Administra accesos, datos y tarifas de la estación.</p></header>
  <nav class="cfg-tabs" aria-label="Configuración"><button v-for="[id,texto] in tabs" :key="id" type="button" :class="{activo:tab===id}" :aria-current="tab===id?'page':undefined" :disabled="ocupado" @click="cambiar(id)">{{texto}}</button></nav>
  <p v-if="error" class="inline-error" role="alert">{{error}}</p><p v-if="aviso" class="cfg-ok" role="status">{{aviso}}</p><p v-if="ocupado" class="cfg-muted" role="status">Procesando…</p>
  <template v-if="tab==='usuarios'">
   <section class="panel cfg-panel">
    <div class="cfg-cabecera"><div><h2>Usuarios administrativos</h2><p class="cfg-muted">Cada persona debe utilizar su propia cuenta.</p></div><button type="button" class="cfg-primario" :disabled="ocupado" @click="abrirNuevo">+ Nuevo usuario</button></div>
    <form v-if="nuevo" class="cfg-form cfg-recuadro" @submit.prevent="crear"><h3>Crear usuario</h3>
     <div class="cfg-grid"><label>Nombre de usuario<input v-model.trim="nombre" minlength="3" maxlength="80" autocomplete="off" required placeholder="usuario.apellido"/></label><label>Contraseña inicial<input v-model="clave" type="password" minlength="12" maxlength="128" autocomplete="new-password" required placeholder="Mínimo 12 caracteres"/></label></div>
     <fieldset><legend>Roles y permisos</legend><label v-for="r in roles" :key="r" class="cfg-check"><input type="checkbox" v-model="rolesNuevos" :value="r"/><span><strong>{{r}}</strong><small>{{explicacion[r]}}</small></span></label></fieldset>
     <div class="cfg-acciones"><button class="cfg-secundario" type="button" @click="cancelar">Cancelar</button><button class="cfg-primario" type="submit" :disabled="ocupado||!rolesNuevos.length">Crear usuario</button></div>
    </form>
    <div v-if="cuentasPrueba" class="cfg-recuadro cfg-form"><strong>{{cuentasPrueba}} cuentas administrativas de pruebas</strong><p class="cfg-muted">Son registros de pruebas automatizadas, no cuentas de trabajo. Retíralos para conservar solo los usuarios normales y su historial de auditoría.</p><div class="cfg-acciones"><button type="button" class="cfg-peligro" :disabled="ocupado" @click="archivarPruebas">Retirar cuentas de pruebas</button></div></div>
    <div v-if="!usuariosNormales.length&&!ocupado&&!error" class="cfg-muted">No hay usuarios disponibles.</div>
    <article v-for="u in usuariosNormales" :key="u.id" class="cfg-usuario">
     <div class="cfg-avatar"><AdminIcon name="user" :size="22"/></div><div class="cfg-identidad"><strong>{{u.nombreUsuario}}</strong><small>{{u.roles.join(" · ")||"Sin permisos"}}</small></div><span class="cfg-estado" :class="{vigente:u.activo}">{{u.activo?"Activo":"Inactivo"}}</span><div class="cfg-acciones"><button type="button" class="cfg-secundario" :disabled="ocupado" @click="abrirUsuario(u)">Editar acceso</button><button type="button" class="cfg-secundario" :disabled="ocupado" @click="abrirPassword(u)">Contraseña</button><button v-if="usuarioSesion&&!esCuentaPropia(u)" type="button" class="cfg-secundario cfg-retirar" :disabled="ocupado" @click="retirarCuenta(u)">Retirar cuenta</button></div>
     <form v-if="editar===u.id" class="cfg-form cfg-edicion" @submit.prevent="guardarUsuario"><label class="cfg-check"><input v-model="activo" type="checkbox"/> Cuenta activa</label><fieldset><legend>Roles asignados</legend><label v-for="r in roles" :key="r" class="cfg-check"><input type="checkbox" v-model="rolesEditados" :value="r"/>{{r}}</label></fieldset><p class="cfg-muted">El sistema impide desactivar al último administrador activo.</p><div class="cfg-acciones"><button type="button" class="cfg-secundario" @click="cancelar">Cancelar</button><button type="submit" class="cfg-primario" :disabled="ocupado">Guardar cambios</button></div></form>
     <form v-if="passwordId===u.id" class="cfg-form cfg-edicion" @submit.prevent="restablecer"><label>Nueva contraseña<input v-model="password" type="password" minlength="12" maxlength="128" autocomplete="new-password" required/></label><div class="cfg-acciones"><button type="button" class="cfg-secundario" @click="cancelar">Cancelar</button><button type="submit" class="cfg-primario" :disabled="ocupado">Restablecer contraseña</button></div></form>
    </article>
   </section>
  </template>
  <template v-else-if="tab==='datos'">
   <section class="panel cfg-panel"><div class="cfg-cabecera"><div><h2>Limpieza selectiva</h2><p class="cfg-muted">Revisa los registros que ya no son necesarios.</p></div><button class="cfg-secundario" type="button" :disabled="ocupado" @click="ejecutar(revisarLimpieza)">Revisar registros</button></div>
    <div v-if="limpiar"><label v-for="[id,t,d] in categorias" :key="id" class="cfg-limpieza"><input type="checkbox" :value="id" v-model="seleccion" :disabled="ocupado||limpiar.cantidades[id]===0"/><span><strong>{{t}}</strong><small>{{d}}</small></span><strong>{{limpiar.cantidades[id]}}</strong></label>
     <div class="cfg-recuadro cfg-form"><strong>{{total}} registros seleccionados</strong><p class="cfg-muted">Esta operación es irreversible. No elimina los movimientos, cobros, cuentas ni auditorías; tampoco cambia los estados de los casilleros. La revisión vence en cinco minutos.</p><label>Escribe ELIMINAR para confirmar<input v-model="confirmacion" autocomplete="off" :disabled="total===0||ocupado"/></label><div class="cfg-acciones"><button type="button" class="cfg-peligro" :disabled="ocupado||total===0||confirmacion!=='ELIMINAR'" @click="borrar">Eliminar seleccionados</button></div></div>
    </div>
   </section>
   <section class="panel cfg-panel"><h2>Historial y operaciones</h2><p>La eliminación de movimientos, reservas, clientes y cobros históricos no está habilitada. Se conservan para evitar perder trazabilidad o romper vínculos entre registros. Las operaciones vigentes no se eliminan desde esta pantalla.</p></section>
  </template>
  <template v-else-if="tab==='tarifas'">
   <section class="panel cfg-panel"><div class="cfg-cabecera"><div><h2>Tarifas de los servicios</h2><p class="cfg-muted">Precios en lempiras. Edita un grupo y guarda sus cambios.</p></div><button class="cfg-secundario" type="button" :disabled="ocupado" @click="ejecutar(cargarTarifas)">Actualizar</button></div>
    <nav class="cfg-tabs cfg-subtabs"><button v-for="[id,t] in grupos" :key="id" :class="{activo:grupo===id}" type="button" :disabled="ocupado" @click="grupo=id;cargarGrupo()">{{t}}</button></nav>
    <form v-if="tarifas&&Object.keys(valores).length" class="cfg-form" @submit.prevent="guardarTarifas">
     <div class="cfg-tabla-scroll">
      <table v-if="grupo==='personal'"><thead><tr><th>Tamaño</th><th>Primera hora · L</th><th>30 min adicionales · L</th></tr></thead><tbody><tr v-for="t in tamanos" :key="t"><td>{{nombreTamano(t)}}</td><td><input :aria-label="'Primera hora '+t" v-model.number="valores['CobroRetiroPersonal:Tarifas:'+t]" type="number" min="0.01" max="100000" step="0.01" required/></td><td><input :aria-label="'Adicional '+t" v-model.number="valores['CobroRetiroPersonal:Adicionales:'+t]" type="number" min="0.01" max="100000" step="0.01" required/></td></tr></tbody></table>
      <table v-else-if="grupo==='carga'"><thead><tr><th>Modalidad</th><th>Minutos base</th><th>Precio base · L</th><th>Minutos extra</th><th>Precio extra · L</th></tr></thead><tbody><tr v-for="m in modalidades" :key="m"><td>{{nombreTamano(m)}}</td><td v-for="k in ['MinutosBase','PrecioBase','MinutosAdicionales','PrecioAdicional']" :key="k"><input :aria-label="m+' '+k" v-model.number="valores[m+':'+k]" type="number" :min="k.startsWith('Minutos')?1:0.01" :max="k.startsWith('Minutos')?1440:100000" :step="k.startsWith('Minutos')?1:0.01" required/></td></tr></tbody></table>
      <table v-else-if="grupo==='web'"><thead><tr><th>Tamaño</th><th v-for="m in duraciones" :key="m">{{m}} min · L</th></tr></thead><tbody><tr v-for="t in tamanos" :key="t"><td>{{nombreTamano(t)}}</td><td v-for="m in duraciones" :key="m"><input :aria-label="t+' '+m+' minutos'" v-model.number="valores['ReservasPrototipo:TarifasNormales:'+t+':'+m]" type="number" min="0.01" max="100000" step="0.01" required/></td></tr></tbody></table>
      <table v-else><thead><tr><th>Duración</th><th>Precio · L</th></tr></thead><tbody><tr v-for="m in duraciones" :key="m"><td>{{m}} minutos</td><td><input :aria-label="m+' minutos'" v-model.number="valores[String(m)]" type="number" min="0.01" max="100000" step="0.01" required/></td></tr></tbody></table>
     </div>
     <div class="cfg-acciones"><button class="cfg-secundario" type="button" :disabled="ocupado" @click="cargarGrupo">Descartar cambios</button><button type="submit" class="cfg-primario" :disabled="ocupado||!clavesValidas">Guardar tarifas</button></div>
    </form><p v-else-if="!ocupado" class="cfg-muted">No hay configuración disponible para este servicio.</p>
    <p class="cfg-muted">Las tarifas de resguardo y reservas normales se conservan en la configuración local; las de carga y temperatura se guardan en SQL Server. No se modifican importes de cobros ya preparados.</p>
   </section>
  </template>
  <template v-else-if="tab==='respaldos'">
   <section class="panel cfg-panel"><div class="cfg-cabecera"><div><h2>Respaldos de SQL Server</h2><p class="cfg-muted">Copia local de la base aislada del prototipo.</p></div><button type="button" class="cfg-primario" :disabled="ocupado" @click="respaldo">Crear respaldo</button></div>
    <div class="cfg-recuadro"><strong>Ubicación en el equipo del backend</strong><p class="cfg-ruta">%LOCALAPPDATA%\SYNCHRO\Configuracion\Respaldos</p><p class="cfg-muted">Contiene datos confidenciales. No lo publiques ni lo compartas. No incluye las claves de protección ni el archivo local de tarifas. La restauración no se ejecuta desde el navegador.</p></div>
    <p v-if="!respaldos.length&&!ocupado" class="cfg-muted">No hay respaldos en esta carpeta.</p><div v-for="r in respaldos" :key="r.nombre" class="cfg-archivo"><AdminIcon name="report" :size="22"/><span><strong>{{r.nombre}}</strong><small>{{fecha(r.creadoUtc)}} · {{mb(r.bytes)}}</small></span></div>
   </section>
  </template>
  <template v-else>
   <section class="panel cfg-panel">
    <div class="cfg-cabecera"><div><h2>Estación principal</h2><p class="cfg-muted">Información del registro de la estación SYNCHRO.</p></div><button class="cfg-secundario" type="button" :disabled="ocupado" @click="ejecutar(cargarEstacion)">Actualizar</button></div>
    <p v-if="errorEstacion" class="inline-error" role="alert">{{errorEstacion}}</p>
    <article v-if="terminalPrincipal" class="cfg-estacion">
      <div class="cfg-avatar"><AdminIcon name="lockers" :size="24"/></div>
      <div class="cfg-identidad"><strong>{{terminalPrincipal.nombre}}</strong><small>Código: {{terminalPrincipal.codigo}} · ID: {{terminalPrincipal.id}}</small></div>
      <span class="cfg-estado" :class="{vigente:terminalPrincipal.activa}">{{terminalPrincipal.activa?"Activa":"Inactiva"}}</span>
    </article>
    <p class="cfg-muted">El estado corresponde al registro de la estación, no a una medición de conexión en tiempo real.</p>
    <div class="cfg-recuadro"><strong>Ubicación registrada</strong><p>{{ubicacionEstacion||"No disponible"}}</p></div>
   </section>
   <section v-if="solicitudTablet" class="panel cfg-panel">
    <h2>Autorizar tablet</h2><p>La estación ha solicitado una nueva autorización. Comprueba el número de solicitud y escribe el código de ocho dígitos que aparece en la tablet.</p>
    <form class="cfg-form" @submit.prevent="aprobarTablet">
      <div class="cfg-grid"><label>Número de solicitud<input :value="solicitudTablet.solicitudId" readonly/></label><label>Código de aprobación<input v-model.trim="codigoVisualTablet" type="text" inputmode="numeric" maxlength="8" pattern="[0-9]{8}" autocomplete="off" required/></label></div>
      <div class="cfg-acciones"><button type="submit" class="cfg-primario" :disabled="ocupado||!/^\d{8}$/.test(codigoVisualTablet)">Aprobar estación</button></div>
    </form>
    <p class="cfg-muted">Solo aprueba una solicitud que reconozcas. La solicitud vence a los cinco minutos.</p>
   </section>
   <section class="panel cfg-panel"><h2>Seguridad</h2><p>Las autorizaciones de WhatsApp, la verificación facial y la vigencia de PIN mantienen sus controles actuales.</p><p class="cfg-muted">La modificación de la ubicación, disponibilidad de servicios y parámetros de seguridad todavía no está habilitada desde esta interfaz.</p></section>
  </template>
 </div>
</template>
<style scoped>
.cfg{display:grid;gap:18px;color:var(--ink);min-width:0;}
.cfg .page-heading{margin:0;}
.cfg-tabs{display:flex;flex-wrap:wrap;gap:8px;border:1px solid var(--border);border-radius:16px;padding:6px;width:fit-content;max-width:100%;background:var(--white);}
.cfg-tabs button{background:transparent;border:0;border-radius:10px;padding:12px 17px;color:var(--ink);font:inherit;font-size:14px;font-weight:750;cursor:pointer;}
.cfg-tabs button.activo{background:#00b9c7;color:#01305c;}
.cfg-tabs button:hover:not(.activo){background:var(--canvas);}
.cfg-panel{padding:24px;min-width:0;display:grid;gap:16px;}
.cfg-panel h2,.cfg-panel h3,.cfg-panel p{margin:0;}
.cfg-panel h2{font-size:21px;}
.cfg-panel p{line-height:1.6;}
.cfg-muted{color:var(--muted);font-size:13px;}
.cfg-cabecera,.cfg-acciones{display:flex;gap:10px;flex-wrap:wrap;align-items:center;justify-content:space-between;}
.cfg-acciones{justify-content:flex-end;}
.cfg-primario,.cfg-secundario,.cfg-peligro{min-height:42px;border:1px solid transparent;border-radius:10px;padding:10px 16px;font:inherit;font-size:13px;font-weight:750;cursor:pointer;}
.cfg-primario{background:#00b9c7;color:#01305c;}
.cfg-secundario{background:var(--white);color:var(--ink);border-color:var(--border);}
.cfg-peligro{background:#b42318;color:#fff;}
.cfg-retirar{color:#b42318;border-color:#e9b8b4;}
.cfg button:disabled{opacity:.5;cursor:not-allowed;}
.cfg-recuadro,.cfg-edicion{border:1px solid var(--border);background:var(--canvas);padding:16px;border-radius:12px;}
.cfg-form{display:grid;gap:14px;}
.cfg-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:13px;}
.cfg-form label{display:grid;gap:6px;font-size:13px;font-weight:700;}
.cfg-form input:not([type=checkbox]){width:100%;min-width:0;box-sizing:border-box;padding:11px;border:1px solid var(--border);border-radius:9px;background:var(--white);color:var(--ink);font:inherit;}
.cfg-form fieldset{border:1px solid var(--border);border-radius:10px;display:grid;gap:11px;padding:12px;min-width:0;}
.cfg-form legend{font-size:13px;font-weight:750;}
.cfg-form .cfg-check,.cfg-limpieza{display:flex;align-items:center;gap:12px;}
.cfg-check input,.cfg-limpieza input{accent-color:#008d9d;width:18px;height:18px;flex-shrink:0;}
.cfg-check small,.cfg-identidad small,.cfg-limpieza small,.cfg-archivo small{display:block;color:var(--muted);font-size:12px;font-weight:400;line-height:1.5;}
.cfg-estacion{display:flex;flex-wrap:wrap;align-items:center;gap:12px;padding:16px;border:1px solid var(--border);border-radius:12px;}
.cfg-usuario{display:flex;flex-wrap:wrap;align-items:center;gap:12px;padding:16px 0;border-top:1px solid var(--border);}
.cfg-avatar{display:grid;place-items:center;width:44px;height:44px;background:var(--canvas);border:1px solid var(--border);border-radius:11px;color:#009cab;}
.cfg-identidad{flex:1;min-width:140px;overflow-wrap:anywhere;}
.cfg-estado{border-radius:8px;background:var(--canvas);color:var(--muted);padding:6px 9px;font-size:12px;}
.cfg-estado.vigente{color:#008879;background:#e1f7f1;}
.cfg-edicion{flex-basis:100%;}
.cfg-limpieza{padding:16px 0;border-bottom:1px solid var(--border);}
.cfg-limpieza span{flex:1;}
.cfg-limpieza+div{margin-top:16px;}
.cfg-subtabs{padding:5px;}
.cfg-tabla-scroll{overflow-x:auto;}
.cfg table{width:100%;border-collapse:collapse;font-size:13px;text-align:left;}
.cfg td,.cfg th{padding:11px 8px;border-bottom:1px solid var(--border);}
.cfg th{background:var(--canvas);color:var(--muted);font-size:12px;white-space:nowrap;}
.cfg td input{min-width:82px!important;}
.cfg-ok{padding:13px;background:var(--canvas);border:1px solid #009c8d;border-radius:10px;margin:0;}
.cfg-archivo{display:flex;align-items:center;gap:12px;border-top:1px solid var(--border);padding:12px 0;overflow-wrap:anywhere;}
.cfg-ruta{font-family:monospace;overflow-wrap:anywhere;margin:8px 0!important;}
@media(max-width:650px){.cfg-grid{grid-template-columns:1fr;}.cfg-panel{padding:16px;}.cfg-tabs{width:100%;}.cfg-tabs button{flex:1;}.cfg-usuario .cfg-acciones{width:100%;}.cfg-cabecera{align-items:flex-start;}}
</style>