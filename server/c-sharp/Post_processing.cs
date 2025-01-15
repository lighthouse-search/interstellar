using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;   
using System.Text;
using Newtonsoft.Json;
using Tomlyn;
using Tomlyn.Model;
using System.Reflection;

namespace Interstellar
{
    public class Post_processing
    {
        public static async Task initalise(HttpContext context, Objects.Config config, string asset_id, string file_type) {
            Console.WriteLine("We're processing!");

            // Update status as post-processing.
            await Status.update(config, asset_id, "processing", "collecting_files", -1, -1, null);
            
            if (config.mode.features.chunks.save_to_disk == false) {
                // Chunks aren't even saved to the disk, this is most likely a demo instance. We'll skip all chunk related logic.
               
                // TODO: Make this chunk merging into a dedicated function.
                for (int i = 0; i < Directory.GetFiles($"/tmp/user_ingest_before_reencoding/{asset_id}/chunks").Length; i++)
                {
                    int current = i;
                    Process cmd = new Process();

                    cmd.StartInfo.FileName = "bash";
                    cmd.StartInfo.RedirectStandardInput = true;
                    cmd.StartInfo.UseShellExecute = false;

                    cmd.Start();

                    // TODO: This could be multithreaded by doing e.g. 1-40 chunks, 40-80 chunks, 180-120 chunks and then adding those outputs together to reconstruct this file much faster.
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

                Thread.Sleep(100); // sleep to cool down files, some processes sometimes hold onto them extra longer....for some reason. This was a bug that happened very rarely in dev, but definitely not something we want in prod. 100ms is nothing to help ensure this doesn't happen.
                
                if (config.mode.post_processing != null) {
                    if (config.mode.type == "video") {
                        await Post_processing.video(context, config, asset_id, file_type);
                    } else if (config.mode.type == "file") {
                        // There isn't any special logic for this right now, off to the next stage (storage).
                    } else {
                        Console.WriteLine("Mode type is not 'video'. No need to use FFmpeg. Continuing to storage.");
                    }

                    await Post_processing.commands_run_processing(context, config, asset_id, file_type);
                } else {
                    Console.WriteLine("Skipping config.mode.post_processing, unset.");
                }

                // Update status as moving to storage.
                await Status.update(config, asset_id, "moving_to_storage", null, -1, -1, null);

                if (config.mode.storage != null) {
                    // TODO: This uses user_ingest_reencoded, which is video exclusive. config.mode.type 'file' always fails. need to fix.
                    await Storage.initalise_move(context, config, asset_id, file_type, $"/tmp/user_ingest_reencoded/{asset_id}" );
                } else {
                    Console.WriteLine("Skipping config.mode.storage, unset.");
                }

                // Remove uploaded data from disk.
                await Handler.clean_up(context, asset_id);
            }
            
            // Let 'em know (that we finished the transfer and post-processing).
            await Status.update(config, asset_id, "uploaded", null, -1, -1, null);

            Console.WriteLine("Finished.");
        }

        public static async Task video(HttpContext context, Objects.Config config, string asset_id, string file_type) {
            if (config.mode.post_processing.commands != null && config.mode.post_processing.commands.validation != null) {
                Console.WriteLine("Skipping Processing.video, config.mode.post_processing.commands.validation not specified.");
                return;
            }

            List<string> validation_commands = await Processes.loop(config.mode.post_processing.commands.validation, config, asset_id, file_type, "validation", context);
            string ffprobe_output = validation_commands[validation_commands.Count-1];
            Console.WriteLine(ffprobe_output);

            Objects.ffprobevideoinfo process_output = JsonConvert.DeserializeObject<Objects.ffprobevideoinfo>(ffprobe_output);
            
            if (process_output == null || process_output.streams == null) {
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

            if (limit_issues.Count > 0) {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(Responses.error($"Your video doesn't meet the server's requirements.", limit_issues));
                await context.Response.CompleteAsync();
                return;
            }
        }

        public static async Task commands_run_processing(HttpContext context, Objects.Config config, string asset_id, string file_type) {
            // TODO: If no commands then we need to move the files to user_ingest_reencoded or some other directory so they actually upload.
            if (config.mode.post_processing != null && config.mode.post_processing.commands != null) {
                if (config.mode.post_processing.commands.processing != null) {
                    await Processes.loop(config.mode.post_processing.commands.processing, config, asset_id, file_type, "reencode", context);
                }
            }
        }
    }
}