using External_server;
using System;
using System.Net;
using Newtonsoft.Json.Linq;
using MySql.Data.MySqlClient;

namespace External_server
{
    public static class Sql
    {
        public static async Task<MySqlConnection> get_connection()
        {
            string connectionString = "Server=localhost;Database=interstellar_test;User=example-user;Password=my_cool_secret;";
            MySqlConnection connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }
        public static async Task<MySqlDataReader> query(MySqlCommand command)
        {
            MySqlDataReader output = null;
            MySqlDataReader reader = command.ExecuteReader();
            while (await reader.ReadAsync())
            {
                output = reader;
            }

            if (output != null) {
                return output;
            } else {
                await reader.CloseAsync();
                return null;
            }
        }
    }
}