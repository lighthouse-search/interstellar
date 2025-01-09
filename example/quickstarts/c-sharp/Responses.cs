using External_server;
using System;
using System.Net;
using Newtonsoft.Json.Linq;
using MySql.Data.MySqlClient;

namespace External_server
{
    public static class Responses
    {
        public static async Task handle(HttpContext context, MySqlConnection connection, int status, object data)
        {
            await connection.CloseAsync();
            if (context.Response.HasStarted == true) {
                Console.WriteLine("Response has already started...aborting.");
                return;
            }
            context.Response.StatusCode = status;
            if (data != null) {
                await context.Response.WriteAsJsonAsync(data);
            }
            await context.Response.CompleteAsync();
        }

        public static async Task error(HttpContext context, MySqlConnection connection, int status, string message, object additional = null)
        {
            var error_response = new Objects.errorD
            {
                error = true,
                message = message,
                additional = additional
            };

            await handle(context, connection, status, error_response);
        }
    }
}