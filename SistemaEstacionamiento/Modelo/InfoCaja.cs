using Dapper;
using System.Data;
using System.Data.SqlClient;
using SistemaEstacionamiento;

namespace AccesoDatos
{
    public class Cajas
    {
        public int num_caja { get; set; }
        public string nombre { get; set; }
        public string ubicacion { get; set; }
        public int estado { get; set; }
        public Cajas ObtenerInfoCaja()
        {
            using (IDbConnection connection = new SqlConnection(Helper.CnnVal("PM")))
            {
                var output = connection.QueryFirstOrDefault<Cajas>("SELECT * FROM cajas WHERE estado = 1");
                return output;
            }

        }

    }
}
