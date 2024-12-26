using Interstellar;
using System;
using Newtonsoft.Json.Linq;

namespace Interstellar
{
    public static class Responses
    {
        public static object error(string message, object additional = null)
        {
            Console.WriteLine("additional");
            Console.WriteLine(additional);
            var errorResponse = new Objects.errorD
            {
                error = true,
                message = message,
                additional = additional
            };

            return errorResponse;
        }
    }
}