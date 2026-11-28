<script setup lang="ts">
import { RouterLink } from 'vue-router';
import { computed } from 'vue';
import { estadoSesion } from '../services/sesion.js';
import AppIcon from '../components/ui/AppIcon.vue';
import LockerJourney from '../components/ui/LockerJourney.vue';

const usuario=computed(()=> (estadoSesion.cliente as {nombreUsuario?:string}|null)?.nombreUsuario || '');
</script>

<template>
  <section class="inicio-cliente" aria-labelledby="inicio-cliente-titulo">
    <div class="inicio-cliente__presentacion">
      <div class="inicio-cliente__texto">
        <h1 id="inicio-cliente-titulo">Bienvenido{{ usuario ? ', '+usuario : '' }}.</h1>
        <p>Guarda tus pertenencias o gestiona una encomienda desde tu cuenta. Elige un servicio para comenzar cuando lo necesites.</p>
        <div class="inicio-cliente__acciones">
          <RouterLink to="/reservar" class="inicio-cliente__accion inicio-cliente__accion--principal">
            <AppIcon nombre="casillero" :tamano="24" />
            <span>Reservar casillero</span>
            <AppIcon nombre="flecha" :tamano="20" />
          </RouterLink>
          <RouterLink to="/mis-reservas" class="inicio-cliente__accion inicio-cliente__accion--secundaria">
            <AppIcon nombre="reservas" :tamano="21" />
            Mis reservas
          </RouterLink>
        </div>
      </div>
      <div class="inicio-cliente__escena">
        <div class="inicio-cliente__estacion">
          <LockerJourney compacto />
          <p class="inicio-cliente__detalle">El punto donde todo se conecta</p>
        </div>
      </div>
    </div>

    <div class="inicio-cliente__servicios">
      <div class="inicio-cliente__encabezado">
        <h2>¿Qué puedes hacer en SYNCHRO?</h2>
        <p>Selecciona una opción y sigue los pasos de la plataforma.</p>
      </div>
      <div class="inicio-cliente__rejilla">
        <RouterLink :to="{path:'/reservar',query:{servicio:'Personal'}}" class="inicio-cliente__tarjeta">
          <span class="inicio-cliente__icono"><AppIcon nombre="guardar" :tamano="34"/></span>
          <h3>Guardar pertenencias</h3>
          <p>Elige un casillero, una ubicación y el tiempo que necesitas.</p>
          <span class="inicio-cliente__enlace">Comenzar <AppIcon nombre="flecha" :tamano="17"/></span>
        </RouterLink>
        <RouterLink :to="{path:'/reservar',query:{servicio:'Encomienda'}}" class="inicio-cliente__tarjeta">
          <span class="inicio-cliente__icono"><AppIcon nombre="encomienda" :tamano="34"/></span>
          <h3>Enviar una encomienda</h3>
          <p>Prepara una reserva para que otra persona pueda recogerla.</p>
          <span class="inicio-cliente__enlace">Comenzar <AppIcon nombre="flecha" :tamano="17"/></span>
        </RouterLink>
        <RouterLink to="/mis-reservas" class="inicio-cliente__tarjeta">
          <span class="inicio-cliente__icono"><AppIcon nombre="historial" :tamano="34"/></span>
          <h3>Consultar tus reservas</h3>
          <p>Revisa solicitudes, pagos, depósitos y retiros desde tu historial.</p>
          <span class="inicio-cliente__enlace">Ver reservas <AppIcon nombre="flecha" :tamano="17"/></span>
        </RouterLink>
      </div>
    </div>
    <div class="inicio-cliente__ayuda">
      <AppIcon nombre="seguridad" :tamano="23"/>
      <p><strong>Un proceso sencillo:</strong> selecciona tu servicio, revisa la reserva y sigue las indicaciones de SYNCHRO para el depósito o retiro.</p>
    </div>
  </section>
</template>

<style scoped>
.inicio-cliente{display:grid;gap:clamp(20px,3vw,34px);padding:clamp(10px,1.5vw,22px) 0 32px;color:var(--text-strong);}
.inicio-cliente__presentacion{display:grid;grid-template-columns:minmax(0,1.1fr) minmax(0,1fr);overflow:hidden;border-radius:22px;border:1px solid var(--border);background:var(--surface);min-height:300px;}
.inicio-cliente__texto{display:flex;flex-direction:column;align-items:flex-start;justify-content:center;gap:clamp(12px,2vw,20px);padding:clamp(22px,4vw,52px);}
.inicio-cliente__texto h1{font-size:clamp(27px,4.4vw,46px);line-height:1.15;letter-spacing:-.03em;overflow-wrap:anywhere;}
.inicio-cliente__texto>p{max-width:54ch;line-height:1.6;font-size:clamp(13px,1.5vw,16px);color:var(--text-muted);}
.inicio-cliente__acciones{display:flex;align-items:stretch;gap:11px;flex-wrap:wrap;}
.inicio-cliente__accion{display:inline-flex;align-items:center;justify-content:center;gap:11px;padding:13px 17px;min-height:52px;border-radius:13px;text-decoration:none;font-size:13px;font-weight:800;border:2px solid;transition:background-color .15s ease,border-color .15s ease;}
.inicio-cliente__accion--principal{background:#01305c;color:white;border-color:#00b9c7;}
.inicio-cliente__accion--principal:hover{background:#07517a;}
.inicio-cliente__accion--secundaria{background:var(--surface-soft);color:var(--text-strong);border-color:var(--border-strong);}
.inicio-cliente__accion--secundaria:hover{border-color:var(--brand-accent);}
.inicio-cliente__escena{display:grid;place-items:center;min-width:0;padding:clamp(16px,3vw,36px);background:linear-gradient(135deg,#01305c 0%,#075478 65%,#008d9d 100%);}
.inicio-cliente__estacion{width:100%;max-width:640px;min-width:0;display:grid;gap:10px;justify-items:center;}
.inicio-cliente__detalle{margin:0;color:#e1f5fa;text-align:center;font-size:clamp(11px,1.1vw,14px);line-height:1.4;font-weight:500;}
.inicio-cliente__servicios{display:grid;gap:17px;}
.inicio-cliente__encabezado{display:grid;gap:6px;}
.inicio-cliente__encabezado h2{font-size:clamp(19px,2.4vw,26px);}
.inicio-cliente__encabezado p{font-size:13px;color:var(--text-muted);}
.inicio-cliente__rejilla{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:15px;}
.inicio-cliente__tarjeta{display:flex;flex-direction:column;gap:11px;min-height:230px;padding:clamp(16px,2vw,24px);border:1px solid var(--border);border-radius:18px;text-decoration:none;background:var(--surface);color:var(--text-strong);transition:border-color .15s ease,transform .15s ease;}
.inicio-cliente__tarjeta:hover{border-color:var(--brand-accent);transform:translateY(-2px);}
.inicio-cliente__icono{width:58px;height:58px;display:grid;place-items:center;background:var(--surface-accent);border:1px solid var(--border);border-radius:15px;}
.inicio-cliente__tarjeta h3{font-size:clamp(14px,1.6vw,18px);}
.inicio-cliente__tarjeta p{font-size:13px;line-height:1.55;color:var(--text-muted);}
.inicio-cliente__enlace{margin-top:auto;display:flex;align-items:center;gap:7px;font-size:12px;font-weight:800;color:var(--accent-text);}
.inicio-cliente__ayuda{display:flex;align-items:center;gap:13px;padding:14px 17px;border-radius:13px;border:1px solid var(--border);background:var(--surface-soft);}
.inicio-cliente__ayuda p{font-size:12px;line-height:1.5;color:var(--text-muted);}
.inicio-cliente__ayuda strong{color:var(--text-strong);}
.inicio-cliente a:focus-visible{outline:3px solid var(--ring);outline-offset:3px;}
@media(max-width:900px){.inicio-cliente__presentacion{grid-template-columns:minmax(0,1fr) minmax(0,1fr);}.inicio-cliente__rejilla{gap:10px;}}
@media(max-width:650px){.inicio-cliente__presentacion{grid-template-columns:1fr;}.inicio-cliente__escena{padding:12px 18px 20px;}.inicio-cliente__texto{padding:23px 19px;}.inicio-cliente__acciones{width:100%;}.inicio-cliente__accion{flex:1 1 150px;}.inicio-cliente__rejilla{grid-template-columns:1fr;}.inicio-cliente__tarjeta{min-height:145px;padding:16px;}.inicio-cliente__ayuda{align-items:flex-start;}}
@media(prefers-reduced-motion:reduce){.inicio-cliente__tarjeta,.inicio-cliente__accion{transition:none;}}

</style>
