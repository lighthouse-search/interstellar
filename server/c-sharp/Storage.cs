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
            if (storage_type == "remote_server") {
                Objects.presignedUrlAuthorization upload_urls = await Remote_server.Get_upload_urls(config, base_path, asset_id);
            } else if (storage_type == "s3") {
                Objects.presignedUrlAuthorization upload_urls = await S3.Generate_Presigned_URL(S3.Get_S3_client(), config.mode.storage.bucket_name, base_path, asset_id);
            } else {
                throw new Exception($"'{storage_type}' is invalid.");
            }

            // await Upload_directory(config, new string[] { $"/tmp/user_ingest_reencoded/{asset_id}" }, asset_id);
        }
    }
}