// Guías del flujo vigente. Sin navegación, datos personales ni operaciones.
export const temasAyuda = [
  {
    id:"guardar", titulo:"Guardar pertenencias", resumen:"Resguardo temporal de tus objetos", icono:"/iconos/32_guardar_pertenencias_personales",
    pasos:[
      "Selecciona Guardar y elige el tipo de objeto y el tamaño del casillero.",
      "Selecciona un casillero disponible y completa el registro facial.",
      "Sigue las indicaciones de WhatsApp para autorizar las notificaciones y recibir tu PIN de retiro.",
      "Cuando la terminal confirme el acceso, guarda tus objetos, cierra la puerta y finaliza el depósito."
    ],
    nota:"No necesitas crear una cuenta web para guardar tus pertenencias directamente en la terminal."
  },
  {
    id:"reservas", titulo:"Reservas y encomiendas", resumen:"Continúa una reserva de la web", icono:"/iconos/03_reserva",
    pasos:[
      "Crea y consulta tu reserva desde tu cuenta en la web de SYNCHRO.",
      "En la terminal selecciona Tengo una reserva y elige Pertenencias personales o Encomienda.",
      "Si es una encomienda, selecciona Normal, Frío o Calor según tu reserva. Completa la identificación y el PIN solicitado.",
      "Deposita únicamente cuando la terminal autorice la operación y confirma que cerraste la puerta."
    ],
    nota:"Para las encomiendas, sigue las instrucciones de autorización por WhatsApp y de pago que correspondan a tu reserva. No compartas los PIN."
  },
  {
    id:"carga", titulo:"Cargar dispositivos", resumen:"Celulares y laptops", icono:"/iconos/31_carga_celular_laptop",
    pasos:[
      "Selecciona Cargar dispositivo y elige Celular o Laptop.",
      "Para el celular, elige carga con cable o inalámbrica, según compatibilidad.",
      "Selecciona un espacio disponible, completa la identificación facial y crea y confirma tu PIN de 6 dígitos.",
      "Coloca tu dispositivo siguiendo las indicaciones. Paga al finalizar el servicio, cuando lo retires."
    ],
    nota:"Este servicio es presencial, sin reserva web. Recuerda tu PIN y verifica que el dispositivo esté cargando correctamente."
  },
  {
    id:"retirar", titulo:"Retirar objetos", resumen:"Pertenencias, dispositivos o encomiendas", icono:"/iconos/02_retirar",
    pasos:[
      "Selecciona Retirar y elige Mis pertenencias, Mi dispositivo o Una encomienda.",
      "Completa la verificación facial y el PIN correspondiente: el recibido por WhatsApp o el que creaste para la carga.",
      "Si hay un importe pendiente, revisa el cobro y sigue las indicaciones de pago. Si la encomienda ya está pagada, no debes pagar nuevamente.",
      "Espera la autorización, recoge tus objetos, cierra la puerta y confirma el retiro."
    ],
    nota:"Si la identificación no coincide, no intentes abrir el casillero por tu cuenta. Solicita ayuda al operador."
  },
  {
    id:"pagos", titulo:"Pagos y tarifas", resumen:"Consulta tu importe y métodos", icono:"/iconos/complementarios/22_pagos",
    pasos:[
      "Consulta las condiciones del servicio y la tarifa correspondiente antes de usarlo.",
      "El resguardo personal y la carga se cobran al retirar. En las encomiendas, el pago depende de la opción elegida en la reserva.",
      "Si corresponde pagar en la terminal, selecciona uno de los métodos disponibles: efectivo, tarjeta o NFC.",
      "Espera la confirmación del pago y sigue las instrucciones para terminar la operación."
    ],
    nota:"Si ya efectuaste el pago y no aparece confirmado, solicita asistencia antes de volver a pagar."
  },
  {
    id:"asistencia", titulo:"Seguridad y asistencia", resumen:"PIN, rostro, casillero o conexión", icono:"/iconos/complementarios/23_ayuda",
    pasos:[
      "Si no recibes el PIN, comprueba el número de WhatsApp y la autorización de mensajes. Solicita ayuda si continúa el problema.",
      "Si falla la identificación facial, sitúate frente a la cámara, con buena iluminación, y sigue las indicaciones.",
      "Si la puerta no abre o no cierra, no la fuerces. Si hay un error de conexión, evita repetir pagos o depósitos.",
      "Contacta al personal de SYNCHRO presente en la estación e indica el código del casillero y el mensaje mostrado."
    ],
    nota:"Nunca compartas tu contraseña o PIN con otras personas. No introduzcas objetos peligrosos ni dispositivos dañados en los espacios de carga."
  }
];
