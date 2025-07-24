using System;
using System.IO.Ports;
using System.Threading;

namespace SistemaEstacionamiento.Modelo
{
    public class Barrera
    {
        private static readonly object _lockObject = new object();

        public void AbrirBarrera(bool Entrada)
        {
            // Usar lock para evitar acceso concurrente
            lock (_lockObject)
            {
                SerialPort barrera = null;
                try
                {
                    Parametros parametros = new Parametros();
                    var Parametros = parametros.ObtenerParametros();
                    string BarreraDeterminada = Entrada ? Parametros.string_rele : Parametros.string_rele2;
                    int COM_rele = Parametros.COM_rele;
                    string portName = "COM" + COM_rele;

                    // Crear una nueva instancia del puerto serial
                    barrera = new SerialPort(portName)
                    {
                        BaudRate = 9600,    // Ajusta según tu dispositivo
                        Parity = Parity.None,
                        DataBits = 8,
                        StopBits = StopBits.One,
                        Handshake = Handshake.None,
                        ReadTimeout = 500,
                        WriteTimeout = 500
                    };

                    // El puerto se obtiene de la base de datos, no necesita validación

                    // Abrir el puerto y enviar comando
                    barrera.Open();
                    Console.WriteLine($"Puerto {portName} abierto exitosamente");

                    barrera.Write(BarreraDeterminada);
                    Console.WriteLine($"Comando enviado: {BarreraDeterminada}");

                    // Pequeña pausa para asegurar que el comando se envíe
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.WriteLine($"ERROR: Acceso denegado al puerto. El puerto puede estar siendo usado por otra aplicación.");
                    Console.WriteLine($"Detalles: {ex.Message}");
                    Console.WriteLine("SOLUCIONES POSIBLES:");
                    Console.WriteLine("1. Cerrar otras aplicaciones que puedan estar usando el puerto");
                    Console.WriteLine("2. Desconectar y reconectar el dispositivo");
                    Console.WriteLine("3. Reiniciar la aplicación");
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"ERROR: Nombre de puerto inválido o parámetros incorrectos.");
                    Console.WriteLine($"Detalles: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"ERROR: El puerto ya está abierto o en un estado inválido.");
                    Console.WriteLine($"Detalles: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR GENERAL: {ex.Message}");
                    Console.WriteLine("NO EXISTE CONEXION ENTRE EL RELE Y LA BARRERA");
                }
                finally
                {
                    // CRÍTICO: Siempre cerrar el puerto en el bloque finally
                    if (barrera != null && barrera.IsOpen)
                    {
                        try
                        {
                            barrera.Close();
                            barrera.Dispose();
                            Console.WriteLine("Puerto cerrado correctamente");
                        }
                        catch (Exception closeEx)
                        {
                            Console.WriteLine($"Error al cerrar el puerto: {closeEx.Message}");
                        }
                    }
                }
            }
        }

        // Método alternativo usando 'using' statement (recomendado)
        public void AbrirBarreraConUsing(bool Entrada)
        {
            lock (_lockObject)
            {
                try
                {
                    Parametros parametros = new Parametros();
                    var Parametros = parametros.ObtenerParametros();
                    string BarreraDeterminada = Entrada ? Parametros.string_rele : Parametros.string_rele2;
                    int COM_rele = Parametros.COM_rele;
                    string portName = "COM" + COM_rele;

                    // Usar 'using' para gestión automática de recursos
                    using (SerialPort barrera = new SerialPort(portName))
                    {
                        // Configurar puerto
                        barrera.BaudRate = 9600;
                        barrera.Parity = Parity.None;
                        barrera.DataBits = 8;
                        barrera.StopBits = StopBits.One;
                        barrera.Handshake = Handshake.None;
                        barrera.ReadTimeout = 500;
                        barrera.WriteTimeout = 500;

                        // El puerto se obtiene de la base de datos (COM7)

                        barrera.Open();
                        barrera.Write(BarreraDeterminada);
                        Thread.Sleep(100); // Pausa para asegurar envío

                        Console.WriteLine($"Comando enviado exitosamente a {portName}");
                    } // El puerto se cierra automáticamente aquí
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("ERROR: Acceso denegado al puerto COM. Posibles soluciones:");
                    Console.WriteLine("1. Verificar que el puerto no esté siendo usado por otra aplicación");
                    Console.WriteLine("2. Ejecutar la aplicación como administrador");
                    Console.WriteLine("3. Verificar permisos del usuario actual");
                    Console.WriteLine("4. Desconectar y reconectar el dispositivo");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex.Message}");
                    Console.WriteLine("NO EXISTE CONEXION ENTRE EL RELE Y LA BARRERA");
                }
            }
        }

        // Método para verificar puertos disponibles
        public static void MostrarPuertosDisponibles()
        {
            string[] puertos = SerialPort.GetPortNames();
            Console.WriteLine("Puertos COM disponibles:");
            foreach (string puerto in puertos)
            {
                Console.WriteLine($"- {puerto}");
            }
        }
    }
}