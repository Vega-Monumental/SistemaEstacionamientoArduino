using AccesoDatos;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
namespace SistemaEstacionamiento
{
    public class Usuario
    {
        public string nombre { get; set; }
        public string ap_paterno { get; set; }
        public string ap_materno { get; set; }
        public int cod_tipo_usuario { get; set; }
        public int cod_usuario { get; set; }
        public string usuario { get; set; }


        public Usuario GetUsuario(string usuario, string pass)
        {
            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
            {
                var output = connection.QuerySingleOrDefault<Usuario>("dbo.sp_login @usuario, @pass", new { usuario = usuario, pass = pass });
                return output;
            }
        }
    }
}
