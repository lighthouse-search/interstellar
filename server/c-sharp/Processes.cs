using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;   
using System.Text;
using System.Net;
using Newtonsoft.Json;
using Interstellar;

namespace Interstellar
{
    public class Processes
    {
        public static async Task<List<string>> loop(List<Objects.Process_item> processes, Objects.Config config, string asset_id, string file_type, string section, HttpContext context) {
            if (processes.Count == 0) {
                throw new Exception("processes.Length is 0");
            }

            List<string> outputs = new List<string>();
            foreach (var command in processes)
            {
                // Set vague status that we're running post processing commands.
                await Status.update(config, asset_id, "processing", section, processes.IndexOf(command), processes.Count, null);

                string arguments = string.Join(" ", command.commands).Replace("%asset_id%", asset_id).Replace("%file_type%", file_type);
                Console.WriteLine("Running command: " + "ffmpeg " + arguments);

                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command.program,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    },
                    EnableRaisingEvents = true
                };

                // StringBuilder output = new StringBuilder("");

                // process.OutputDataReceived += (sender, e) =>
                // {
                //     if (!string.IsNullOrEmpty(e.Data))
                //     {
                //         // Console.WriteLine(e.Data);
                //         output.AppendLine(e.Data);
                //     }
                // };

                // process.ErrorDataReceived += (sender, e) =>
                // {
                //     if (!string.IsNullOrEmpty(e.Data))
                //     {
                //         // Console.WriteLine(e.Data);
                //     }
                // };

                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0) {
                    throw new Exception("Process failed to run.");
                }

                string line = process.StandardOutput.ReadToEnd();

                if (command.handoff != null) {
                    // If this fails, an error throws. As you can see, there I drafted an exception handler to communicate the error directly to the client, but the code was refactored to close the connection when processing starts, so it's useless. TODO: Add a status endpoint call here that sends the message from the server.
                    await handoff_process_output(null, line, command.handoff);
                    
                    // } catch (Exception e) {
                    //     Objects.HTTPS_Request_Response error_data = JsonConvert.DeserializeObject<Objects.HTTPS_Request_Response>(e.Message);
                    //     Objects.errorD returned_error = JsonConvert.DeserializeObject<Objects.errorD>(error_data.response_body);
                    //     if (context != null && error_data.response_body != null && returned_error != null) {
                    //         context.Response.StatusCode = 400;
                    //         await context.Response.WriteAsJsonAsync(Responses.error($"External server returned an error: {returned_error.message}"));
                    //         throw;
                    //     } else {
                    //         context.Response.StatusCode = 400;
                    //         await context.Response.WriteAsJsonAsync(Responses.error($"External server returned an error, cannot continue."));
                    //         throw;
                    //     }
                    // }
                }

                outputs.Add(line);
            }

            return outputs;
        }

        public static async Task<bool> handoff_process_output(Process process, string manual_output, Objects.Process_item_handoff handoff) {
            string line = process != null ? process.StandardOutput.ReadToEnd() : manual_output.ToString();

            Objects.HTTPS_Request_Response handoff_request = await Https.Request_POST(
                handoff.endpoint,
                line
            );

            if (handoff_request.status_code != HttpStatusCode.OK) {
                throw new Exception("Didn't get 200 status code.");
            }

            // It's very important that if this function fails, it throws an exception.
            return true;
        }
    }
}