using System;
using System.Data.SqlClient;
using System.Data;
using AccesoDatos;
using Dapper;
using System.Globalization;
using System.Drawing;
using System.Drawing.Printing;
using BarcodeLib;
using System.Linq;

namespace SistemaEstacionamiento
{
    public class Boleta
    {


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


        private PrintDocument pdPrint;

        private Image codigoBarrasTicket;

        private int numeroTicketActual;

        public string PatenteActual { get; set; }
        public Cajas CajaActual { get; set; }


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

        public int ObtenerUltimoTicket()
        {
            using (IDbConnection connection = new SqlConnection(Helper.CnnVal("PM")))
            {
                int output = connection.Query<int?>("dbo.sp_GetUltimoFolio").Single() ?? 0;
                return output;
            }
        }

        public string ObtenerNombreImpresora()
        {
            try
            {

                using (IDbConnection Conexion = new System.Data.SqlClient.SqlConnection(Helper.CnnVal("PM")))
                {

                    var Resultado = Conexion.QuerySingleOrDefault("SELECT nom_impresora FROM parametros");

                    return Resultado.nom_impresora;

                }

            }

            catch (Exception ex)
            {

                Console.WriteLine("UserLog", $"IMPRESORA NO ENCONTRADA. VERIFICAR EN TABLA PARÁMETROS EL NOMBRE CORRECTO DE LA IMPRESORA, O BIEN, SU CORRECTA INSTALACION");
                return null;

            }


        }


        #region Método de impresión de ticket
        public bool PrintTicket(int num_ticket)
        {
            string NOMBRE_IMPRESORA = ObtenerNombreImpresora();

            Barcode ticket = new Barcode();

            ticket.IncludeLabel = true;
            codigoBarrasTicket = ticket.Encode(TYPE.CODE128, num_ticket.ToString(), Color.Black, Color.White, 110, 110);


            pdPrint = new PrintDocument();
            pdPrint.PrinterSettings.PrinterName = NOMBRE_IMPRESORA;
            PaperSize ps = new PaperSize("", 250, 300);
            if (pdPrint.PrinterSettings.IsValid)
            {
                pdPrint.DocumentName = "TicketIngreso";
                pdPrint.PrintPage += new PrintPageEventHandler(this.ContenidoTicket);
                pdPrint.PrintController = new StandardPrintController();

                pdPrint.DefaultPageSettings.Margins.Left = 0;
                pdPrint.DefaultPageSettings.Margins.Right = 0;
                pdPrint.DefaultPageSettings.Margins.Top = 0;
                pdPrint.DefaultPageSettings.Margins.Bottom = 0;

                pdPrint.DefaultPageSettings.PaperSize = ps;

                pdPrint.Print();
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ContenidoTicket(object sender, PrintPageEventArgs e)
        {

            int numeroTicketActual = ObtenerUltimoTicket() + 1;

           


            Graphics g = e.Graphics;
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;


            Font cabecera = new Font("CALIBRI", (float)11.5, FontStyle.Regular);
            Font fBody = new Font("CALIBRI", (float)11.5, FontStyle.Regular);
            Font pie = new Font("CALIBRI", (float)10, FontStyle.Regular);
            SolidBrush sb = new SolidBrush(Color.Black);
            int y = 2;
            int SPACE = 20;
            RectangleF cb = new RectangleF(5, SPACE + 200, 250, 100);
            RectangleF rect = new RectangleF(5, SPACE + 310, 250, 300);
            RectangleF rect1 = new RectangleF(5, SPACE + 320, 250, 300);

            g.DrawString("COMERCIALIZADORA VEGA MONUMENTAL S.A.", cabecera, sb, y, SPACE);
            g.DrawString("AVENIDA 21 DE MAYO #3315", cabecera, sb, y, SPACE + 15);
            g.DrawString("CONCEPCIÓN", cabecera, sb, y, SPACE + 30);
            g.DrawString("SUC: EST. CENTRO COMERCIAL", cabecera, sb, y, SPACE + 45);
            g.DrawString("AVENIDA 21 DE MAYO #3225", cabecera, sb, y, SPACE + 60);
            g.DrawString("RUT Nro.: 76.126.876-7", cabecera, sb, y, SPACE + 75);
            g.DrawString("GIRO: ESTACIONAMIENTOS", cabecera, sb, y, SPACE + 90);

            g.DrawString($"N° TICKET: {numeroTicketActual}", cabecera, sb, y, SPACE + 120);

            g.DrawString($"FECHA : {DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}", cabecera, sb, y, SPACE + 140);
            g.DrawString($"HORA : {DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}", cabecera, sb, 280, SPACE + 140, new StringFormat(StringFormatFlags.DirectionRightToLeft));

            if (this.CajaActual != null && !string.IsNullOrEmpty(this.CajaActual.ubicacion))
            {
                g.DrawString($"ACCESO: {this.CajaActual.ubicacion}", fBody, sb, y, SPACE + 155);
            }
            else
            {
                g.DrawString("ACCESO: NO DETECTADO", fBody, sb, y, SPACE + 155);
            }
            g.DrawString($"PATENTE: {this.patente}", fBody, sb, y, SPACE + 170);
            if (codigoBarrasTicket != null)
                g.DrawImage(codigoBarrasTicket, cb);

            g.DrawString("Conserve este ticket.", pie, sb, rect, sf);
            g.DrawString("Recuerde cancelar antes de salir.", pie, sb, rect1, sf);
            // Indicate that no more data to print, and the Print Document can now send the print data to the spooler.
            e.HasMorePages = false;
        }
        #endregion


    }
}