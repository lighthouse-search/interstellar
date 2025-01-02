using System;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Interstellar
{
    public static class File_operations
    {
        // Process all files in the directory passed in, recurse on any directories that are found, and process the files they contain.
        // Warning: I haven't fully tested 'recursive' here.
        public static void ProcessDirectory(string targetDirectory, List<string> list, bool recursive, bool file_names)
        {
            // Process the list of files found in the directory.
            string [] fileEntries = Directory.GetFiles(targetDirectory);
            foreach(string fileName in fileEntries)
                ProcessFile(fileName, list, file_names);

            // Recurse into subdirectories of this directory.
            if (recursive == true) {
                string [] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
                foreach(string subdirectory in subdirectoryEntries)
                    ProcessDirectory(subdirectory, list, true, file_names); // 'recursive' is always true here, as the original param was recursive. This would only be false in the future if there was a limit on recursiveness, e.g. only going 1 directory deep.
            }
        }

        // Insert logic for processing found files here.
        public static string ProcessFile(string path, List<string> list, bool file_names)
        {
            if (file_names == true) {
                list.Add(Path.GetFileName(path));
            } else {
                list.Add(path);
            }
            return path;
        }

        public static string[] Get_files_in_directory(string[] base_paths, bool recursive, bool file_names)
        {
            string[] files_list = new string [] {};
            var pre_files_list = files_list.ToList();

            foreach(string path in base_paths)
            {
                if (File.Exists(path))
                {
                    // This path is a file
                    File_operations.ProcessFile(path, pre_files_list, file_names);
                }
                else if (Directory.Exists(path))
                {
                    // This path is a directory
                    File_operations.ProcessDirectory(path, pre_files_list, recursive, file_names);
                }
                else
                {
                    Console.WriteLine("{0} is not a valid file or directory.", path);
                }
            }

            return files_list;
        }

        public static async Task<string> GetWhisperSubtitles(string AudioFilePath, string SrtFilePath)
        {
            if (AudioFilePath == null || SrtFilePath == null) {
                throw new Exception("Missing critical data.");
            }
            
            // Create an HttpClient with the API key as authorization header
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("whisper_api_key").ToString()); // Replace "TOKEN" with your actual API token

            // Create a multipart/form-data request with the audio file as content
            using var content = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(AudioFilePath);
            content.Add(new StreamContent(fileStream), "file", Path.GetFileName(AudioFilePath));
            content.Add(new StringContent("whisper-1"), "model");

            // Send a POST request to the Whisper endpoint
            var requestUrl = "https://api.openai.com/v1/audio/transcriptions";
            var response = await client.PostAsync(requestUrl, content);

            // Check if the request was successful
            if (response.IsSuccessStatusCode)
            {
                // Read the response content as a string
                var result = await response.Content.ReadAsStringAsync();

                // Write the result to the output SRT file
                File.WriteAllText(SrtFilePath, result);

                Console.WriteLine("SRT file generated successfully.");
                return result;
            }
            else
            {
                // Handle error
                Console.WriteLine($"Request failed: {response.ReasonPhrase}");
                return null;
            }
        }
    }
}