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
    public class Processing
    {
        public static async Task video(HttpContext context, string video_id, string file_type) {
            Objects.Config config = Config.Get("test");

            // TODO: Need to check file-type is allowed by config. Either doing this in processing here or doing it each time a chunk is uploaded.

            Thread.Sleep(100); // sleep to cool down files, some processes sometimes hold onto them extra longer....for some reason. This was a bug that happened very rarely in dev, but definitely not something we want in prod. 100ms is nothing to help ensure this doesn't happen.

            for (int i = 0; i < Directory.GetFiles($"/tmp/user_ingest_before_reencoding/{video_id}/chunks").Length; i++)
            {
                int current = i;
                Process cmd = new Process();

                cmd.StartInfo.FileName = "bash";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.UseShellExecute = false;

                cmd.Start();

                await using (StreamWriter sw = cmd.StandardInput)
                {
                    if (sw.BaseStream.CanWrite)
                    {
                        // await sw.WriteLineAsync("cd /usr/bin");
                        await sw.WriteLineAsync($"cat /tmp/user_ingest_before_reencoding/{video_id}/chunks/{video_id}_{current}.ingest_chunk >> /tmp/user_ingest_before_reencoding/{video_id}/{video_id}.{file_type}");
                        // await sw.WriteAsync("exit");
                        sw.Close();
                    }
                }

                await cmd.WaitForExitAsync();

                Console.WriteLine("DELETING");
                Console.WriteLine(current);
                // File.Delete($"/tmp/user_ingest_before_reencoding/{video_id}/chunks/{video_id}_{i}.txt");
            }

            Process initCheck = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v quiet -print_format json -show_format -show_streams /tmp/user_ingest_before_reencoding/{video_id}/{video_id}.{file_type}",
                    // UseShellExecute = false,
                    RedirectStandardOutput = true,             
                    CreateNoWindow = true,
                    RedirectStandardError = true
                },
                // EnableRaisingEvents = true
            };

            initCheck.Start();
            initCheck.WaitForExit();

            string line = initCheck.StandardOutput.ReadToEnd();
            Console.WriteLine(line);

            Objects.ffprobevideoinfo process_output = JsonConvert.DeserializeObject<Objects.ffprobevideoinfo>(line);
            if (process_output.streams == null) {
                throw new Exception("We couldn't identify critical metadata about your video (is your video corrupt?)");
            }

            List<Objects.Config_video_limit_issues> limit_issues = new List<Objects.Config_video_limit_issues>();

            foreach (var stream_data in process_output.streams)
            {
                if (stream_data.codec_type == "video")
                {
                    foreach (PropertyInfo property in stream_data.GetType().GetProperties())
                    {
                        if (config.mode.video_limits.min != null && config.mode.video_limits.min.stream != null) {
                            // Check if values are too small.
                            Validation.compare_value_between_objects_in_a_class((enforced_value, client_value) => {
                                if (enforced_value is int enforcedFloat && client_value is int clientInt)
                                {
                                    if (clientInt != -1 == false && clientInt < enforcedFloat)
                                    {
                                        limit_issues.Add(new Objects.Config_video_limit_issues {
                                            problem = "too_small",
                                            prop = property.Name.ToLower(),
                                            needs_to_be = enforcedFloat,
                                            value_is = clientInt
                                        });
                                    }
                                } else {
                                    throw new Exception("enforced_value or client_value is not comparable (this is a wildly stupid error, probably not your fault, so create a bug report).");
                                }
                            }, property, JsonConvert.DeserializeObject<Objects.ffprobevideoinfostream>(JsonConvert.SerializeObject(config.mode.video_limits.min.stream)), stream_data);
                        }

                        if (config.mode.video_limits.max != null && config.mode.video_limits.max.stream != null) {
                            // Check if values are too large.
                            Validation.compare_value_between_objects_in_a_class((enforced_value, client_value) => {
                                if (enforced_value is int enforcedFloat && client_value is int clientInt)
                                {
                                    if (clientInt != -1 && clientInt > enforcedFloat)
                                    {
                                        limit_issues.Add(new Objects.Config_video_limit_issues {
                                            problem = "too_large",
                                            prop = property.Name.ToLower(),
                                            needs_to_be = enforcedFloat,
                                            value_is = client_value
                                        });
                                    }
                                } else {
                                    throw new Exception("enforced_value or client_value is not comparable (this is a wildly stupid error, probably not your fault, so create a bug report).");
                                }
                            }, property, JsonConvert.DeserializeObject<Objects.ffprobevideoinfostream>(JsonConvert.SerializeObject(config.mode.video_limits.max.stream)), stream_data);
                        }

                        if (config.mode.video_limits.must != null && config.mode.video_limits.must.stream != null) {
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
                            }, property, JsonConvert.DeserializeObject<Objects.ffprobevideoinfostream>(JsonConvert.SerializeObject(config.mode.video_limits.must.stream)), stream_data);
                        }

                        if (config.mode.video_limits.must_not_be != null && config.mode.video_limits.must_not_be.stream != null) {
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
                            }, property, JsonConvert.DeserializeObject<Objects.ffprobevideoinfostream>(JsonConvert.SerializeObject(config.mode.video_limits.must_not_be.stream)), stream_data);
                        }

                        if (config.mode.video_limits.includes != null && config.mode.video_limits.includes.stream != null) {
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
                            }, property, JsonConvert.DeserializeObject<Objects.ffprobevideoinfostream>(JsonConvert.SerializeObject(config.mode.video_limits.includes.stream)), stream_data);
                        }

                        if (config.mode.video_limits.does_not_include != null && config.mode.video_limits.does_not_include.stream != null) {
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
                            }, property, JsonConvert.DeserializeObject<Objects.ffprobevideoinfostream>(JsonConvert.SerializeObject(config.mode.video_limits.does_not_include.stream)), stream_data);
                        }
                    }
                }
            }

            Objects.Config_mode_video_limits_min_max_format video_limits_min_max_format = JsonConvert.DeserializeObject<Objects.Config_mode_video_limits_min_max_format>(JsonConvert.SerializeObject(process_output.format));
            
            foreach (PropertyInfo property in video_limits_min_max_format.GetType().GetProperties())
            {
                if (config.mode.video_limits.min != null && config.mode.video_limits.min.format != null) {
                    // Check if values are too small.
                    Validation.compare_value_between_objects_in_a_class((enforced_value, client_value) => {
                        if (enforced_value is string enforcedString)
                        {
                            if (float.TryParse(enforcedString, out float parsedEnforcedFloat))
                            {
                                enforced_value = parsedEnforcedFloat;
                            } else
                            {
                                throw new Exception("enforced_value is a string but cannot be parsed as a float.");
                            }
                        }
                        if (client_value is string clientString)
                        {
                            if (float.TryParse(clientString, out float parsedClientFloat))
                            {
                                client_value = parsedClientFloat;
                            }
                            else
                            {
                                throw new Exception("client_value is a string but cannot be parsed as a float.");
                            }
                        }
                        if ((enforced_value is float enforcedFloat || enforced_value is int enforcedInt) && (client_value is float clientFloat || client_value is int clientInt))
                        {
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
                            if (clientV != -1 && clientV < enforcedV)
                            {
                                limit_issues.Add(new Objects.Config_video_limit_issues {
                                    problem = "too_small",
                                    prop = property.Name.ToLower(),
                                    needs_to_be = enforcedV,
                                    value_is = clientV
                                });
                            }
                        } else {
                            throw new Exception("enforced_value or client_value is not comparable (this is a wildly stupid error, probably not your fault, so create a bug report).");
                        }
                    }, property, JsonConvert.DeserializeObject<Objects.Config_mode_video_limits_min_max_format>(JsonConvert.SerializeObject(config.mode.video_limits.min.format)), video_limits_min_max_format);
                }

                if (config.mode.video_limits.max != null && config.mode.video_limits.max.format != null) {
                    // Check if values are too large.
                    Validation.compare_value_between_objects_in_a_class((enforced_value, client_value) => {
                        if (enforced_value is string enforcedString)
                        {
                            if (float.TryParse(enforcedString, out float parsedEnforcedFloat))
                            {
                                enforced_value = parsedEnforcedFloat;
                            }
                            else
                            {
                                throw new Exception("client_value is a string but cannot be parsed as a float.");
                            }
                        }
                        if (client_value is string clientString)
                        {
                            if (float.TryParse(clientString, out float parsedClientFloat))
                            {
                                client_value = parsedClientFloat;
                            }
                            else
                            {
                                throw new Exception("client_value is a string but cannot be parsed as a float.");
                            }
                        }
                        Console.WriteLine($"{property.Name}");
                        if ((enforced_value is float enforcedFloat || enforced_value is int enforcedInt) && (client_value is float clientFloat || client_value is int clientInt))
                        {
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
                            Console.WriteLine($"clientV {property.Name} client: {clientV} enforced: {enforcedV}");
                            if (clientV != -1 && clientV > enforcedV)
                            {
                                limit_issues.Add(new Objects.Config_video_limit_issues {
                                    problem = "too_large",
                                    prop = property.Name.ToLower(),
                                    needs_to_be = enforcedV,
                                    value_is = clientV
                                });
                            }
                        } else {
                            throw new Exception("enforced_value or client_value is not comparable (this is a wildly stupid error, probably not your fault, so create a bug report).");
                        }
                    }, property, JsonConvert.DeserializeObject<Objects.Config_mode_video_limits_min_max_format>(JsonConvert.SerializeObject(config.mode.video_limits.max.format)), video_limits_min_max_format);
                }

                if (config.mode.video_limits.must != null && config.mode.video_limits.must.format != null) {
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
                    }, property, JsonConvert.DeserializeObject<Objects.Config_mode_video_limits_min_max_format>(JsonConvert.SerializeObject(config.mode.video_limits.must.format)), video_limits_min_max_format);
                }

                if (config.mode.video_limits.must_not_be != null && config.mode.video_limits.must_not_be.format != null) {
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
                    }, property, JsonConvert.DeserializeObject<Objects.Config_mode_video_limits_min_max_format>(JsonConvert.SerializeObject(config.mode.video_limits.must_not_be.format)), video_limits_min_max_format);
                }

                if (config.mode.video_limits.includes != null&& config.mode.video_limits.includes.format != null) {
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
                    }, property, JsonConvert.DeserializeObject<Objects.Config_mode_video_limits_min_max_format>(JsonConvert.SerializeObject(config.mode.video_limits.includes.format)), video_limits_min_max_format);
                }

                if (config.mode.video_limits.does_not_include != null && config.mode.video_limits.does_not_include.format != null) {
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
                    }, property, JsonConvert.DeserializeObject<Objects.Config_mode_video_limits_min_max_format>(JsonConvert.SerializeObject(config.mode.video_limits.does_not_include.format)), video_limits_min_max_format);
                }
            }

            if (limit_issues.Count > 0) {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(Responses.error($"Your video doesn't meet the server's requirements.", limit_issues));
                await context.Response.CompleteAsync();
                return;
            }

            throw new Exception("catch");
            
            System.IO.Directory.CreateDirectory($"/tmp/user_ingest_reencoded/{video_id}");

            await Processes.loop(config.mode.ffmpeg.commands.reencode, video_id, file_type);
            Task.Delay(400);
            await Processes.loop(config.mode.ffmpeg.commands.thumbnail, video_id, file_type);

            File_operations.DeleteDirectory($"/tmp/user_ingest_before_reencoding/{video_id}/");    

            string[] paths = new string[] { $"/tmp/user_ingest_reencoded/{video_id}" };
            string[] filesList = new string [] {};
            var preFilesList = filesList.ToList();

            foreach(string path in paths)
            {
                if (File.Exists(path))
                {
                    // This path is a file
                    File_operations.ProcessFile(path, preFilesList, video_id);
                }
                else if (Directory.Exists(path))
                {
                    // This path is a directory
                    File_operations.ProcessDirectory(path, preFilesList, video_id);
                }
                else
                {
                    Console.WriteLine("{0} is not a valid file or directory.", path);
                }
            }

            filesList = preFilesList.ToArray();

            WebRequest presignedUrl_request = WebRequest.Create(config.authorisation.get_upload_urls);
            // Set the Method property of the request to POST.
            presignedUrl_request.Method = "POST";
            presignedUrl_request.ContentType = "application/json";

            Objects.presignedUrlsBody presignedurlAuthBodyV = new Objects.presignedUrlsBody {
                video_id = video_id,
                files = filesList,
            };

            string presignedurlAuthBody = JsonConvert.SerializeObject(presignedurlAuthBodyV);

            var presignedurlAuthBodyBytes = Encoding.Default.GetBytes(presignedurlAuthBody);

            Stream presignedUrlnewStream = presignedUrl_request.GetRequestStream();
            presignedUrlnewStream.Write(presignedurlAuthBodyBytes, 0, presignedurlAuthBodyBytes.Length);
            presignedUrlnewStream.Close();

            WebResponse presignedUrlresponse = presignedUrl_request.GetResponse();

            string presignedurl_DANGEROUS_chunk = null;
            
            using (Stream request_stream = presignedUrlresponse.GetResponseStream())
            {
                // Open the stream using a StreamReader for easy access.
                StreamReader reader = new StreamReader(request_stream);
                // Read the content.
                string responseFromServer = reader.ReadToEnd();
                // Display the content.
                if (!String.IsNullOrWhiteSpace(responseFromServer)) {
                    presignedurl_DANGEROUS_chunk = responseFromServer;
                    Console.WriteLine("RESPONSE FROM SERVER:");
                    Console.WriteLine(responseFromServer);
                } // chunkAuthorization
            }

            presignedUrlresponse.Close();

            Objects.presignedUrlAuthorization presignedurlAuthorizationOutput = JsonConvert.DeserializeObject<Objects.presignedUrlAuthorization>(presignedurl_DANGEROUS_chunk);
            Objects.presignedUrlFilesArray[] files = presignedurlAuthorizationOutput.files;
            Console.WriteLine("OUTPUT");
            Console.WriteLine(presignedurlAuthorizationOutput);
            if (presignedurlAuthorizationOutput.ok != true) {
                // TODO: This error shouldn't return to the client.
                throw new Exception("Presigned URLs error");
            }

            foreach (Objects.presignedUrlFilesArray element in files)
            {
                Console.WriteLine($"curl1 '{element.url}' --upload-file /tmp/user_ingest_reencoded/{video_id}/{element.file}");

                byte[] filedata = await File.ReadAllBytesAsync($"/tmp/user_ingest_reencoded/{video_id}/{element.file}");
                
                var wc = new WebClient();
                wc.UploadData(element.url, "PUT", filedata);
            }

            // await db.KeyDeleteAsync($"{Security.redis_key_sanitize(video_id)}_alreadyuploaded");
            // await db.KeyDeleteAsync($"{Security.redis_key_sanitize(video_id)}_maxsize");

            File_operations.DeleteDirectory($"/tmp/user_ingest_reencoded/{video_id}");

            Console.WriteLine("Finished");
            return;
        }
    }
}