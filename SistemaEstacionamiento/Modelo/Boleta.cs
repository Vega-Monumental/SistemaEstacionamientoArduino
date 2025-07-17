using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using AccesoDatos;
using Dapper;

namespace SistemaEstacionamiento
{
    public class Boleta
    {

        private Usuario _usuario;
        //num_boleta
        public int num_ticket { get; set; }
        public int num_boleta { get; set; }
        public int Folio { get; set; }
        public int Cantidad { get; set; } = 1;
        public int monto { get; set; }
        public decimal IVA { get; private set; } = 1.19M;
        public decimal Neto { get { return (int)Math.Round(monto / IVA, 2); } }
        public decimal NI { get { return monto - Neto; } }
        public DateTime fechaentrada { get; set; }
        public TimeSpan horaentrada { get; set; }
        public string fechasalida { get; set; }
        public TimeSpan horasalida { get; set; }
        public string patente { get; set; }
        public int cod_turno { get; set; }
        public int cod_usuario { get; set; }
        public int cod_tipo_usuario { get; set; }
        public int num_caja { get; set; }
        public string nombre_acceso { get; set; }
        public int estado { get; set; }
        public int tipo_liberado { get; set; }

        public int? incidenceID { get; set; }
        public string fechapago { get; set; }
        public TimeSpan horapago { get; set; }

        public bool InsertarTicket(Boleta ib)
        {
            using (IDbConnection connection = new SqlConnection(Helper.CnnVal("PM")))
            {
                DynamicParameters p = new DynamicParameters();
                p.Add("@num_boleta", ib.num_boleta);
                p.Add("@patente", ib.patente);
                p.Add("@cod_turno", ib.cod_turno);
                p.Add("@cod_usuario", ib.cod_usuario);
                p.Add("@cod_tipo_usuario", ib.cod_tipo_usuario);
                p.Add("@num_caja", ib.num_caja);
                p.Add("@acceso", ib.nombre_acceso);
                p.Add("@incidenceID", ib.incidenceID);

                var count = connection.Execute("dbo.sp_InsertarTicket",
                p, commandType: CommandType.StoredProcedure);
                return count > 0;


            }

        }

    }
}
