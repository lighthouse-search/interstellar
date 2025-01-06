using Interstellar;
using System;
using System.Net;
using Newtonsoft.Json.Linq;

namespace Interstellar
{
    public static class Responses
    {
        public static async Task handle(HttpContext context, int status, object data)
        {
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

        public static async Task error(HttpContext context, int status, string message, object additional = null)
        {
            var error_response = new Objects.errorD
            {
                error = true,
                message = message,
                additional = additional
            };

            await handle(context, status, error_response);
        }
    }
}