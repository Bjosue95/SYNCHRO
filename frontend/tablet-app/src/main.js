import "./styles/reset.css";
import "./styles/transitions.css";
import "./styles/tablet.css";

import { createApp } from "vue";
import App from "./App.vue";
import router from "./router";

const app = createApp(App);

app.use(router);

app.mount("#app");
