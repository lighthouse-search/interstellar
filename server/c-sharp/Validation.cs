using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;   
using System.Text;
using System.Net;
using Newtonsoft.Json;
using Interstellar;
using Tomlyn;
using Tomlyn.Model;
using System.Reflection;

namespace Interstellar
{
    public class Validation
    {
        // Keys are derived from the base_class. If a key doesn't exist in the base_class the key won't be comparedto the secondary_class.
        public static void compare_value_between_objects_in_a_class(Action<object, object> myCoolFunction, PropertyInfo property, object base_class, object secondary_class)
        {
            var enforced_value = property.GetValue(base_class);
            if (enforced_value != null && enforced_value.Equals(-1) == false)
            {
                var client_value = property.GetValue(secondary_class);
                if (client_value != null && client_value.Equals(-1) == false) {
                    myCoolFunction(enforced_value, client_value);
                }
            }
        }

        public static bool array_includes(string str, List<String> array)
        {
            // string str = "Hello, world!";
            // new List<string> { "world", "planet", "hello" };
            // Output would be: true

            bool containsAny = array.Any(sub => str.Contains(sub));
            return containsAny;
        }
    }
}