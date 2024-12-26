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
    public class Config
    {
        private static void UpdateConfig(object current_mode, object default_mode)
        {
            foreach (PropertyInfo property in current_mode.GetType().GetProperties())
            {
                object current_value = property.GetValue(current_mode);
                object default_value = property.GetValue(default_mode);

                if (current_value == null)
                {
                    property.SetValue(current_mode, default_value);
                }
                else if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
                {
                    UpdateConfig(current_value, default_value);
                }
            }
        }

        public static Objects.Config Get(string mode) {
            // Read the TOML configuration from the environment variable
            string json_config = Environment.GetEnvironmentVariable("config");
            if (string.IsNullOrWhiteSpace(json_config))
            {
                throw new Exception("Configuration not found in environment variable 'config'.");
            }

            Objects.Config config = JsonConvert.DeserializeObject<Objects.Config>(json_config);

            if (!config.modes.ContainsKey("default")) {
                throw new Exception("Missing 'default configuration");
            }
            if (!config.modes.ContainsKey(mode)) {
                throw new Exception("Missing 'default configuration");
            }
            
            Objects.Config_mode default_mode = config.modes["default"];
            Objects.Config_mode current_mode = config.modes[mode];

            // foreach (PropertyInfo property in typeof(Objects.Config_mode).GetProperties())
            // {
            //     Console.WriteLine(property.Name);
            //     if (property.GetValue(property.Name) == null);
            // }

            // var mergedConfig = MergeConfigs(default_mode, current_mode);

            // return MergeConfigs(config.modes["default"], config.modes[mode]);

            // Objects.MyClass myObject = new Objects.MyClass
            // {
            //     Name = "Alice",
            //     Age = 30,
            //     Country = "Wonderland"
            // };

            Config.UpdateConfig(current_mode, default_mode);

            config.mode = current_mode;

            Console.WriteLine("Parsed configuration: " + JsonConvert.SerializeObject(config));
            return config;
        }
    }
}