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
    public class Storage
    {
        public static async Task initalise_move(HttpContext context, Objects.Config config, string asset_id, string file_type, string base_path) {
            string storage_type = config.mode.storage.type.ToLower();

            List<Objects.files_in_directory> files_in_directory = File_operations.Get_files_in_directory(new string[] { base_path }, true);
            List<String> files_in_directory_full_paths = files_in_directory.Select(file => file.full_path).ToList();
            List<String> files_in_directory_cropped_paths = files_in_directory.Select(file => file.cropped_path).ToList();

            Objects.ObjectKey_URL[] urls;
            if (storage_type == "remote_server") {
                Objects.ObjectKey_URL[] upload_urls = await Remote_server.Get_upload_urls(config, files_in_directory_cropped_paths.ToArray(), asset_id);
                urls = upload_urls;
            } else if (storage_type == "s3") {
                Objects.ObjectKey_URL[] presigned_urls = S3.Generate_Presigned_URL(config, asset_id, S3.Get_S3_client(config), config.mode.storage.bucket_name, files_in_directory_cropped_paths.ToArray(), 43200).ToArray();
                urls = presigned_urls;
            } else {
                throw new Exception($"'{storage_type}' is invalid.");
            }

            var relevant_files = from file in files_in_directory
                        join presigned_url in urls on file.cropped_path equals presigned_url.object_key
                        select new { file, presigned_url };

            List<Objects.File_path_URL> files_and_urls = new List<Objects.File_path_URL>();
            foreach (var item in relevant_files) {
                files_and_urls.Add(new Objects.File_path_URL {
                    url = item.presigned_url.url,
                    file_path = item.file.full_path
                });
            }

            await Https.Upload_directory(config, files_and_urls);
        }
    }
}