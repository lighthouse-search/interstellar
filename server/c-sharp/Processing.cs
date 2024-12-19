using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;   
using System.Text;
using System.Net;
using Newtonsoft.Json;

namespace Interstellar
{
    public class Processing
    {
        public static async Task video(string video_id, string file_type) {
            // context.Response.StatusCode = 200;
            // await context.Response.WriteAsJsonAsync(new Objects.errorD 
            // {
            //     type = "processing"
            // });
            // await context.Response.CompleteAsync();

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

            Objects.ffprobevideoinfo processOutput = JsonConvert.DeserializeObject<Objects.ffprobevideoinfo>(line);
            if (processOutput.streams == null) {
                throw new Exception("We couldn't identify critical metadata about your video (is your video corrupt?)");
            }

            Objects.ffprobevideoinfostream streamData = null;

            if (processOutput.streams.Length > 10000) {
                throw new Exception($"Too many streams ({processOutput.streams.Length})...how do you even have this many in a single video?");
            }

            bool alreadyFoundVideoStream = false;

            foreach (var stream in processOutput.streams)
            {
                // Check if the codec_type is "video"
                if (stream.codec_type == "video")
                {
                    if (alreadyFoundVideoStream == true) {
                        throw new Exception("Cannot have more than 1 video stream in uploaded file.");
                    }
                    alreadyFoundVideoStream = true;
                    // Found the first video stream
                    Console.WriteLine("Found a video stream!");
                    streamData = stream;
                }
            }

            if (streamData == null) {
                throw new Exception("No stream was found in the provided video.");
            }

            Console.WriteLine("VID STREAM");
            Console.WriteLine(streamData);
            
            if (streamData.width > 3840) {
                // too much width (over 4k);
                throw new Exception("Width cannot exceed more than 3840 pixels (maximum file resolution is 4k).");
            }
            if (streamData.height > 2160) {
                // too much height (over 4k);
                throw new Exception("Height cannot exceed more than 2160 pixels (maximum file resolution is 4k).");
            }
            if (streamData.width < 256) {
                // too little (under 160p);
                throw new Exception("Width cannot be less than 256 pixels (the minimum file resolution is 144p).");
            }
            if (streamData.height < 144) {
                // too little (under 160p);
                throw new Exception("Height cannot be less than 144 pixels (the minimum file resolution is 144p).");
            }

            if (processOutput.format.probe_score < 98) {
                // file is not trustworthy. Explain to user.
                throw new Exception($"Hmm...that file doesn't look like a .{file_type} file, we can't ingest it for security reasons. Please ensure it is not corrupt.");
            }

            string duration_input = processOutput.format.duration;
            int index = duration_input.IndexOf(".");
            if (index > 0)
                duration_input = duration_input.Substring(0, index);
            
            long duration = Convert.ToInt64(duration_input);

            Console.WriteLine("Duration");

            // TODO: All of this should be configurable in Interstellar config, which cleaner code.

            if (duration == 86400 && processOutput.format.duration.Contains(".")) {
                // Video over 24 hours. Cannot continue.
                throw new Exception("Your video is over 24 hours. Videos over 24 hours are not supported. Please adjust your video length.");
            }

            if (duration < 1) {
                // Video over 24 hours. Cannot continue.
                throw new Exception("Duration is under 1...what?");
            }

            if (duration > 86400) {
                // Video over 24 hours. Cannot continue.
                throw new Exception("Your video is over 24 hours. Videos over 24 hours are not supported. Please adjust your video length.");
            }

            if (Convert.ToInt64(processOutput.format.size) < 1000000) {
                // TODO: Minimum size should be configurable in Intersteallar config, and this is a stupid restriction.
                throw new Exception("Video cannot be under 1mb.");
            }

            if (Convert.ToInt64(processOutput.format.size) > 5000000000) {
                // TODO: Maximum size should be configurable in Intersteallar config.
                throw new Exception("Your video cannot be over 5GB, this is a temporary pre-beta condition.");
            }

            // if (Convert.ToInt64(processOutput.format.size) > 42949672960) {
            //     context.Response.StatusCode = 400;
            //     await context.Response.WriteAsJsonAsync(new Objects.errorD 
            //     {
            //         error = true,
            //         message = "Your video is over 40GB."
            //     });
            //     return;
            // }
            if (Convert.ToInt64(processOutput.format.bit_rate) > 600000000) {
                // TODO: This should be configurable in Interstellar config.
                throw new Exception("Bitrate cannot be over 600000000 bytes.");
            }

            Thread.Sleep(400);

            // TODO: SANITIZE THAT video_id AGAIN BUD.
            Process reencode = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i /tmp/user_ingest_before_reencoding/{video_id}/{video_id}.{file_type} -vcodec libx264 -maxrate 400k -crf 28 -filter:v fps=60 /tmp/user_ingest_before_reencoding/{video_id}/{video_id}.avi -y -preset medium",
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true
            };

            reencode.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(e.Data);
                }
            };

            reencode.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(e.Data);
                }
            };

            reencode.Start();
            reencode.WaitForExit();

            Thread.Sleep(400);

            System.IO.Directory.CreateDirectory($"/tmp/user_ingest_reencoded/{video_id}");

            Process reencodeToMpd = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i /tmp/user_ingest_before_reencoding/{video_id}/{video_id}.avi " +
                                "-vf \"scale=-2:144\" -c:v libx264 -crf 28 -profile:v baseline -b:v:0 250k " +
                                "-vf \"scale=-2:240\" -c:v libx264 -crf 28 -profile:v baseline -b:v:1 500k " +
                                "-vf \"scale=-2:360\" -c:v libx264 -crf 28 -profile:v main -b:v:2 750k " +
                                "-vf \"scale=-2:480\" -c:v libx264 -crf 28 -profile:v main -b:v:3 1000k " +
                                "-vf \"scale=-2:720\" -c:v libx264 -crf 28 -profile:v high -b:v:4 1500k " +
                                "-vf \"scale=-2:1080\" -c:v libx264 -crf 28 -profile:v high -b:v:5 2500k " +
                                $"-f dash /tmp/user_ingest_reencoded/{video_id}/video.mpd -y -preset medium",
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true
            };

            
            reencodeToMpd.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(e.Data);
                }
            };

            reencodeToMpd.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(e.Data);
                }
            };

            reencodeToMpd.Start();
            reencodeToMpd.WaitForExit();

            // Console.WriteLine("ANY MESSAGE? - SECOND CMD");
            // Console.WriteLine(reencodeToMpd.StandardOutput.ReadToEnd());
            // Console.WriteLine("ERROR - SECOND CMD:");
            // Console.WriteLine(reencodeToMpd.StandardError.ReadToEnd());
            Console.WriteLine("EXIT CODE - SECOND CMD!");
            Console.WriteLine(reencodeToMpd.ExitCode);

            Task.Delay(400);

            // ffmpeg -i input.mp4 -vf "select='not(mod(t,5))',setpts=N/FRAME_RATE/TB" output_%04d.jpg

            // string[] resolutionList = new string [] {};
            // var preResolutionList = resolutionList.ToList();

            // if (processOutput.streams[0].width > 192 && processOutput.streams[0].height > 144) {
            //     preResolutionList.Add("192:144");
            // }

            // if (processOutput.streams[0].width > 640 && processOutput.streams[0].height > 480) {
            //     preResolutionList.Add("640:480");
            // }

            // if (processOutput.streams[0].width > 1200 && processOutput.streams[0].height > 720) {
            //     preResolutionList.Add("1200:720");
            // }

            // if (processOutput.streams[0].width > 1920 && processOutput.streams[0].height > 1080) {
            //     preResolutionList.Add("1920:1080");
            // }

            // if (processOutput.streams[0].width > 3840 && processOutput.streams[0].height > 2160) {
            //     preResolutionList.Add("3840:2160");
            // }
            // resolutionList = preResolutionList.ToArray();

            // for (int i = 0; i < resolutionList.Length; i++)
            // {

            System.IO.Directory.CreateDirectory($"/tmp/user_ingest_reencoded/{video_id}/thumbnails");

            Process generateThumbnails = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i /tmp/user_ingest_before_reencoding/{video_id}/{video_id}.avi -preset veryfast -vf \"scale=-1:480\" /tmp/user_ingest_reencoded/{video_id}/auto_generated_thumbnail.jpeg",
                    // UseShellExecute = false,
                    // RedirectStandardOutput = true,                    
                    CreateNoWindow = true,
                    // RedirectStandardError = true
                },
                // EnableRaisingEvents = true
            };

            generateThumbnails.Start();
            generateThumbnails.WaitForExit();

            string directory = $"/tmp/user_ingest_reencoded/{video_id}/thumbnails/";
            List<string> imageUrls = new List<string>();

            string[] imagePreviews = Directory.GetFiles(directory, "*.jpeg");

            Console.WriteLine("IMAGE PREVIEWS!");
            Console.WriteLine(imagePreviews.Length);

            foreach (string image in imagePreviews)
            {
                // Read the image file
                byte[] imageBytes = File.ReadAllBytes(image);
                
                // Convert the image bytes to base64 URL
                string base64Url = Convert.ToBase64String(imageBytes);
                
                // Create the data URL
                string dataUrl = $"data:image/jpeg;base64,{base64Url}";
                
                // Add the data URL to the array
                imageUrls.Add(dataUrl);
            }

            // Convert the imageUrls list to JSON
            string json = JsonConvert.SerializeObject(imageUrls, Formatting.Indented);

            // Write the JSON to a file
            File.WriteAllText($"/tmp/user_ingest_reencoded/{video_id}/thumbnails.json", json);

            File_operations.DeleteDirectory($"/tmp/user_ingest_reencoded/{video_id}/thumbnails"); // Delet this so it doesn't contribute to S3 writes.

            Console.WriteLine("JSON file created successfully!");

            // Process getAudioFile = new Process
            // {
            //     StartInfo = new ProcessStartInfo
            //     {
            //         FileName = "ffmpeg",
            //         Arguments = $"-i /tmp/user_ingest_before_reencoding/{video_id}/{video_id}.avi -max_alloc 600000000 -acodec copy /tmp/user_ingest_before_reencoding/{video_id}/{video_id}.aac",
            //         // UseShellExecute = false,
            //         RedirectStandardOutput = true,
            //         CreateNoWindow = true,
            //         RedirectStandardError = true
            //     },
            //     // EnableRaisingEvents = true
            // };

            // getAudioFile.Start();
            // getAudioFile.WaitForExit();

            // string subtitles = await Helper.GetWhisperSubtitles($"/tmp/user_ingest_before_reencoding/{video_id}/{video_id}.aac", $"/tmp/user_ingest_before_reencoding/{video_id}/{video_id}.srt");

            // Console.WriteLine("SUBTITLES!");
            // Console.WriteLine(subtitles);

            // File.WriteAllText($"/tmp/user_ingest_reencoded/{video_id}/english-us.srt", subtitles);

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

            Console.WriteLine($"{Environment.GetEnvironmentVariable("api_address")}/video/get-upload-urls");
            WebRequest presignedUrl_request = WebRequest.Create($"{Environment.GetEnvironmentVariable("api_address")}/video/get-upload-urls");
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