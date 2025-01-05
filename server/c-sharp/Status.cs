using System;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Interstellar
{
    public class Status
    {
        public static async Task update(Objects.Config config, string asset_id, string type, string section, long stage, long out_of, Objects.Status_update_body_video video)
        {
            try {
                await Https.Request_POST(
                    config.mode.status.status_update,
                    new Objects.Status_update_body {
                        asset_id = asset_id,
                        type = type,
                        section = section,
                        stage = stage,
                        out_of = out_of,
                        video = video
                    }
                );
            } catch (Exception e) {
                // If it works it works but also we don't care.
                Console.WriteLine(e);
            }
        }
    }
}