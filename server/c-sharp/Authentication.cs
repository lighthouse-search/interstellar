using System;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Interstellar
{
    public class Authentication
    {
        public static async Task<Objects.Authentication_initalise> initalise(HttpContext context, Objects.Config config, IDatabase redis, string mode, string asset_id, long file_size, long chunk)
        {
            string storage_type = config.mode.storage.type.ToLower();
            if (storage_type == "remote_server") {
                Objects.chunkAuthorization chunk_authorisation = await Authentication.chunk_authorisation(config, mode, asset_id, file_size, chunk);

                // External server didn't like that.
                if (chunk_authorisation.error == true) {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(Responses.error($"Error from server: {chunk_authorisation.message}"));
                    await context.Response.CompleteAsync();
                    return null;
                }

                return new Objects.Authentication_initalise {
                    already_uplodaded = chunk_authorisation.uploaded,
                    max_size = chunk_authorisation.max_size
                };
            } else if (storage_type == "redis") {
                if (redis != null) {
                    new Exception("Missing redis connection (If you're seeing this, you should file a bug report).");
                }

                // TODO: Need to make a function that adds to this before closing the request.
                string already_uploaded_raw = await redis.StringGetAsync($"{Security.redis_key_sanitize(asset_id)}_already_uploaded");
                string max_size_raw = await redis.StringGetAsync($"{Security.redis_key_sanitize(asset_id)}_max_size");

                long already_uploaded = long.Parse(already_uploaded_raw);
                long max_size = long.Parse(max_size_raw);

                return new Objects.Authentication_initalise {
                    already_uplodaded = already_uploaded,
                    max_size = max_size
                };
            } else {
                throw new Exception("No valid storage type.");
            }
        }

        public static async Task clock_in(Objects.Config config, IDatabase redis, string asset_id, long already_uploaded, long max_size)
        {
            string storage_type = config.mode.storage.type.ToLower();
            if (storage_type == "remote_server") {
                // We don't need to do anything.
            } else if (storage_type == "redis") {
                if (redis != null) {
                    new Exception("Missing redis connection (If you're seeing this, you should file a bug report).");
                }

                redis.StringSet($"{Security.redis_key_sanitize(asset_id)}_already_uploaded", $"{already_uploaded}");
                redis.StringSet($"{Security.redis_key_sanitize(asset_id)}_max_size", $"{max_size}");
            } else {
                throw new Exception("Storage type isn't handled, aborting. Please file a bug report.");
            }
        }

        public static async Task<Objects.chunkAuthorization> chunk_authorisation(Objects.Config config, string mode, string asset_id, long file_size, long chunk)
        {
            Objects.HTTPS_Request_Response chunk_authorisation_response = await Https.Request_POST(
                config.mode.authentication.chunk_authorisation,
                new Objects.chunkAuthorizationBody {
                    mode = mode,
                    asset_id = asset_id,
                    size = file_size,
                    chunk = chunk
                }
            );
            Objects.chunkAuthorization chunk_authorization_output = JsonConvert.DeserializeObject<Objects.chunkAuthorization>(chunk_authorisation_response.response_body);
            return chunk_authorization_output;
        }
    }
}