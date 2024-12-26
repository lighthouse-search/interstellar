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
        public static async Task loop(Objects.Process_item[] processes, string video_id, string file_type) {
            foreach (var command in processes)
            {
                string arguments = string.Join(" ", command.commands).Replace("%video_id%", video_id).Replace("%file_type%", file_type);
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

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine(e.Data);
                    }
                };

                process.Start();
                process.WaitForExit();

                Thread.Sleep(400);
            }
        }
    }
}