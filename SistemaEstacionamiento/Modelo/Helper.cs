using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using System.Data;

namespace AccesoDatos
{
    public static class Helper
    {
        public static string CnnVal(string name)
        {
            var connString = ConfigurationManager.ConnectionStrings[name].ConnectionString;

            return connString;
        }

        public static string CnnDB(string name)
        {
            var db = new SqlConnectionStringBuilder(CnnVal(name)).DataSource;
            return db;
        }
    }
}
