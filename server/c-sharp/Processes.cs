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
        public static async Task loop(Objects.Process_item[] processes, string asset_id, string file_type, HttpContext context) {
            foreach (var command in processes)
            {
                string arguments = string.Join(" ", command.commands).Replace("%asset_id%", asset_id).Replace("%file_type%", file_type);
                Console.WriteLine("Running command: " + "ffmpeg " + arguments);

                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command.program,
                        Arguments = arguments,
                        CreateNoWindow = true,
                    },
                    EnableRaisingEvents = true
                };

                StringBuilder output = new StringBuilder("");

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // Console.WriteLine(e.Data);
                        output.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // Console.WriteLine(e.Data);
                    }
                };

                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0) {
                    throw new Exception("Process failed to run.");
                }

                if (command.handoff != null) {
                    // try {
                        // await handoff_process_output(null, output, command.handoff);
                    // } catch (Exception e) {
                    //     Objects.HTTPS_Request_Response error_data = JsonConvert.DeserializeObject<Objects.HTTPS_Request_Response>(e.Message);
                    //     Objects.errorD returned_error = JsonConvert.DeserializeObject<Objects.errorD>(error_data.response_body);
                    //     if (context != null && error_data.response_body != null && returned_error != null) {
                    //         context.Response.StatusCode = 400;
                    //         await context.Response.WriteAsJsonAsync(Responses.error($"External server returned an error: {returned_error.message}"));
                    //         return;
                    //     } else {
                    //         context.Response.StatusCode = 400;
                    //         await context.Response.WriteAsJsonAsync(Responses.error($"External server returned an error, cannot continue."));
                    //         return;
                    //     }
                    // }
                }
            }
        }

        public static async Task<bool> handoff_process_output(Process process, StringBuilder manual_output, Objects.Process_item_handoff handoff) {
            string line = process != null ? process.StandardOutput.ReadToEnd() : manual_output.ToString();

            Objects.HTTPS_Request_Response handoff_request = await Https.Request_POST(
                handoff.endpoint,
                line
            );

            // It's very important that if this function fails, it throws an exception.
            return true;
        }
    }
}