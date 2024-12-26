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
                        limit_issues = limit_issues.Concat(Config.compare_keys_video_limits(property, config, ((object value) => { return JsonConvert.DeserializeObject<Objects.ffprobevideoinfostream>(JsonConvert.SerializeObject(value)); }), stream_data, "stream", [])).ToList();
                    }
                }
            }

            foreach (var stream_data in process_output.streams)
            {
                if (stream_data.codec_type == "audio")
                {
                    foreach (PropertyInfo property in stream_data.GetType().GetProperties())
                    {
                        limit_issues = limit_issues.Concat(Config.compare_keys_video_limits(property, config, ((object value) => { return JsonConvert.DeserializeObject<Objects.ffprobevideoinfostream>(JsonConvert.SerializeObject(value)); }), stream_data, "stream", [])).ToList();
                    }
                }
            }

            Objects.Config_mode_video_limits_min_max_format video_limits_min_max_format = JsonConvert.DeserializeObject<Objects.Config_mode_video_limits_min_max_format>(JsonConvert.SerializeObject(process_output.format));
            
            foreach (PropertyInfo property in video_limits_min_max_format.GetType().GetProperties())
            {
                var output = Config.compare_keys_video_limits(property, config, ((object value) => { return JsonConvert.DeserializeObject<Objects.Config_mode_video_limits_min_max_format>(JsonConvert.SerializeObject(value)); }), video_limits_min_max_format, "format", ["video_min_max_float"]);
                limit_issues = limit_issues.Concat(output).ToList();
            }

            // TODO: check must_be, must_not_be, includes, does_not_include in ffmpeg format check.

            if (limit_issues.Count > 0) {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(Responses.error($"Your video doesn't meet the server's requirements.", limit_issues));
                await context.Response.CompleteAsync();
                return;
            }

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