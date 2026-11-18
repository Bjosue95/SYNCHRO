const form = document.querySelector("#solicitud");
const type = document.querySelector("#tipo");
const company = document.querySelector("#empresa-campo");
const companyInput = form.elements.empresa;
const status = document.querySelector("#estado");
const button = form.querySelector("button");

function updateCompany() {
  const isCompany = type.value === "empresa";
  company.hidden = !isCompany;
  companyInput.disabled = !isCompany;
  companyInput.required = isCompany;
}

type.addEventListener("change", updateCompany);
updateCompany();

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (button.disabled) return;
  button.disabled = true;
  status.classList.remove("error");
  status.textContent = "Enviando solicitud…";

  try {
    const response = await fetch(form.action, {
      method: "POST",
      body: new URLSearchParams(new FormData(form)),
      headers: { Accept: "application/json" },
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.error);
    form.reset();
    updateCompany();
    status.textContent = "Recibimos tu solicitud. El equipo de SYNCHRO podrá contactarte por correo.";
  } catch (error) {
    status.classList.add("error");
    status.textContent = error instanceof Error && error.message.startsWith("Revisa")
      ? error.message
      : "No pudimos enviar tu solicitud. Tus datos siguen en el formulario. Intenta de nuevo.";
  } finally {
    button.disabled = false;
    status.focus();
  }
});

const themeButton = document.querySelector("#tema");
const prefersDark = matchMedia("(prefers-color-scheme: dark)");
const themePreference = "synchro-theme";
try {
  const stored = localStorage.getItem(themePreference);
  if (stored === "dark" || stored === "light") document.documentElement.dataset.theme = stored;
} catch {
  // System colors are still available when browser storage is restricted.
}
function syncThemeButton() {
  const dark = (document.documentElement.dataset.theme || (prefersDark.matches ? "dark" : "light")) === "dark";
  const label = dark ? "Activar modo claro" : "Activar modo oscuro";
  themeButton.setAttribute("aria-label", label);
  themeButton.title = label;
  themeButton.firstElementChild.textContent = dark ? "☀" : "☾";
}
themeButton.addEventListener("click", () => {
  const current = document.documentElement.dataset.theme || (prefersDark.matches ? "dark" : "light");
  const next = current === "dark" ? "light" : "dark";
  document.documentElement.dataset.theme = next;
  try { localStorage.setItem(themePreference, next); } catch {}
  syncThemeButton();
});
prefersDark.addEventListener("change", syncThemeButton);
syncThemeButton();

for (const link of document.querySelectorAll("[data-modulo]")) {
  link.addEventListener("click", () => {
    form.elements.modulo.value = link.dataset.modulo;
  });
}

if (location.protocol === "file:") {
  status.textContent = "Vista local. Inicia el servidor incluido para enviar solicitudes.";
  button.disabled = true;
}
