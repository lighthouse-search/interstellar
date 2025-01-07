using System;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Interstellar
{
    public static class Variable_operations
    {
        public static float from_string_to_float(string string_value)
        {
            if (float.TryParse(string_value, out float parsed_string_value))
            {
                return parsed_string_value;
            }
            else
            {
                throw new Exception("value is a string but cannot be parsed as a float.");
            }
        }
    }
}