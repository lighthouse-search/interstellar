using Interstellar;
using System;
using System.Net;
using Newtonsoft.Json.Linq;
using MySql.Data.MySqlClient;

namespace Interstellar
{
    public static class Sql
    {
        public static async Task<MySqlDataReader> query(MySqlCommand command)
        {
            MySqlDataReader output = null;
            MySqlDataReader reader = command.ExecuteReader();
            while (await reader.ReadAsync())
            {
                output = reader;
            }

            return output;
        }
    }
}