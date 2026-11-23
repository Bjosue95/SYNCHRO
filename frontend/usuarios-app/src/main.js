import { createApp } from "vue";
import App from "./App.vue";
import { router } from "./router/index.js";
import "./styles/base.css";
import "./styles/access.css";
import "./styles/theme-transition.css";

createApp(App).use(router).mount("#app");
