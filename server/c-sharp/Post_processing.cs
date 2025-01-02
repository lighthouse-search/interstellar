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
    public class Post_processing
    {
        public static async Task initalise(HttpContext context, Objects.Config config, string asset_id, string file_type) {
            Console.WriteLine("We're processing!");
            
            // TODO: Make this chunk merging into a dedicated function.
            for (int i = 0; i < Directory.GetFiles($"/tmp/user_ingest_before_reencoding/{asset_id}/chunks").Length; i++)
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
                        await sw.WriteLineAsync($"cat /tmp/user_ingest_before_reencoding/{asset_id}/chunks/{asset_id}_{current}.ingest_chunk >> /tmp/user_ingest_before_reencoding/{asset_id}/{asset_id}.{file_type}");
                        // await sw.WriteAsync("exit");
                        sw.Close();
                    }
                }

                await cmd.WaitForExitAsync();

                Console.WriteLine("DELETING");
                Console.WriteLine(current);
                // File.Delete($"/tmp/user_ingest_before_reencoding/{asset_id}/chunks/{asset_id}_{i}.txt");
            }
            
            if (config.mode.type == "video") {
                await Post_processing.video(context, config, asset_id, file_type);
            } else {
                Console.WriteLine("Mode type is not 'video'. No need to use FFmpeg. Continuing to storage.");
            }

            await Storage.initalise_move(context, config, asset_id, file_type, new string[] { $"/tmp/user_ingest_reencoded/{asset_id}" });

            // Remove uploaded data from disk.
            await Handler.clean_up(context, asset_id);
        }

        public static async Task video(HttpContext context, Objects.Config config, string asset_id, string file_type) {
            // TODO: Need to check file-type is allowed by config. Either doing this in processing here or doing it each time a chunk is uploaded.

            Thread.Sleep(100); // sleep to cool down files, some processes sometimes hold onto them extra longer....for some reason. This was a bug that happened very rarely in dev, but definitely not something we want in prod. 100ms is nothing to help ensure this doesn't happen.

            Process initCheck = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v quiet -print_format json -show_format -show_streams /tmp/user_ingest_before_reencoding/{asset_id}/{asset_id}.{file_type}",
                    // UseShellExecute = false,
                    RedirectStandardOutput = true,             
                    CreateNoWindow = true,
                    RedirectStandardError = true
                },
                // EnableRaisingEvents = true
            };

            initCheck.Start();
            initCheck.WaitForExit();

            if (initCheck.ExitCode != 0) {
                throw new Exception("FFprobe failed to run.");
            }

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
                // TODO: Audio stream is passed but audio-specific keys are not within object.
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

            if (config.mode.ffmpeg.commands.reencode != null) {
                await Processes.loop(config.mode.ffmpeg.commands.reencode, asset_id, file_type, context);
            }
            Task.Delay(400);
            if (config.mode.ffmpeg.commands.thumbnail != null) {
                await Processes.loop(config.mode.ffmpeg.commands.thumbnail, asset_id, file_type, context);
            }

            Console.WriteLine("Finished");
            return;
        }

        public static async Task Upload_directory(Objects.Config config, string[] base_paths, string asset_id)
        {
            string[] files_list = File_operations.Get_files_in_directory(base_paths, true);

            Objects.presignedUrlAuthorization get_upload_urls = await Remote_server.Get_upload_urls(config, files_list, base_paths, asset_id);
            Objects.presignedUrlFilesArray[] files = get_upload_urls.files;

            foreach (Objects.presignedUrlFilesArray element in files)
            {
                string path = $"/tmp/user_ingest_reencoded/{asset_id}/{element.file}";
                Console.WriteLine($"Moving to storage...: {path}");
                byte[] filedata = await File.ReadAllBytesAsync(path);
                
                var wc = new WebClient();
                wc.UploadData(element.url, "PUT", filedata);
                Console.WriteLine($"Moved to storage: {path}");
            }
        }
    }
}