using AccesoDatos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace SistemaEstacionamiento.Modelo
{
   public class Neural
   {

        int? incidenceID;
        public Parametros parametros = new Parametros();


        internal static bool Pinger(string direccion)
        {
            bool resp = false;
            Ping p = new Ping();
            try
            {
                PingReply r = p.Send(direccion, 2000);

                if (r.Status == IPStatus.Success)
                    resp = true;
            }
            catch
            {
                resp = false;
            }

            return resp;
        }

        internal void msjCajero()
        {
            parametros = parametros.GetMsjCajero();

            string msj = parametros.msjCajero;
            //txtmsjCajero.Text = msj;

        }

        private string id_camEntrada()
        {
            string id_camEntrada = null;
            parametros = parametros.GetCamEntrada();
            id_camEntrada = Convert.ToString(parametros.camEntrada);
            return id_camEntrada;
        }
        private string id_cameraMask()
        {
            string cameraMask = null;
            parametros = parametros.GetcameraMask();
            cameraMask = Convert.ToString(parametros.cameraMask);
            return cameraMask;
        }

        public async Task<String> getPatente()
        {
            parametros = parametros.GetdirANPR();
            string ip_neural = parametros.dir_ANPR;

            //LLAMA A FUNCION DE PING A DIRECCIÓN DE LPR. SI HACE PING REALIZA FUNCION GETPATENTE, SINO, RESPONDE CON NO_CAMARA.
            bool r = Pinger(ip_neural);
            string patente = null;

            //string date = DateTime.Now.ToString("yyyy-MM-dd");
            //string datetime = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
            //string filename = @"c:\tmp\imgs\" + date + @"\" + datetime + ".jpg"; // path de imgs

            if (r == true)
            {
                msjCajero();
                parametros = parametros.GetLecturaPatente();
                bool lecturaPatente = parametros.LecturaPatente;

                if (lecturaPatente == true)
                {

                    Patente p = new Patente();

                    //leer patente
                    //envio de trigger a cámara
                    TcpClient mClient;
                    string id_camentrada = id_camEntrada();
                    string id_cameramask = id_cameraMask();
                    string mRequest_EVENT = "<?xml version='1.0' encoding='UTF-8'?>" +
                                            "<Alarm><AlarmDetail><AlarmType>##</AlarmType><ExtraFields>" +
                                            "<EF Name='CameraMask'>??</EF>" +
                                            "<EF Name='PathImage'></EF>" +
                                            "<EF Name='PathEvidence'></EF>" +
                                            "</ExtraFields></AlarmDetail></Alarm>";
                    //Console.WriteLine(mRequest_EVENT);
                    mClient = new TcpClient();
                    mClient.Connect(ip_neural, 8040);
                    string aux = mRequest_EVENT.Replace("##", "40"); //alarmtype
                    //aux = aux.Replace("!!", @"c:\tmp"); //pathimage
                    aux = aux.Replace("!!", @"D:\tmp"); //pathimage
                    //aux = aux.Replace("**", @"c:\tmp"); //pathevidence
                    aux = aux.Replace("**", @"D:\tmp"); //pathevidence


                    byte[] bytes = Encoding.ASCII.GetBytes(aux.Replace("??", id_cameramask)); //cameramask

                    NetworkStream stream = mClient.GetStream();

                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();

                    //recibo de patente
                    parametros = parametros.GetAwait();
                    int _await = parametros.await_LecturaPatente;
                    await Task.Delay(_await);
                    p = p.GetPlate(id_camentrada);

                    string _ultimoid = p.NumberPlate;
                    incidenceID = p.id;


                    if (_ultimoid == "NO_PLATE")
                    {
                        patente = "NO_PATENTE";
                        string target = p.ImagePath;
                        //string ruta = target.Replace(@"c:\", @"\\" + ip_neural + @"\c$\");
                        string ruta = target.Replace(@"D:\", @"\\" + ip_neural + @"\D$\");
                        

                    }
                    else
                    {
                        patente = _ultimoid;
                        string target = p.ImagePath;
                        //string ruta = target.Replace(@"c:\", @"\\" + ip_neural + @"\c$\");
                        string ruta = target.Replace(@"D:\", @"\\" + ip_neural + @"\D$\");
                        

                    }

                    mClient.Close();

                }
                else
                {
                    patente = "NO_CAMARA";
                    
                }
            }
            else
            {
                patente = "NO_CAMARA";
                
            }

            return patente;
        }

    }
}
