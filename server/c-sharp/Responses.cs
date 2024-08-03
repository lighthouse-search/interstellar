using Interstellar;
using System;

namespace Interstellar
{
    public class Responses {
        public static Objects.errorD error(string message)
        {
            return new Objects.errorD 
            {
                error = true,
                message = message
            };
        }
    }
}