# SYNCHRO · Landing de casilleros electrónicos

La interfaz está escrita en HTML, CSS y JavaScript sin frameworks ni dependencias externas. Incluye cinco imágenes conceptuales y las variantes oficiales de la marca del repositorio de SYNCHRO.

## Vista previa

Abre `index.html` en el navegador. El catálogo, la navegación y el cambio de tema funcionan al abrir el archivo. Para probar el envío de solicitudes inicia el servidor.

## Formulario funcional

Con Node.js 22.13 o posterior, abre una terminal en esta carpeta y ejecuta:

```bash
node server.mjs
```

Abre http://localhost:8080. Los registros se guardan en `datos/solicitudes.sqlite`. Para consultarlos desde el equipo autorizado que aloja el servidor:

```bash
node server.mjs --listar
```

Si alojas la página en Internet, configura `HOST=0.0.0.0` cuando el proveedor lo requiera, ajusta `PORT` al puerto del servicio y establece `PUBLIC_ORIGIN` con la URL HTTPS final. Conserva la carpeta `datos/` en almacenamiento persistente de acceso restringido y configura límites de solicitudes en el proxy. El servidor solo publica los archivos de la interfaz. Los registros quedan en la base local al servidor. Los avisos por correo requieren un servicio de correo y un destinatario comercial.

En un alojamiento exclusivamente estático, conecta el formulario a un servicio externo antes de aceptar solicitudes.
