using AccesoDatos;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
namespace SistemaEstacionamiento.Modelo
{
    public class Turno
    {
        public int cod_turno { get; set; }
        public string nombre_turno { get; set; }

        public Turno GetTurno()
        {
            //Consulta para rescatar los datos del usuario que ingresa al sistema
            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
            {
                var output = connection.QuerySingleOrDefault<Turno>("dbo.sp_GetTurno");


                return output;
            }

        }
        public List<Turno> listTurnos()
        {
            //listar turnos
            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
            {
                var output = connection.Query<Turno>("select cod_turno, nombre_turno from dbo.listTurnos").AsList();


                return output;
            }
        }
        public Turno modificarTurno(int cod)
        {
            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
            {
                DynamicParameters p = new DynamicParameters();
                p.Add("@id", cod);
                //connection.Execute("dbo.sp_ModificarTurno", p, commandType: CommandType.StoredProcedure);
                var output = connection.QuerySingleOrDefault<Turno>("dbo.sp_ModificarTurno", p, commandType: CommandType.StoredProcedure);

                return output;
            }
        }

        public Turno validarTurno()
        {
            using (IDbConnection connection = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
            {
                var output = connection.QuerySingleOrDefault<Turno>("dbo.sp_ValidarTurno");

                return output;
            }

        }

    }






}