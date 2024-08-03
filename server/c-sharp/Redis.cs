using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DnsClient;
using DnsClient.Protocol;
using Newtonsoft.Json;
using System.Diagnostics;
using Jose;
using System.Security.Cryptography.X509Certificates;

namespace Interstellar
{
    public class Redis {
        public static void start()
        {
            Process start_redis = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "redis-server",
                    Arguments = "--daemonize yes --bind 0.0.0.0 --port 6379",
                    CreateNoWindow = true,
                }
            };

            start_redis.Start();
            start_redis.WaitForExit();
        }
    }
}