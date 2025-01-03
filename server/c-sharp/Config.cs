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
        private static void Fallback_to_default_values(object current_mode, object default_mode)
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
                    Fallback_to_default_values(current_value, default_value);
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
            Objects.Config_mode default_mode = config.modes["default"];

            if (mode != null) {
                if (!config.modes.ContainsKey(mode)) {
                    throw new Exception("Missing 'default configuration");
                }
                Objects.Config_mode current_mode = config.modes[mode];
                if (current_mode.use_default == true) {
                    Config.Fallback_to_default_values(current_mode, default_mode);
                }
                config.mode = current_mode;
            } else {
                config.mode = default_mode;
            }

            if (config.mode.storage_reference != null) {
                config.mode.storage = config.storage[config.mode.storage_reference.id];
            }
            if (config.mode.authentication_reference != null) {
                config.mode.authentication = config.authentication[config.mode.authentication_reference.id];
            }
            if (config.mode.database_reference != null) {
                config.mode.database = config.database[config.mode.database_reference.id];
            }

            Console.WriteLine("Parsed configuration: " + JsonConvert.SerializeObject(config));
            return config;
        }

        public static List<Objects.Config_video_limit_issues> compare_keys_video_limits(PropertyInfo property, Objects.Config config, Func<object, object> item_class, object stream_data, string type, string[] flags) {
            if (type != "stream" && type != "format") {
                throw new Exception("type must be stream/format.");
            }

            List<Objects.Config_video_limit_issues> limit_issues = new List<Objects.Config_video_limit_issues>();

            object video_limits_min = null;
            if (config.mode.video_limits.min != null)
            {
                video_limits_min = type == "stream" ? config.mode.video_limits.min.stream : config.mode.video_limits.min.format;
            }
            object video_limits_max = null;
            if (config.mode.video_limits.max != null)
            {
                video_limits_max = type == "stream" ? config.mode.video_limits.max.stream : config.mode.video_limits.max.format;
            }

            // TODO: Double check this works.
            object video_limits_must_be = null;
            if (config.mode.video_limits.must != null)
            {
                video_limits_must_be = type == "stream" ? config.mode.video_limits.must.stream : config.mode.video_limits.must.format;
            }
            object video_limits_must_not_be = null;
            if (config.mode.video_limits.must_not_be != null)
            {
                video_limits_must_not_be = type == "stream" ? config.mode.video_limits.must_not_be.stream : config.mode.video_limits.must_not_be.format;
            }

            object video_limits_includes = null;
            if (config.mode.video_limits.includes != null)
            {
                video_limits_includes = type == "stream" ? config.mode.video_limits.includes.stream : config.mode.video_limits.includes.format;
            }
            object video_limits_does_not_include = null;
            if (config.mode.video_limits.does_not_include != null)
            {
                video_limits_does_not_include = type == "stream" ? config.mode.video_limits.does_not_include.stream : config.mode.video_limits.does_not_include.format;
            }

            if (config.mode.video_limits.min != null && video_limits_min != null) {
                // Check if values are too small.
                Validation.compare_value_between_objects_in_a_class((enforced_value, client_value) => {
                    if (flags.Contains("video_min_max_float")) {
                        if (enforced_value is string enforcedString)
                        {
                            enforced_value = Variable_operations.from_string_to_float(enforcedString);
                        }
                        if (client_value is string clientString)
                        {
                            client_value = Variable_operations.from_string_to_float(clientString);
                        }
                    }
                    var clientV = client_value switch
                    {
                        float f => f,
                        int i => i,
                        _ => throw new InvalidOperationException("Unexpected type")
                    };
                    var enforcedV = enforced_value switch
                    {
                        float f => f,
                        int i => i,
                        _ => throw new InvalidOperationException("Unexpected type")
                    };

                    if (clientV != -1 == false && clientV < enforcedV)
                    {
                        limit_issues.Add(new Objects.Config_video_limit_issues {
                            problem = "too_small",
                            prop = property.Name.ToLower(),
                            needs_to_be = enforcedV,
                            value_is = clientV
                        });
                    }
                }, property, item_class(config.mode.video_limits.min.stream), stream_data);
            }

            if (config.mode.video_limits.max != null && video_limits_max != null) {
                // Check if values are too large.
                Validation.compare_value_between_objects_in_a_class((enforced_value, client_value) => {
                    if (flags.Contains("video_min_max_float")) {
                        if (enforced_value is string enforcedString)
                        {
                            enforced_value = Variable_operations.from_string_to_float(enforcedString);
                        }
                        if (client_value is string clientString)
                        {
                            client_value = Variable_operations.from_string_to_float(clientString);
                        }
                    }
                    var clientV = client_value switch
                    {
                        float f => f,
                        int i => i,
                        _ => throw new InvalidOperationException("Unexpected type")
                    };
                    var enforcedV = enforced_value switch
                    {
                        float f => f,
                        int i => i,
                        _ => throw new InvalidOperationException("Unexpected type")
                    };

                    if (clientV != -1 && clientV > enforcedV)
                    {
                        limit_issues.Add(new Objects.Config_video_limit_issues {
                            problem = "too_large",
                            prop = property.Name.ToLower(),
                            needs_to_be = enforcedV,
                            value_is = clientV
                        });
                    }
                }, property, item_class(video_limits_max), stream_data);
            }

            if (config.mode.video_limits.must != null && video_limits_must_be != null) {
                // must_be - Value must be inarray.
                Validation.compare_value_between_objects_in_a_class((enforced_value, client_value) => {
                    if (enforced_value is List<string> enforced_array && client_value is String client_string)
                    {
                        if (enforced_array.Contains(client_string) == false) // Value is not on the allow-list.
                        {
                            limit_issues.Add(new Objects.Config_video_limit_issues {
                                problem = "invalid_value",
                                prop = property.Name.ToLower(),
                                needs_to_be = enforced_array,
                                value_is = client_string
                            });
                        }
                    } else {
                        throw new Exception("enforced_value or client_value is not comparable (this is a wildly stupid error, probably not your fault, so create a bug report).");
                    }
                }, property, item_class(video_limits_must_be), stream_data);
            }

            if (config.mode.video_limits.must_not_be != null && video_limits_must_not_be != null) {
                // must_not_be - Value must not be in array.
                Validation.compare_value_between_objects_in_a_class((enforced_value, client_value) => {
                    if (enforced_value is List<string> enforced_array && client_value is String client_string)
                    {
                        if (enforced_array.Contains(client_string) == true) // block-listed value was provided.
                        {
                            limit_issues.Add(new Objects.Config_video_limit_issues {
                                problem = "invalid_value",
                                prop = property.Name.ToLower(),
                                cannot_be = enforced_array,
                                value_is = client_string
                            });
                        }
                    } else {
                        throw new Exception("enforced_value or client_value is not comparable (this is a wildly stupid error, probably not your fault, so create a bug report).");
                    }
                }, property, item_class(video_limits_must_not_be), stream_data);
            }

            if (config.mode.video_limits.includes != null && video_limits_includes != null) {
                // includes - Value must include string from array.
                Validation.compare_value_between_objects_in_a_class((enforced_value, client_value) => {
                    if (enforced_value is List<string> enforced_array && client_value is String client_string)
                    {
                        if (Validation.array_includes(client_string, enforced_array) == false)
                        {
                            limit_issues.Add(new Objects.Config_video_limit_issues {
                                problem = "invalid_value",
                                prop = property.Name.ToLower(),
                                must_include = enforced_array,
                                value_is = client_string
                            });
                        }
                    } else {
                        throw new Exception("enforced_value or client_value is not comparable (this is a wildly stupid error, probably not your fault, so create a bug report).");
                    }
                }, property, item_class(video_limits_includes), stream_data);
            }

            if (config.mode.video_limits.does_not_include != null && video_limits_does_not_include != null) {
                // does not include - Value does not include string from array.
                Validation.compare_value_between_objects_in_a_class((enforced_value, client_value) => {
                    if (enforced_value is List<string> enforced_array && client_value is String client_string)
                    {
                        if (Validation.array_includes(client_string, enforced_array) == true)
                        {
                            limit_issues.Add(new Objects.Config_video_limit_issues {
                                problem = "invalid_value",
                                prop = property.Name.ToLower(),
                                cannot_be = enforced_array,
                                value_is = client_string
                            });
                        }
                    } else {
                        throw new Exception("enforced_value or client_value is not comparable (this is a wildly stupid error, probably not your fault, so create a bug report).");
                    }
                }, property, item_class(video_limits_does_not_include), stream_data);
            }

            return limit_issues;
        }
    }
}