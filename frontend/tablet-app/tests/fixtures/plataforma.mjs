// Respuestas controladas SOLO para pruebas de UI. La aplicacion no importa estas fixtures.
export async function prepararPlataforma(page, { cliente = true, admin = true, rol = 'Administrador', fallo = false } = {}) {
  const peticiones = [];
  let operacion = null, estadoWhatsApp = 'Pendiente';
  const id = '10000000-0000-4000-8000-000000000001';
  const intentoId = '20000000-0000-4000-8000-000000000001';
  const espacios = ['Personal','CargaCelular','Frio','Calor'].map((clase,i) => ({ configuracion: { codigo:['P10','C10','F10','H10'][i], clase, conector:clase === 'CargaCelular' ? 'USB-C' : null, tamano:'Mediano', habilitado:true, tarifas:{30:15,60:25,90:35}, temperaturaSimulada:clase === 'Frio' ? 5 : clase === 'Calor' ? 50 : null }, estado:'Disponible',hardwareSimulado:true }));
  const perfil = { personaId:41, nombreUsuario:'cliente_prueba',telefono:'+50400000001' };
  const sesion = {usuario:'operador_prueba',rol,roles:[rol]};
  await page.route('**/api/**',async route => {
    const request=route.request(), url=new URL(request.url()), path=url.pathname;
    peticiones.push({ path, method:request.method(), body:request.postDataJSON?.bind(request) });
    const respuesta=(body,status=200) => route.fulfill({status,contentType:'application/json',body:JSON.stringify(body)});
    if(fallo)return respuesta({mensaje:'Servicio de prueba no disponible'},503);
    if(path.endsWith('/csrf-token'))return respuesta({csrfToken:'csrf-exclusivo-pruebas'});
    if(path === '/api/plataforma/contexto')return respuesta({nombreUbicacion:'Estación de prueba de UI',direccion:'Datos ficticios de prueba',prototipo:true});
    if(path === '/api/web/clientes/mi-sesion')return respuesta(perfil,cliente?200:401);
    if(/\/acceso\/(registro|login)$/.test(path)){cliente=true;return respuesta(perfil);}
    if(path === '/api/web/clientes/logout'){cliente=false;return respuesta({cerrada:true});}
    if(path === '/api/admin/mi-sesion')return respuesta(sesion,admin?200:401);
    if(path === '/api/admin/login'){admin=true;return respuesta(sesion);}
    if(path === '/api/admin/logout'){admin=false;return respuesta({cerrada:true});}
    if(path === '/api/servicios/compartimentos')return respuesta(espacios);
    if(path === '/api/casilleros-bd')return respuesta(espacios.map(x=>({codigo:x.configuracion.codigo,tamano:'Mediano',tipo:'Normal',estado:x.estado})));
    if(path === '/api/web/clientes/operaciones/reservas' || path === '/api/admin/panel/reservas')return respuesta(operacion?[operacion]:[]);
    if(path.startsWith('/api/web/servicios/')){
      if(path.endsWith('/cancelar-propia')) {operacion.estado='Cancelada';return respuesta(operacion);}
      if(path.endsWith('/pagar')) {operacion.estado='Pagada';return respuesta({operacion,pinRetiro:'123456',yaConfirmado:false});}
      const codigo=path.split('/')[4];
      operacion={id,codigo,servicio:espacios.find(x=>x.configuracion.codigo===codigo).configuracion.clase,estado:'Reservada',minutos:request.postDataJSON().minutos,monto:25,moneda:'HNL',fecha:new Date().toISOString(),venceUtc:new Date(Date.now()+600000).toISOString(),venceReservaUtc:new Date(Date.now()+600000).toISOString()};
      return respuesta({operacion,credencial:'A'.repeat(64)});
    }
    if(path === '/api/admin/whatsapp-manual/pendientes')return respuesta({operaciones:[{intentoId,codigoAutorizacion:'AABBCCDDEEFF1122',codigoCasillero:'M03',estado:estadoWhatsApp,creadoUtc:new Date().toISOString(),venceUtc:new Date(Date.now()+600000).toISOString(),telefonoCliente:'50400000001',usoId:17}]});
    if(path === '/api/admin/whatsapp-manual/confirmar'){estadoWhatsApp='PinEmitido';return respuesta({usoId:17,codigoCasillero:'M03',pinRetiro:'123456',telefonoCliente:'50400000001'});}
    if(path === '/api/admin/whatsapp-manual/marcar-enviado'){estadoWhatsApp='Enviado';return respuesta({envioDeclarado:true});}
    if(path === '/api/admin/usuarios/')return respuesta([{id:7,nombreUsuario:'operador_prueba',activo:true,roles:[rol]}]);
    if(path.startsWith('/api/admin/panel/') || path.includes('/operaciones/encomiendas'))return respuesta([]);
    return respuesta({mensaje:'Ruta sin fixture de prueba'},404);
  });
  return peticiones;
}
