using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
namespace AccesoDatos
{
    public class Patente
    {
        public int? id { get; set; }
        public string NumberPlate { get; set; }
        public string ImagePath { get; set; }

        public Patente GetPlate(string id_camEntrada)
        {
            using (IDbConnection connection = new SqlConnection(Helper.CnnVal("LP")))
            {

                var output = connection.Query<Patente>("dbo.usp_GetPlate @id_camEntrada", new { id_camEntrada = id_camEntrada }).SingleOrDefault();

               
                return output;
            }
        }


        public string PrimerRegistroDiario(int? id)
        {

            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("LP")))
            {
                var output = connection.QueryFirstOrDefault<string>("dbo.usp_PrimerRegistroDiario", param: new
                { @id = id }, commandType: CommandType.StoredProcedure);


                return output;
            }

        }

    }
}
