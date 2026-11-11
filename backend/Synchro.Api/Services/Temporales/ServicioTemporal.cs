using System.Security.Cryptography;

namespace Synchro.Api.Services.Temporales;

internal sealed class ServicioTemporal(IRepositorioServicioTemporal repo,IConfiguration config,TimeProvider reloj)
{
    private DateTimeOffset Ahora=>reloj.GetUtcNow();
    private static ResumenServicioTemporal Vista(OperacionServicioTemporal o)=>new(o.Id,o.Codigo,o.Clase,o.Estado,o.Minutos,o.Monto,"HNL",o.VenceReservaUtc,o.InicioUtc,o.FinContratadoUtc,o.PagoAnulado);
    private void Vencer(DatosServicioTemporal d)
    {
        if(d.Configuracion.Habilitado&&!d.Configuracion.TemperaturaApta)
        {
            d.Configuracion=d.Configuracion with{Habilitado=false};
            if(d.EstadoInventario=="Disponible")d.EstadoInventario="Bloqueado";
            d.Incidencias.Add("Temperatura simulada fuera del rango configurado.");
            d.Eventos.Add(new(null,"Incidencia termica simulada","Sistema",Ahora));
        }
        foreach(var o in d.Operaciones.Values)
        {
            if((o.Estado is EstadoServicioTemporal.Reservada or EstadoServicioTemporal.Pagada)&&o.VenceReservaUtc<=Ahora)
            {
                o.Estado=EstadoServicioTemporal.Cancelada;o.PagoAnulado=o.PagoId is not null;o.PinHash=null;o.PinSalt=null;
                d.EstadoInventario=d.Configuracion.Habilitado&&d.Configuracion.TemperaturaApta?"Disponible":"Bloqueado";
                d.Eventos.Add(new(o.Id,"Reserva vencida","Sistema",Ahora));
            }
            else if(o.Estado==EstadoServicioTemporal.Activa&&o.FinContratadoUtc<=Ahora)
            {o.Estado=EstadoServicioTemporal.Agotada;d.Eventos.Add(new(o.Id,"Tiempo contratado agotado","Sistema",Ahora));}
        }
    }
    private T Aplicar<T>(DatosServicioTemporal d,Func<T> accion){Vencer(d);return accion();}
    private static OperacionServicioTemporal Propietario(DatosServicioTemporal d,Guid id,string credencial,int? terminal=null)
    {
        if(!d.Operaciones.TryGetValue(id,out var o)||!CredencialesServicioTemporal.Iguales(o.TokenHash,CredencialesServicioTemporal.HashToken(credencial))||
            terminal is not null&&o.TerminalId is not null&&o.TerminalId!=terminal)
            throw new ServicioTemporalNoDisponibleException();
        return o;
    }
    private static void Terminal(int id){if(id<=0)throw new ArgumentException("Terminal invalida.");}
    private static bool Termico(ClaseServicioTemporal clase)=>clase is ClaseServicioTemporal.Frio or ClaseServicioTemporal.Calor;
    private static void ExigirReservaWeb(OperacionServicioTemporal o)
    {if(Termico(o.Clase)&&(o.ClientePersonaId is not >0||o.OrigenReserva!="Web"))throw new ServicioTemporalNoDisponibleException(true);}
    private static void GestionTermica(OperacionServicioTemporal o,int? cliente,int? terminal=null)
    {
        if(!Termico(o.Clase))return;
        if(terminal is not null)throw new ReservaSoloWebException();
        if(cliente is not >0)throw new ClienteWebRequeridoException();
        ExigirReservaWeb(o);
        if(o.ClientePersonaId!=cliente)throw new ServicioTemporalNoDisponibleException();
    }
    private string CrearPin(OperacionServicioTemporal o)
    {
        string pin;
        do{pin=CodigoPinRetiro.Generar();}
        while(o.PinHash is not null&&o.PinSalt is not null&&CredencialesServicioTemporal.Iguales(
            CredencialesServicioTemporal.HashPin(pin,o.Id,o.PinSalt,config),o.PinHash));
        byte[] salt=CodigoPinRetiro.GenerarSalt();
        try{o.PinSalt=Convert.ToBase64String(salt);o.PinHash=CredencialesServicioTemporal.HashPin(pin,o.Id,o.PinSalt,config);o.IntentosPin=0;return pin;}
        finally{CryptographicOperations.ZeroMemory(salt);}
    }
    public Task<ReservaServicioTemporal> ReservarAsync(string codigo,int minutos,int? terminal,CancellationToken token,int? clientePersonaId=null)
    {
        if(terminal is not null)Terminal(terminal.Value);
        if(minutos is <1 or >1440)throw new ArgumentException("Duracion invalida.");
        return repo.TransaccionAsync(codigo,null,terminal,d=>Aplicar(d,()=>
        {
            var c=d.Configuracion;c.Validar();
            if(Termico(c.Clase))
            {
                if(terminal is not null)throw new ReservaSoloWebException();
                if(clientePersonaId is not >0)throw new ClienteWebRequeridoException();
            }
            if(!c.Tarifas.TryGetValue(minutos,out decimal precio))throw new ArgumentException("Duracion no disponible.");
            if(!c.Habilitado||!c.TemperaturaApta||d.EstadoInventario!="Disponible"||d.Operaciones.Values.Any(o=>o.Ocupa))
                throw new ServicioTemporalNoDisponibleException(true);
            byte[] secreto=RandomNumberGenerator.GetBytes(32);
            string credencial;
            try{credencial=Convert.ToHexString(secreto);}finally{CryptographicOperations.ZeroMemory(secreto);}
            var o=new OperacionServicioTemporal{Id=Guid.NewGuid(),Codigo=c.Codigo,Clase=c.Clase,TerminalId=terminal,ClientePersonaId=clientePersonaId,OrigenReserva=terminal is null?"Web":"Terminal",
                Minutos=minutos,Monto=precio,TokenHash=CredencialesServicioTemporal.HashToken(credencial),CreadaUtc=Ahora,VenceReservaUtc=Ahora.AddMinutes(10)};
            d.Operaciones.Add(o.Id,o);d.EstadoInventario="Reservado";
            d.Eventos.Add(new(o.Id,"Reserva creada",terminal is null?"Web":"Terminal:"+terminal,Ahora));
            return new ReservaServicioTemporal(Vista(o),credencial);
        }),token,clientePersonaId);
    }
    public Task<PagoServicioTemporal> PagarAsync(string codigo,Guid id,string credencial,Guid pagoId,int? terminal,CancellationToken token,int? clientePersonaId=null,string? metodoPago=null)
    {
        if(pagoId==Guid.Empty)throw new ArgumentException("Identificador de pago invalido.");
        if(terminal is not null)Terminal(terminal.Value);
        return repo.TransaccionAsync(codigo,id,terminal,d=>Aplicar(d,()=>
        {
            var o=Propietario(d,id,credencial,terminal);
            GestionTermica(o,clientePersonaId,terminal);
            // No se reciben billetes ni se procesa dinero real: el canal solo
            // identifica la modalidad seleccionada en la demostracion.
            string canal=terminal is not null
                ? metodoPago switch
                {
                    null or "NFC" => "NfcSimulado",
                    "Tarjeta" => "TarjetaTerminalSimulada",
                    "Efectivo" => "EfectivoSimulado",
                    _ => throw new ArgumentException("Metodo de pago simulado invalido.")
                }
                : metodoPago switch
                {
                    null or "TarjetaWeb" => "WebSimulado",
                    _ => throw new ArgumentException("El pago web no admite esta modalidad.")
                };
            if(o.PagoId==pagoId&&!o.PagoAnulado&&o.CanalPago==canal)return new PagoServicioTemporal(Vista(o),null,true);
            if(o.Estado!=EstadoServicioTemporal.Reservada)throw new ServicioTemporalNoDisponibleException(true);
            string pin=CrearPin(o);o.Estado=EstadoServicioTemporal.Pagada;o.PagoId=pagoId;o.PagoUtc=Ahora;o.CanalPago=canal;
            d.Eventos.Add(new(o.Id,"Pago simulado confirmado",canal,Ahora));
            return new PagoServicioTemporal(Vista(o),pin,false);
        }),token,clientePersonaId);
    }
    public Task<ResumenServicioTemporal> IniciarAsync(string codigo,Guid id,string credencial,int terminal,bool equipoCerrado,CancellationToken token)
    {
        Terminal(terminal);
        return repo.TransaccionAsync(codigo,id,terminal,d=>Aplicar(d,()=>
        {
            var o=Propietario(d,id,credencial,terminal);
            ExigirReservaWeb(o);
            if((o.Estado is EstadoServicioTemporal.Activa or EstadoServicioTemporal.Agotada)&&d.EstadoInventario=="Ocupado")return Vista(o);
            if(o.Estado!=EstadoServicioTemporal.Pagada||!d.Configuracion.Habilitado||!d.Configuracion.TemperaturaApta||
                d.EstadoInventario!="Reservado"||o.Clase==ClaseServicioTemporal.CargaLaptop&&!equipoCerrado)
                throw new ServicioTemporalNoDisponibleException(true);
            o.TerminalId=terminal;o.Estado=EstadoServicioTemporal.Activa;o.InicioUtc=Ahora;o.FinContratadoUtc=Ahora.AddMinutes(o.Minutos);
            d.EstadoInventario="Ocupado";d.Eventos.Add(new(o.Id,"Deposito e inicio simulados","Terminal:"+terminal,Ahora));return Vista(o);
        }),token);
    }
    public Task<RetiroServicioTemporal> RetirarAsync(string codigo,Guid id,string credencial,int terminal,string pin,CancellationToken token)
    {
        Terminal(terminal);
        return repo.TransaccionAsync(codigo,id,terminal,d=>Aplicar(d,()=>
        {
            var o=Propietario(d,id,credencial,terminal);
            if(o.Estado is not(EstadoServicioTemporal.Activa or EstadoServicioTemporal.Agotada)||o.PinHash is null||o.PinSalt is null||d.EstadoInventario!="Ocupado")
                throw new ServicioTemporalNoDisponibleException(true);
            if(o.IntentosPin>=5)return new RetiroServicioTemporal(false,true,Vista(o));
            string hash=CredencialesServicioTemporal.HashPin(pin,o.Id,o.PinSalt,config);
            if(!CredencialesServicioTemporal.Iguales(hash,o.PinHash))
            {
                o.IntentosPin++;d.Eventos.Add(new(o.Id,"PIN rechazado","Terminal:"+terminal,Ahora));
                return new RetiroServicioTemporal(false,o.IntentosPin>=5,Vista(o));
            }
            o.Estado=EstadoServicioTemporal.Finalizada;o.RetiroUtc=Ahora;o.PinHash=null;o.PinSalt=null;
            d.EstadoInventario=d.Configuracion.Habilitado&&d.Configuracion.TemperaturaApta?"Disponible":"Bloqueado";
            d.Eventos.Add(new(o.Id,"Retiro simulado confirmado","Terminal:"+terminal,Ahora));return new RetiroServicioTemporal(true,false,Vista(o));
        }),token);
    }
    public Task<PagoServicioTemporal> RenovarPinAsync(string codigo,Guid id,string credencial,int terminal,CancellationToken token)
    {
        Terminal(terminal);
        return repo.TransaccionAsync(codigo,id,terminal,d=>Aplicar(d,()=>
        {
            var o=Propietario(d,id,credencial,terminal);
            if(o.Estado is not(EstadoServicioTemporal.Pagada or EstadoServicioTemporal.Activa or EstadoServicioTemporal.Agotada))throw new ServicioTemporalNoDisponibleException(true);
            string pin=CrearPin(o);d.Eventos.Add(new(o.Id,"PIN renovado con credencial de gestion","Terminal:"+terminal,Ahora));
            return new PagoServicioTemporal(Vista(o),pin,false);
        }),token);
    }
    public Task<ResumenServicioTemporal> CancelarAsync(string codigo,Guid id,string credencial,CancellationToken token,int? clientePersonaId=null)
        =>repo.TransaccionAsync(codigo,id,null,d=>Aplicar(d,()=>
        {
            var o=Propietario(d,id,credencial);
            GestionTermica(o,clientePersonaId);
            if(o.Estado==EstadoServicioTemporal.Cancelada)return Vista(o);
            if(o.Estado is not(EstadoServicioTemporal.Reservada or EstadoServicioTemporal.Pagada))throw new ServicioTemporalNoDisponibleException(true);
            o.Estado=EstadoServicioTemporal.Cancelada;o.PagoAnulado=o.PagoId is not null;o.PinHash=null;o.PinSalt=null;
            d.EstadoInventario=d.Configuracion.Habilitado&&d.Configuracion.TemperaturaApta?"Disponible":"Bloqueado";
            d.Eventos.Add(new(o.Id,"Reserva cancelada con credencial","Web o terminal",Ahora));return Vista(o);
        }),token,clientePersonaId);
    public Task<ResumenServicioTemporal> ConsultarAsync(string codigo,Guid id,string credencial,CancellationToken token)
        =>repo.TransaccionAsync(codigo,id,null,d=>Aplicar(d,()=>Vista(Propietario(d,id,credencial))),token);
    public async Task<IReadOnlyList<DisponibilidadServicioTemporal>> DisponibilidadAsync(CancellationToken token)
    {
        var resultado=new List<DisponibilidadServicioTemporal>();
        foreach(string codigo in await repo.CodigosAsync(token))
            resultado.Add(await repo.TransaccionAsync(codigo,null,null,d=>Aplicar(d,()=>new DisponibilidadServicioTemporal(d.Configuracion,d.EstadoInventario)),token));
        return resultado;
    }
    public Task RegistrarIncidenciaAsync(string codigo,string descripcion,int administrador,CancellationToken token)
    {
        if(administrador<=0||string.IsNullOrWhiteSpace(descripcion)||descripcion.Length>1000)throw new ArgumentException("Incidencia invalida.");
        return repo.TransaccionAsync(codigo,null,null,d=>
        {
            d.Configuracion=d.Configuracion with{Habilitado=false};
            if(d.EstadoInventario=="Disponible")d.EstadoInventario="Bloqueado";
            d.Incidencias.Add(descripcion);
            d.Eventos.Add(new(null,"Incidencia registrada","Administrador:"+administrador,Ahora));return true;
        },token);
    }
    public Task ConfigurarAsync(ConfiguracionServicioTemporal datos,int administrador,CancellationToken token)
    {datos.Validar();if(administrador<=0)throw new ArgumentException("Administrador invalido.");return repo.ConfigurarAsync(datos,administrador,token);}
    public Task<ReporteServiciosTemporales> ReportarAsync(DateTimeOffset desde,DateTimeOffset hasta,CancellationToken token)
    {
        if(hasta<=desde||hasta>Ahora||hasta-desde>TimeSpan.FromDays(31))throw new ArgumentException("Periodo invalido; maximo 31 dias.");
        return repo.ReportarAsync(desde,hasta,token);
    }
}
