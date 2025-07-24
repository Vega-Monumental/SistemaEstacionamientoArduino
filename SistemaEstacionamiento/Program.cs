using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Timers;
using SistemaEstacionamiento.Modelo;
using AccesoDatos;

namespace SistemaEstacionamiento
{
    /// <summary>
    /// Clase principal para comunicación con Arduino usando protocolo JSON
    /// Sistema de Control de Acceso Vehicular v2.0
    /// </summary>
    public class ControlAccesoVehicularJSON
    {
        #region Campos Privados

        private SerialPort arduinoPort;
        private bool sistemaActivo = false;
        private Thread hiloLectura;
        private CancellationTokenSource cancelacionToken;
        private System.Timers.Timer timerStatus;
        private Queue<string> colaMensajes = new Queue<string>();

        // Configuración del puerto
        private string nombrePuerto = "COM7";
        private int baudRate = 9600;

        // Estado del sistema
        private string estadoActual = "DESCONOCIDO";
        private bool vehiculoPresente = false;
        private DateTime ultimaSolicitud = DateTime.MinValue;

        #endregion

        #region Eventos

        /// <summary>
        /// Evento disparado cuando se recibe una solicitud de acceso
        /// </summary>
        public event EventHandler<EventoSolicitudAcceso> SolicitudAccesoRecibida;

        /// <summary>
        /// Evento disparado cuando un vehículo entra o sale
        /// </summary>
        public event EventHandler<EventoVehiculo> VehiculoDetectado;

        /// <summary>
        /// Evento disparado cuando cambia el estado del sistema
        /// </summary>
        public event EventHandler<EventoCambioEstado> EstadoCambiado;

        /// <summary>
        /// Evento para mensajes de log/debug
        /// </summary>
        public event EventHandler<string> MensajeLog;

        /// <summary>
        /// Evento para errores
        /// </summary>
        public event EventHandler<string> ErrorOcurrido;

        #endregion

        #region Constructor y Destructor

        /// <summary>
        /// Constructor
        /// </summary>
        public ControlAccesoVehicularJSON(string puerto = "COM7")
        {
            nombrePuerto = puerto;
            cancelacionToken = new CancellationTokenSource();

            // Timer para solicitar estado periódicamente
            timerStatus = new System.Timers.Timer(10000); // Cada 10 segundos
            timerStatus.Elapsed += TimerStatus_Elapsed;
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~ControlAccesoVehicularJSON()
        {
            DetenerSistema();
        }

        #endregion

        #region Métodos Públicos

        /// <summary>
        /// Iniciar conexión con Arduino
        /// </summary>
        public async Task<bool> IniciarSistemaAsync()
        {
            try
            {
                OnMensajeLog("Iniciando sistema de control de acceso...");

                // Configurar puerto serial
                arduinoPort = new SerialPort(nombrePuerto, baudRate)
                {
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    NewLine = "\n"
                };

                // Abrir puerto
                arduinoPort.Open();
                sistemaActivo = true;

                // Limpiar buffer
                arduinoPort.DiscardInBuffer();
                arduinoPort.DiscardOutBuffer();

                // Iniciar hilo de lectura
                hiloLectura = new Thread(LeerDatosArduino)
                {
                    IsBackground = true,
                    Name = "Arduino JSON Reader"
                };
                hiloLectura.Start();

                // Esperar mensaje de inicio
                await Task.Delay(2000);

                // Solicitar estado inicial
                await EnviarComandoAsync("STATUS");

                // Iniciar timer de status
                timerStatus.Start();

                OnMensajeLog($"Sistema iniciado correctamente en puerto {nombrePuerto}");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOcurrido($"Error al iniciar sistema: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Detener sistema
        /// </summary>
        public void DetenerSistema()
        {
            try
            {
                OnMensajeLog("Deteniendo sistema...");

                sistemaActivo = false;
                cancelacionToken?.Cancel();
                timerStatus?.Stop();

                if (hiloLectura != null && hiloLectura.IsAlive)
                {
                    hiloLectura.Join(2000);
                }

                if (arduinoPort != null && arduinoPort.IsOpen)
                {
                    arduinoPort.Close();
                    arduinoPort.Dispose();
                }

                OnMensajeLog("Sistema detenido correctamente");
            }
            catch (Exception ex)
            {
                OnErrorOcurrido($"Error al detener sistema: {ex.Message}");
            }
        }

        /// <summary>
        /// Enviar comando al Arduino
        /// </summary>
        public async Task<bool> EnviarComandoAsync(string comando)
        {
            try
            {
                if (arduinoPort != null && arduinoPort.IsOpen)
                {
                    await Task.Run(() => arduinoPort.WriteLine(comando.ToUpper()));
                    OnMensajeLog($"→ Comando enviado: {comando}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                OnErrorOcurrido($"Error al enviar comando: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Solicitar diagnóstico del sensor VEK
        /// </summary>
        public async Task<DiagnosticoVEK> SolicitarDiagnosticoAsync()
        {
            await EnviarComandoAsync("VEK_DIAG");

            // Esperar respuesta (máximo 2 segundos)
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(100);

                lock (colaMensajes)
                {
                    while (colaMensajes.Count > 0)
                    {
                        var mensaje = colaMensajes.Dequeue();
                        var diagnostico = ParsearDiagnostico(mensaje);
                        if (diagnostico != null)
                            return diagnostico;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Obtener lista de puertos disponibles
        /// </summary>
        public static string[] ObtenerPuertosDisponibles()
        {
            return SerialPort.GetPortNames();
        }

        #endregion

        #region Métodos Privados

        /// <summary>
        /// Hilo principal de lectura de datos
        /// </summary>
        private void LeerDatosArduino()
        {
            string bufferParcial = "";

            while (sistemaActivo && !cancelacionToken.Token.IsCancellationRequested)
            {
                try
                {
                    if (arduinoPort.IsOpen && arduinoPort.BytesToRead > 0)
                    {
                        string datos = arduinoPort.ReadExisting();
                        bufferParcial += datos;

                        // Procesar líneas completas
                        string[] lineas = bufferParcial.Split('\n');

                        for (int i = 0; i < lineas.Length - 1; i++)
                        {
                            string linea = lineas[i].Trim();
                            if (!string.IsNullOrEmpty(linea))
                            {
                                ProcesarMensajeJSON(linea);
                            }
                        }

                        // Guardar último fragmento incompleto
                        bufferParcial = lineas[lineas.Length - 1];
                    }

                    Thread.Sleep(10);
                }
                catch (TimeoutException)
                {
                    // Timeout normal, continuar
                }
                catch (Exception ex)
                {
                    if (sistemaActivo)
                    {
                        OnErrorOcurrido($"Error en lectura: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Procesar mensaje JSON recibido
        /// </summary>
        private void ProcesarMensajeJSON(string mensaje)
        {
            try
            {
                OnMensajeLog($"← Recibido: {mensaje}");

                // Agregar a cola para procesamiento futuro
                lock (colaMensajes)
                {
                    colaMensajes.Enqueue(mensaje);

                    // Limitar tamaño de la cola
                    while (colaMensajes.Count > 100)
                    {
                        colaMensajes.Dequeue();
                    }
                }

                // Parsear JSON
                JObject json = JObject.Parse(mensaje);

                // Procesar según tipo de mensaje
                string tipo = json["tipo"]?.ToString();
                string evento = json["evento"]?.ToString();

                if (!string.IsNullOrEmpty(evento))
                {
                    ProcesarEvento(json, evento);
                }
                else if (!string.IsNullOrEmpty(tipo))
                {
                    ProcesarTipoMensaje(json, tipo);
                }
            }
            catch (JsonException ex)
            {
                OnErrorOcurrido($"Error parseando JSON: {ex.Message}");
                OnMensajeLog($"Mensaje problemático: {mensaje}");
            }
            catch (Exception ex)
            {
                OnErrorOcurrido($"Error procesando mensaje: {ex.Message}");
            }
        }

        /// <summary>
        /// Procesar eventos
        /// </summary>
        private void ProcesarEvento(JObject json, string evento)
        {
            switch (evento)
            {
                case "SOLICITUD_ACCESO":
                    ProcesarSolicitudAcceso(json);
                    break;

                case "VEHICULO_ENTRADA":
                    vehiculoPresente = true;
                    OnVehiculoDetectado(new EventoVehiculo
                    {
                        Tipo = TipoEventoVehiculo.Entrada,
                        Timestamp = json["timestamp"]?.Value<long>() ?? 0,
                        FechaHora = DateTime.Now
                    });
                    break;

                case "VEHICULO_SALIDA":
                    vehiculoPresente = false;
                    OnVehiculoDetectado(new EventoVehiculo
                    {
                        Tipo = TipoEventoVehiculo.Salida,
                        Timestamp = json["timestamp"]?.Value<long>() ?? 0,
                        FechaHora = DateTime.Now
                    });
                    break;
            }
        }

        /// <summary>
        /// Procesar tipos de mensaje
        /// </summary>
        private void ProcesarTipoMensaje(JObject json, string tipo)
        {
            switch (tipo)
            {
                case "INICIO_SISTEMA":
                    OnMensajeLog($"Sistema Arduino: v{json["version"]} - {json["nombre"]}");
                    break;

                case "ESTADO_COMPLETO":
                    ActualizarEstado(json);
                    break;

                case "CAMBIO_ESTADO":
                    ProcesarCambioEstado(json);
                    break;

                case "DIAGNOSTICO_VEK":
                    OnMensajeLog($"Diagnóstico VEK: {json["interpretacion"]}");
                    break;

                case "SISTEMA_RESETEADO":
                    OnMensajeLog("Sistema Arduino reseteado");
                    estadoActual = "ESPERANDO";
                    break;
            }
        }

        /// <summary>
        /// Procesar solicitud de acceso
        /// </summary>
        private void ProcesarSolicitudAcceso(JObject json)
        {
            ultimaSolicitud = DateTime.Now;

            var evento = new EventoSolicitudAcceso
            {
                FechaHora = DateTime.Now,
                TimestampArduino = json["timestamp"]?.Value<long>() ?? 0,
                EstadoVEK = json["estado_vek"]?.Value<bool>() ?? false
            };

            OnSolicitudAccesoRecibida(evento);
        }

        /// <summary>
        /// Actualizar estado del sistema
        /// </summary>
        private void ActualizarEstado(JObject json)
        {
            string nuevoEstado = json["estado"]?.ToString() ?? "DESCONOCIDO";

            if (nuevoEstado != estadoActual)
            {
                string estadoAnterior = estadoActual;
                estadoActual = nuevoEstado;

                OnEstadoCambiado(new EventoCambioEstado
                {
                    EstadoAnterior = estadoAnterior,
                    EstadoNuevo = nuevoEstado,
                    FechaHora = DateTime.Now
                });
            }

            vehiculoPresente = json["vehiculo_presente"]?.Value<bool>() ?? false;
        }

        /// <summary>
        /// Procesar cambio de estado
        /// </summary>
        private void ProcesarCambioEstado(JObject json)
        {
            string anterior = json["estado_anterior"]?.ToString();
            string nuevo = json["estado_nuevo"]?.ToString();

            estadoActual = nuevo;

            OnEstadoCambiado(new EventoCambioEstado
            {
                EstadoAnterior = anterior,
                EstadoNuevo = nuevo,
                FechaHora = DateTime.Now,
                TimestampArduino = json["timestamp"]?.Value<long>() ?? 0
            });
        }

        /// <summary>
        /// Parsear diagnóstico VEK
        /// </summary>
        private DiagnosticoVEK ParsearDiagnostico(string mensaje)
        {
            try
            {
                JObject json = JObject.Parse(mensaje);

                if (json["tipo"]?.ToString() == "DIAGNOSTICO_VEK")
                {
                    return new DiagnosticoVEK
                    {
                        Relay1 = json["relay1"]?.Value<bool>() ?? false,
                        Relay2 = json["relay2"]?.Value<bool>() ?? false,
                        Interpretacion = json["interpretacion"]?.ToString(),
                        ConfigDIP = json["config_dip"]?.ToString()
                    };
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Timer para solicitar estado periódicamente
        /// </summary>
        private async void TimerStatus_Elapsed(object sender, ElapsedEventArgs e)
        {
            await EnviarComandoAsync("STATUS");
        }

        #endregion

        #region Eventos Protegidos

        protected virtual void OnSolicitudAccesoRecibida(EventoSolicitudAcceso e)
        {
            SolicitudAccesoRecibida?.Invoke(this, e);
        }

        protected virtual void OnVehiculoDetectado(EventoVehiculo e)
        {
            VehiculoDetectado?.Invoke(this, e);
        }

        protected virtual void OnEstadoCambiado(EventoCambioEstado e)
        {
            EstadoCambiado?.Invoke(this, e);
        }

        protected virtual void OnMensajeLog(string mensaje)
        {
            MensajeLog?.Invoke(this, mensaje);
        }

        protected virtual void OnErrorOcurrido(string error)
        {
            ErrorOcurrido?.Invoke(this, error);
        }

        #endregion

        #region Propiedades Públicas

        /// <summary>
        /// Estado actual del sistema
        /// </summary>
        public string EstadoActual => estadoActual;

        /// <summary>
        /// Indica si hay un vehículo presente
        /// </summary>
        public bool VehiculoPresente => vehiculoPresente;

        /// <summary>
        /// Última solicitud de acceso
        /// </summary>
        public DateTime UltimaSolicitud => ultimaSolicitud;

        /// <summary>
        /// Indica si el sistema está activo
        /// </summary>
        public bool SistemaActivo => sistemaActivo;

        #endregion
    }

    #region Clases de Eventos

    /// <summary>
    /// Evento de solicitud de acceso
    /// </summary>
    public class EventoSolicitudAcceso : EventArgs
    {
        public DateTime FechaHora { get; set; }
        public long TimestampArduino { get; set; }
        public bool EstadoVEK { get; set; }

        public override string ToString()
        {
            return $"Solicitud de acceso: {FechaHora:yyyy-MM-dd HH:mm:ss} (VEK: {EstadoVEK})";
        }
    }

    /// <summary>
    /// Evento de detección de vehículo
    /// </summary>
    public class EventoVehiculo : EventArgs
    {
        public TipoEventoVehiculo Tipo { get; set; }
        public DateTime FechaHora { get; set; }
        public long Timestamp { get; set; }

        public override string ToString()
        {
            return $"Vehículo {Tipo}: {FechaHora:HH:mm:ss}";
        }
    }

    /// <summary>
    /// Tipo de evento de vehículo
    /// </summary>
    public enum TipoEventoVehiculo
    {
        Entrada,
        Salida
    }

    /// <summary>
    /// Evento de cambio de estado
    /// </summary>
    public class EventoCambioEstado : EventArgs
    {
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public DateTime FechaHora { get; set; }
        public long TimestampArduino { get; set; }

        public override string ToString()
        {
            return $"Estado: {EstadoAnterior} → {EstadoNuevo}";
        }
    }

    /// <summary>
    /// Resultado de diagnóstico VEK
    /// </summary>
    public class DiagnosticoVEK
    {
        public bool Relay1 { get; set; }
        public bool Relay2 { get; set; }
        public string Interpretacion { get; set; }
        public string ConfigDIP { get; set; }

        public override string ToString()
        {
            return $"VEK: {Interpretacion} (R1:{Relay1}, R2:{Relay2})";
        }
    }

    #endregion

    /// <summary>
    /// Programa principal de ejemplo
    /// </summary>
    class Program
    {
        static ControlAccesoVehicularJSON controlAcceso;
        static bool ejecutando = true;
        static int contadorAccesos = 0;

        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════╗");
            Console.WriteLine("║    SISTEMA DE CONTROL DE ACCESO VEHICULAR      ║");
            Console.WriteLine("║               Versión 2.0 - JSON               ║");
            Console.WriteLine("╚════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Mostrar puertos disponibles
            await MostrarPuertosDisponibles();

            // Seleccionar puerto
            Console.Write("Ingrese el puerto COM (ej: COM3): ");
            string puerto = Console.ReadLine();
            if (string.IsNullOrEmpty(puerto))
                puerto = "COM3";

            // Inicializar sistema
            controlAcceso = new ControlAccesoVehicularJSON(puerto);

            // Suscribir eventos
            SuscribirEventos();

            // Iniciar sistema
            Console.WriteLine("\nIniciando conexión con Arduino...");
            if (await controlAcceso.IniciarSistemaAsync())
            {
                Console.WriteLine("✓ Sistema iniciado correctamente\n");

                MostrarComandosDisponibles();

                // Procesar comandos del usuario
                await ProcesarComandosUsuario();
            }
            else
            {
                Console.WriteLine("✗ Error al iniciar el sistema");
                Console.WriteLine("Verifique la conexión y el puerto seleccionado");
            }

            // Limpiar y salir
            Console.WriteLine("\nCerrando sistema...");
            controlAcceso.DetenerSistema();
            Console.WriteLine("Sistema cerrado. Presione cualquier tecla para salir.");
            Console.ReadKey();
        }

        /// <summary>
        /// Mostrar puertos COM disponibles
        /// </summary>
        static async Task MostrarPuertosDisponibles()
        {
            await Task.Run(() =>
            {
                string[] puertos = ControlAccesoVehicularJSON.ObtenerPuertosDisponibles();
                Console.WriteLine("Puertos COM disponibles:");

                if (puertos.Length == 0)
                {
                    Console.WriteLine("  - No se encontraron puertos disponibles");
                }
                else
                {
                    foreach (string puerto in puertos)
                    {
                        Console.WriteLine($"  - {puerto}");
                    }
                }
                Console.WriteLine();
            });
        }

        /// <summary>
        /// Suscribir a todos los eventos del sistema
        /// </summary>
        static void SuscribirEventos()
        {
            controlAcceso.SolicitudAccesoRecibida += ControlAcceso_SolicitudAccesoRecibida;
            controlAcceso.VehiculoDetectado += ControlAcceso_VehiculoDetectado;
            controlAcceso.EstadoCambiado += ControlAcceso_EstadoCambiado;
            controlAcceso.MensajeLog += ControlAcceso_MensajeLog;
            controlAcceso.ErrorOcurrido += ControlAcceso_ErrorOcurrido;
        }

        /// <summary>
        /// Manejador del evento de solicitud de acceso
        /// </summary>
        static async void ControlAcceso_SolicitudAccesoRecibida(object sender, EventoSolicitudAcceso e)
        {
            contadorAccesos++;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n╔══════════════════════════════════════════════════╗");
            Console.WriteLine($"║        SOLICITUD DE ACCESO #{contadorAccesos:D4}                ║");
            Console.WriteLine($"╠══════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Fecha/Hora: {e.FechaHora:yyyy-MM-dd HH:mm:ss}            ║");
            Console.WriteLine($"║ Estado VEK: {(e.EstadoVEK ? "Confirmado" : "No confirmado")}                      ║");
            Console.WriteLine($"╚══════════════════════════════════════════════════╝");
            Console.ResetColor();

            // Simular proceso de acceso
            await ProcesarAccesoVehicular();
        }

        /// <summary>
        /// Procesar acceso vehicular (simulación)
        /// </summary>

        static async Task ProcesarAccesoVehicular()
        {
            SerialPort SP1 = new SerialPort();

            Parametros Parametros = null;

            Neural n = new Neural();

            Boleta b = new Boleta();

            Barrera barrera = new Barrera();

            var resultado = await n.getPatente();
            string patente = resultado.patente;
            int? id = resultado.incidenceID;

            Console.WriteLine($"Patente  : {patente}, ID   : {id}");



            // 1. Instanciar la clase Cajas
            Cajas caja = new Cajas();

            // 2. Llamar al método ObtenerInfoCaja() para obtener los datos de la caja activa (según IP/estado)
            Cajas infoCaja = caja.ObtenerInfoCaja();

            int numeroTicketActual = b.ObtenerUltimoTicket() + 1;
            //b.PatenteActual = patente;

            if (infoCaja != null)
            {

                b.num_boleta = 0;

                b.patente = patente;

                b.cod_turno = 0;

                b.cod_usuario = 0;

                b.cod_tipo_usuario = 0;

                b.nombre_acceso = "A1";

                b.num_caja = infoCaja.num_caja;

                b.incidenceID = id;

                b.CajaActual = infoCaja;

            }


            if (b.PrintTicket(numeroTicketActual) == true)
            {
                b.InsertarTicket(b);
                barrera.AbrirBarrera(true);
            }
            else
            {
                Console.WriteLine("IMPRESORA NO DISPONIBLE. VERIFICAR CONEXIÓN O CONFIGURACIÓN DE IMPRESORA.");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  ✓ ACCESO AUTORIZADO - Proceso completado\n");
            Console.ResetColor();
        }
    
        /// <summary>
        /// Manejador de detección de vehículo
        /// </summary>
        static void ControlAcceso_VehiculoDetectado(object sender, EventoVehiculo e)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🚗 Vehículo {e.Tipo}");
            Console.ResetColor();
        }

        /// <summary>
        /// Manejador de cambio de estado
        /// </summary>
        static void ControlAcceso_EstadoCambiado(object sender, EventoCambioEstado e)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Estado: {e.EstadoAnterior} → {e.EstadoNuevo}");
            Console.ResetColor();

            // Mostrar indicador visual del estado
            MostrarEstadoVisual(e.EstadoNuevo);
        }

        /// <summary>
        /// Mostrar estado visual
        /// </summary>
        static void MostrarEstadoVisual(string estado)
        {
            Console.Write("          ");

            switch (estado)
            {
                case "ESPERANDO":
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine("[⚪ Esperando vehículo]");
                    break;

                case "VEHICULO_DETECTADO":
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[🟡 Vehículo presente - Esperando botón]");
                    break;

                case "PROCESANDO":
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("[🔵 Procesando solicitud]");
                    break;

                case "COOLDOWN":
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("[🟣 Periodo de espera]");
                    break;
            }

            Console.ResetColor();
        }

        /// <summary>
        /// Manejador de mensajes de log
        /// </summary>
        static void ControlAcceso_MensajeLog(object sender, string mensaje)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {mensaje}");
            Console.ResetColor();
        }

        /// <summary>
        /// Manejador de errores
        /// </summary>
        static void ControlAcceso_ErrorOcurrido(object sender, string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {error}");
            Console.ResetColor();
        }

        /// <summary>
        /// Mostrar comandos disponibles
        /// </summary>
        static void MostrarComandosDisponibles()
        {
            Console.WriteLine("\n┌─────────────────────────────────────┐");
            Console.WriteLine("│     COMANDOS DISPONIBLES            │");
            Console.WriteLine("├─────────────────────────────────────┤");
            Console.WriteLine("│ S - Solicitar estado del sistema    │");
            Console.WriteLine("│ D - Diagnóstico del sensor VEK      │");
            Console.WriteLine("│ R - Resetear sistema                │");
            Console.WriteLine("│ H - Ayuda (comandos Arduino)        │");
            Console.WriteLine("│ C - Limpiar pantalla                │");
            Console.WriteLine("│ Q - Salir del programa              │");
            Console.WriteLine("└─────────────────────────────────────┘\n");
        }

        /// <summary>
        /// Procesar comandos del usuario
        /// </summary>
        static async Task ProcesarComandosUsuario()
        {
            while (ejecutando)
            {
                if (Console.KeyAvailable)
                {
                    var tecla = Console.ReadKey(true);

                    switch (tecla.Key)
                    {
                        case ConsoleKey.S:
                            await controlAcceso.EnviarComandoAsync("STATUS");
                            break;

                        case ConsoleKey.D:
                            Console.WriteLine("\nSolicitando diagnóstico VEK...");
                            var diag = await controlAcceso.SolicitarDiagnosticoAsync();
                            if (diag != null)
                            {
                                Console.WriteLine($"Diagnóstico: {diag}");
                            }
                            break;

                        case ConsoleKey.R:
                            await controlAcceso.EnviarComandoAsync("RESET");
                            break;

                        case ConsoleKey.H:
                            await controlAcceso.EnviarComandoAsync("HELP");
                            break;

                        case ConsoleKey.C:
                            Console.Clear();
                            MostrarComandosDisponibles();
                            break;

                        case ConsoleKey.Q:
                            ejecutando = false;
                            break;
                    }
                }

                await Task.Delay(100);
            }
        }
    }
}