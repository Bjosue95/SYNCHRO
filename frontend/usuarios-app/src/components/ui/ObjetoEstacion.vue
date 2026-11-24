<script setup lang="ts">
// Objetos exclusivos de la ilustración de SYNCHRO; no son controles ni estados reales.
defineProps<{ tipo: 'caja' | 'mochila' | 'bolso' | 'maleta' | 'frio' | 'caliente' | 'laptop' | 'celular' }>();
</script>

<template>
  <svg class="syn-station-object" :data-objeto="tipo" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"
    aria-hidden="true" focusable="false" fill="none" stroke="#01305C" stroke-width="1.5"
    stroke-linecap="round" stroke-linejoin="round">
    <ellipse cx="32" cy="57" rx="21" ry="2" fill="#AEC6D2" stroke="none" />

    <g v-if="tipo === 'caja'">
      <path d="m10 22 22-10 22 10-22 11z" fill="#F7FBFC" />
      <path d="M10 22v25l22 11V33z" fill="#D1E4ED" />
      <path d="M32 33v25l22-11V22z" fill="#EBF3F7" />
      <path d="m23 16 22 11v9l-8 4v-9L16 19" fill="#A5DDE3" />
      <path d="m39 43 9-4v6l-9 4z" fill="#FFF" />
      <path d="m15 42 8 4m-8-1 5 3" stroke="#5B6770" stroke-width="1" />
    </g>
    <g v-else-if="tipo === 'mochila'">
      <path d="M25 18v-4a7 7 0 0 1 14 0v4" />
      <path d="M16 29h-4v17h5m31-17h4v17h-5" fill="#AAC7D6" />
      <rect x="16" y="17" width="32" height="39" rx="9" fill="#CEE2EC" />
      <path d="M19 28h26" stroke="#7095A7" />
      <rect x="22" y="34" width="20" height="16" rx="4" fill="#F7FBFC" />
      <path d="M25 39h14m-3 0v3" stroke="#008995" />
      <path d="M23 21h18" stroke="#FFF" stroke-width="2" />
    </g>
    <g v-else-if="tipo === 'bolso'">
      <path d="M23 24v-8a9 9 0 0 1 18 0v8" stroke="#5B6770" />
      <path d="M13 24h38l3 26a5 5 0 0 1-5 5H15a5 5 0 0 1-5-5z" fill="#CEE2EC" />
      <path d="M14 29h36" stroke="#FFF" stroke-width="2" />
      <path d="M21 23v17m22-17v17" stroke="#7095A7" />
      <rect x="25" y="38" width="14" height="10" rx="2" fill="#F7FBFC" />
      <path d="M29 42h6" stroke="#008995" />
    </g>
    <g v-else-if="tipo === 'maleta'">
      <path d="M26 20V8h12v12" stroke="#5B6770" />
      <path d="M26 9h12" stroke-width="3" />
      <rect x="15" y="19" width="34" height="35" rx="5" fill="#D4E5EE" />
      <path d="M22 26v21m10-21v21m10-21v21" stroke="#7197AA" />
      <path d="M20 22h24" stroke="#FFF" stroke-width="2" />
      <rect x="42" y="30" width="7" height="10" rx="1" fill="#A5DDE3" />
      <circle cx="22" cy="56" r="2" fill="#5B6770" />
      <circle cx="42" cy="56" r="2" fill="#5B6770" />
    </g>
    <g v-else-if="tipo === 'frio'">
      <rect x="25" y="8" width="14" height="7" rx="2" fill="#AACDDC" />
      <path d="M26 15v5c0 4-6 6-6 11v20a4 4 0 0 0 4 4h16a4 4 0 0 0 4-4V31c0-5-6-7-6-11v-5" fill="#E4F3FA" />
      <path d="M21 31h22v17H21z" fill="#B8E0EE" stroke="none" />
      <path d="M32 34v11m-5-8 10 5m-10 0 10-5m-7-1 2-2 2 2m-6 7 4 2 4-2" stroke="#3A829F" stroke-width="1.2" />
      <path d="M24 26v3" stroke="#FFF" stroke-width="2" />
    </g>
    <g v-else-if="tipo === 'caliente'">
      <path d="M14 31h36l-3 19a5 5 0 0 1-5 4H22a5 5 0 0 1-5-4z" fill="#F3E2D3" />
      <rect x="11" y="26" width="42" height="6" rx="3" fill="#FCF6F0" />
      <path d="M23 26v-3a3 3 0 0 1 3-3h12a3 3 0 0 1 3 3v3" fill="#E4C3A8" />
      <path d="M22 16c-5-5 4-6 0-11m10 11c-5-5 4-6 0-11m10 11c-5-5 4-6 0-11" stroke="#A77959" stroke-width="1.3" />
      <path d="M23 39h18m-14 5h10" stroke="#B18C70" />
    </g>
    <g v-else-if="tipo === 'laptop'">
      <rect x="12" y="13" width="40" height="30" rx="3" fill="#C2D9E6" />
      <rect x="16" y="17" width="32" height="22" rx="1" fill="#01305C" />
      <path d="M22 24h13m-13 5h20m-20 5h8" stroke="#A4DAE4" stroke-width="1.4" />
      <path d="m12 43-6 8a3 3 0 0 0 3 3h46a3 3 0 0 0 3-3l-6-8z" fill="#E8F1F6" />
      <path d="M8 50h48m-31-6-2 4h18l-2-4" stroke="#7798AA" stroke-width="1" />
    </g>
    <g v-else-if="tipo === 'celular'">
      <rect x="20" y="6" width="24" height="50" rx="5" fill="#C5DCE8" />
      <rect x="23" y="13" width="18" height="32" rx="1.5" fill="#01305C" />
      <path d="M29 10h6" stroke="#5B6770" />
      <path d="M27 21h10m-10 5h7" stroke="#B3DDE7" stroke-width="1.2" />
      <path d="M27 38h10" stroke="#5EC6E8" stroke-width="2" />
      <circle cx="32" cy="50" r="1.5" fill="#5B6770" stroke="none" />
    </g>
  </svg>
</template>
