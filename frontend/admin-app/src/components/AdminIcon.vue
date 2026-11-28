<script setup>
import { computed } from "vue";
const props = defineProps({
  name: { type: String, required: true },
  size: { type: Number, default: 20 },
});
const paths = {
  dashboard: "M3 3h8v8H3z M13 3h8v5h-8z M13 10h8v11h-8z M3 13h8v8H3z",
  message: "M20 11.5a7.5 7.5 0 0 1-7.5 7.5 8 8 0 0 1-3.5-.8L4 20l1.7-4.7A8.5 8.5 0 1 1 20 11.5Z",
  lockers: "M3 3h18v18H3z M3 9h18 M12 3v18 M7 6h.01 M16 6h.01 M7 15h.01 M16 15h.01",
  calendar: "M8 3v4 M16 3v4 M3 10h18 M5 5h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2Z",
  activity: "M3 12h4l3-7 4 14 3-7h4",
  report: "M4 19h16 M7 16V9 M12 16V5 M17 16v-4",
  settings: "M12 15.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7Z M19.4 15a1.7 1.7 0 0 0 .3 1.9l-1.8 1.8a1.7 1.7 0 0 0-1.9-.3l-1 .4-.5 1.9h-2.6l-.5-1.9-1-.4a1.7 1.7 0 0 0-1.9.3l-1.8-1.8A1.7 1.7 0 0 0 7 15l-.4-1-1.9-.5v-2.6L6.6 10l.4-1a1.7 1.7 0 0 0-.3-1.9l1.8-1.8A1.7 1.7 0 0 0 10.4 5l1-.4.5-1.9h2.6l.5 1.9 1 .4a1.7 1.7 0 0 0 1.9-.3l1.8 1.8A1.7 1.7 0 0 0 19.4 9l.4 1 1.9.5v2.6l-1.9.5Z",
  logout: "M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4 M16 17l5-5-5-5 M21 12H9",
  menu: "M4 7h16 M4 12h16 M4 17h16",
  close: "M5 5l14 14 M19 5 5 19",
  refresh: "M3 11a9 9 0 0 1 15-6l3 3 M21 3v5h-5 M21 13a9 9 0 0 1-15 6l-3-3 M3 21v-5h5",
  arrow: "M5 12h14 M13 6l6 6-6 6",
  chevron: "m9 18 6-6-6-6",
  shield: "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z M9 12l2 2 4-4",
  key: "M8.5 14a5.5 5.5 0 1 1 4.6-2.5L11 14H8.5v2H6v2H3v-3.5L8.5 14Z",
  clock: "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20Z M12 6v6l4 2",
  search: "M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16Z M17 17l4 4",
  info: "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20Z M12 11v6 M12 7h.01",
  check: "M5 12l4 4L19 6",
  lock: "M6 11h12a2 2 0 0 1 2 2v7H4v-7a2 2 0 0 1 2-2Z M8 11V7a4 4 0 0 1 8 0v4 M12 15v2",
  eye: "M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z M12 9a3 3 0 1 0 0 6 3 3 0 1 0 0-6Z",
  "eye-off": "M3 3l18 18 M10.6 5.1A10.8 10.8 0 0 1 12 5c6.5 0 10 7 10 7a18 18 0 0 1-3.3 4.2 M6.2 6.2C3.4 8.4 2 12 2 12s3.5 7 10 7a10 10 0 0 0 4.1-.9 M9.9 9.9a3 3 0 0 0 4.2 4.2",
  sun: "M12 8a4 4 0 1 0 0 8 4 4 0 1 0 0-8 M12 2v2 M12 20v2 M2 12h2 M20 12h2 M4.9 4.9l1.4 1.4 M17.7 17.7l1.4 1.4 M4.9 19.1l1.4-1.4 M17.7 6.3l1.4-1.4",
  moon: "M20.9 13.35A9 9 0 1 1 10.65 3.1a7 7 0 0 0 10.25 10.25Z",
  user: "M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2 M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z",
};
const d = computed(() => paths[props.name] || paths.info);
</script>
<template>
  <svg :width="size" :height="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" focusable="false" aria-hidden="true"><path :d="d" /></svg>
</template>
