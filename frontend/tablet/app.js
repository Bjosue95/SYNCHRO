"use strict";

/* =========================================
   SYNCHRO | RELOJ Y FECHA
   ========================================= */

function actualizarReloj() {
  const ahora = new Date();

  const horaTexto = new Intl.DateTimeFormat("es-HN", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(ahora);

  const fechaTexto = new Intl.DateTimeFormat("es-HN", {
    weekday: "long",
    day: "numeric",
    month: "long",
  }).format(ahora);

  const fechaFormateada =
    fechaTexto.charAt(0).toUpperCase() + fechaTexto.slice(1);

  document.querySelectorAll("#hora, #hora-menu").forEach((elemento) => {
    elemento.textContent = horaTexto;
  });

  document.querySelectorAll("#fecha, #fecha-menu").forEach((elemento) => {
    elemento.textContent = fechaFormateada;
  });
}

actualizarReloj();
setInterval(actualizarReloj, 30000);

/* =========================================
   SYNCHRO | PANTALLA PRINCIPAL
   ========================================= */

const app = document.getElementById("app");

document.getElementById("comenzar").addEventListener("click", mostrarMenu);

function mostrarMenu() {
  // 1. Crear el contenido del menú

  app.innerHTML = `
        <section class="pantalla-menu"
                 aria-labelledby="titulo-menu">

            <header class="encabezado-menu">

                <div class="marca-menu">
                    <img
                        src="assets/isotipo-fondo-claro.png"
                        alt=""
                        class="isotipo-menu"
                    >

                    <img
                        src="assets/nombre-azul.png"
                        alt="SYNCHRO"
                        class="logo-menu"
                    >
                </div>

                <div class="controles-menu">

                    <button
                        type="button"
                        id="abrir-ayuda"
                        class="boton-ayuda"
                    >
                        ? Ayuda
                    </button>

                    <button
                        type="button"
                        id="cambiar-tema"
                        class="boton-tema"
                        aria-label="Activar modo oscuro"
                        title="Cambiar tema"
                    >
                        ☾
                    </button>

                    <div class="datos-menu">
                        <time id="hora-menu">--:--</time>
                        <span id="fecha-menu"></span>
                    </div>

                </div>

            </header>

            <h1 id="titulo-menu">
                ¿Qué deseas hacer?
            </h1>

            <div class="servicios">

                <button
                    type="button"
                    id="servicio-guardar"
                    class="tarjeta-servicio"
                >
                    <img
                        src="iconos/01_guardar_depositar.png"
                        alt=""
                        class="icono-servicio"
                    >

                    <span class="titulo-servicio">Guardar</span>

                    <span class="descripcion-servicio">
                        Encomiendas y pertenencias
                    </span>
                </button>

                <div class="tarjeta-servicio">
                    <img
                        src="iconos/02_retirar.png"
                        alt=""
                        class="icono-servicio"
                    >
                    <h2>Retirar</h2>
                    <p>Recoge tus objetos</p>
                </div>

                <div class="tarjeta-servicio">
                    <img
                        src="iconos/03_reserva.png"
                        alt=""
                        class="icono-servicio"
                    >
                    <h2>Tengo una reserva</h2>
                    <p>Continúa tu reserva web</p>
                </div>

                <div class="tarjeta-servicio">
                    <img
                        src="iconos/04_carga_inalambrica.png"
                        alt=""
                        class="icono-servicio"
                    >
                    <h2>Cargar dispositivo</h2>
                    <p>Celular o laptop</p>
                </div>

            </div>

            <!-- VENTANA DE AYUDA -->

            <dialog
                id="dialogo-ayuda"
                class="dialogo-ayuda"
                aria-labelledby="titulo-ayuda"
            >
                <h2 id="titulo-ayuda">Centro de ayuda</h2>

                <p class="descripcion-ayuda">
                    ¿En qué podemos ayudarte? Selecciona una pregunta.
                </p>

                <div class="preguntas-ayuda">

                    <details class="pregunta-ayuda">
                        <summary>
                            ¿Cómo guardo mis pertenencias?
                        </summary>
                        <p>
                            Selecciona Guardar y después Mis pertenencias.
                            Sigue las instrucciones para depositar tu
                            mochila, compras u otros objetos personales.
                        </p>
                    </details>

                    <details class="pregunta-ayuda">
                        <summary>
                            ¿Cómo envío una encomienda?
                        </summary>
                        <p>
                            Selecciona Guardar y después Enviar encomienda.
                            El destinatario deberá confirmar su aceptación
                            por WhatsApp antes de habilitar el depósito.
                        </p>
                    </details>

                    <details class="pregunta-ayuda">
                        <summary>
                            ¿Cómo retiro una encomienda?
                        </summary>
                        <p>
                            Selecciona Retirar. Para una encomienda enviada
                            por otra persona, SYNCHRO solicitará verificación
                            facial y el PIN recibido por WhatsApp.
                        </p>
                    </details>

                    <details class="pregunta-ayuda">
                        <summary>
                            ¿Qué hago si tengo una reserva?
                        </summary>
                        <p>
                            Selecciona Tengo una reserva y sigue los pasos
                            para identificar la reserva que realizaste
                            previamente desde la página web.
                        </p>
                    </details>

                    <details class="pregunta-ayuda">
                        <summary>
                            ¿Cómo cargo mi dispositivo?
                        </summary>
                        <p>
                            Selecciona Cargar dispositivo y elige el tipo
                            de carga disponible para tu celular o laptop.
                            Sigue las indicaciones de la terminal.
                        </p>
                    </details>

                </div>

                <button type="button" id="cerrar-ayuda">
                    Cerrar ayuda
                </button>

            </dialog>

        </section>
    `;

  // 2. Actualizar el reloj del menú

  actualizarReloj();

  /* =====================================
       3. MODO CLARO Y OSCURO
       ===================================== */

  const pantallaMenu = document.querySelector(".pantalla-menu");

  const botonTema = document.getElementById("cambiar-tema");

  botonTema.addEventListener("click", () => {
    const oscuro = pantallaMenu.classList.toggle("tema-oscuro");

    botonTema.textContent = oscuro ? "☀" : "☾";

    botonTema.setAttribute(
      "aria-label",
      oscuro ? "Activar modo claro" : "Activar modo oscuro",
    );

    document.querySelector(".logo-menu").src = oscuro
      ? "assets/nombre-blanco.png"
      : "assets/nombre-azul.png";

    document.querySelector(".isotipo-menu").src = oscuro
      ? "assets/isotipo-fondo-oscuro.png"
      : "assets/isotipo-fondo-claro.png";

    // Cambiar también los iconos de servicios

    document.querySelectorAll(".icono-servicio").forEach((icono) => {
      const archivo = icono
        .getAttribute("src")
        .split("/")
        .pop()
        .replace("-oscuro.svg", "")
        .replace(/\.(png|svg)$/, "");

      icono.src = oscuro
        ? `iconos/${archivo}-oscuro.svg`
        : `iconos/${archivo}.svg`;
    });
  });

  /* =====================================
       4. VENTANA DE AYUDA
       ===================================== */

  const botonAbrirAyuda = document.getElementById("abrir-ayuda");

  const botonCerrarAyuda = document.getElementById("cerrar-ayuda");

  const dialogoAyuda = document.getElementById("dialogo-ayuda");

  botonAbrirAyuda.addEventListener("click", () => {
    dialogoAyuda.showModal();
  });

  botonCerrarAyuda.addEventListener("click", () => {
    dialogoAyuda.close();
  });

  document
    .getElementById("servicio-guardar")
    .addEventListener("click", mostrarOpcionesGuardar);
}

/* =========================================
   SYNCHRO | OPCIONES PARA GUARDAR
   ========================================= */

function mostrarOpcionesGuardar() {
  const menu = document.querySelector(".pantalla-menu");
  const titulo = document.getElementById("titulo-menu");
  const servicios = document.querySelector(".servicios");

  titulo.hidden = true;
  servicios.hidden = true;

  const pantalla = document.createElement("section");
  pantalla.className = "opciones-guardar";

  const oscuro = menu.classList.contains("tema-oscuro");
  const variante = oscuro ? "-oscuro" : "";

  pantalla.innerHTML = `
        <button type="button" class="boton-regresar"
                id="regresar-menu">
            ← Volver al menú
        </button>

        <h1>¿Qué deseas guardar?</h1>

        <div class="opciones-grid">

            <div class="tarjeta-servicio">
                <img
                    src="iconos/10_mochila_bolso${variante}.svg"
                    alt=""
                    class="icono-servicio"
                >
                <h2>Mis pertenencias</h2>
                <p>Mochilas, compras y objetos personales</p>
            </div>

            <div class="tarjeta-servicio">
                <img
                    src="iconos/09_encomienda_paquete${variante}.svg"
                    alt=""
                    class="icono-servicio"
                >
                <h2>Enviar encomienda</h2>
                <p>Depositar un paquete para otra persona</p>
            </div>

        </div>
    `;

  menu.insertBefore(pantalla, document.getElementById("dialogo-ayuda"));

  document.getElementById("regresar-menu").addEventListener("click", () => {
    pantalla.remove();
    titulo.hidden = false;
    servicios.hidden = false;
  });
}
