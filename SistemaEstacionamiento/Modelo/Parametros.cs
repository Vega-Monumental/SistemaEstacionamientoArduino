using AccesoDatos;
using Dapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SistemaEstacionamiento.Modelo
{
    public class Parametros
    {
        [DefaultValue(0)]
        public bool LecturaPatente { get; set; }
        public string msjCajero { get; set; }
        public int camEntrada { get; set; }
        public int camSalida { get; set; }
        public int await_LecturaPatente { get; set; }
        public string dir_ANPR { get; set; }
        public int cameraMask { get; set; }
        public int num_caja { get; set; }
        public string nom_impresora { get; set; }
        public int COM_rele { get; set; }
        public string string_rele { get; set; }
        public string string_rele2 { get; set; }
        public int await_boleta_barrera { get; set; }



        public Parametros ObtenerParametros()
        {
            var connection = new   System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM"));
            return connection.QueryFirstOrDefault<Parametros>(
                "SELECT * FROM parametros");
        }

        public Parametros GetLecturaPatente()
        {
            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
            {
                var output = connection.QuerySingleOrDefault<Parametros>("select lecturaPatente from dbo.parametros");

                return output;
            }

        }

        public Parametros GetAwait()
        {
            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
            {
                var output = connection.QuerySingleOrDefault<Parametros>("select await_LecturaPatente from dbo.parametros");

                return output;
            }

        }
        public Parametros GetdirANPR()
        {
            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
            {
                var output = connection.QuerySingleOrDefault<Parametros>("select dir_ANPR from dbo.parametros");

                return output;
            }

        }

        public Parametros GetMsjCajero()
        {
            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
            {
                var output = connection.QuerySingleOrDefault<Parametros>("select msjCajero from dbo.parametros");

                return output;
            }

        }




        public Parametros GetCamEntrada()
        {
            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
            {
                var output = connection.QuerySingleOrDefault<Parametros>("select camEntrada from dbo.parametros");

                return output;
            }
        }

        public Parametros GetcameraMask()
        {
            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
            {
                var output = connection.QuerySingleOrDefault<Parametros>("select cameraMask from dbo.parametros");

                return output;
            }
        }
    }
}
