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
        public static void ProcessDirectory(string targetDirectory, List<string> list, string videoid)
        {
            // Process the list of files found in the directory.
            string [] fileEntries = Directory.GetFiles(targetDirectory);
            foreach(string fileName in fileEntries)
                ProcessFile(fileName, list, videoid);

            // Recurse into subdirectories of this directory.
            string [] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach(string subdirectory in subdirectoryEntries)
                ProcessDirectory(subdirectory, list, videoid);
        }

        // Insert logic for processing found files here.
        public static string ProcessFile(string path, List<string> list, string videoid)
        {
            Console.WriteLine("Processed file '{0}'.", path);
            list.Add(path.Replace($"/tmp/user_ingest_reencoded/{videoid}/", "").Replace(".tmp", ""));
            return path;
        }

        public static T MergeObjects<T>(T target, T source)
        {
            Type targetType = typeof(T);
            PropertyInfo[] properties = targetType.GetProperties();

            foreach (PropertyInfo property in properties)
            {
                object sourceValue = property.GetValue(source, null);
                if (sourceValue != null)
                {
                    property.SetValue(target, sourceValue, null);
                }
            }

            return target;
        }

        public static void CopyValues<T> (T target, T source)
        {
            Type t = typeof (T);
            var properties = t.GetProperties ().Where (prop => prop.CanRead && prop.CanWrite);
            foreach (var prop in properties)
            {
                var value = prop.GetValue (source, null);
                if (value != null)
                    prop.SetValue (target, value, null);
            }
        }

        public static bool DeleteDirectory(string dirPath) {
            bool deleted = false;
            while (!deleted)
            {
                try
                {
                    Directory.Delete(dirPath, true);
                    deleted = true;
                }
                catch (IOException)
                {
                    // Wait for 100 milliseconds and try again
                    Thread.Sleep(100);
                }
            }

            return true;
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