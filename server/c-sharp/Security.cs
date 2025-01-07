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
    public class Security {
        public static string file_name_sanitize(string i_input)
        {
            // TODO: fix bcs regex with no timeout is blowdart horror
            if (String.IsNullOrWhiteSpace(i_input)) {
                return null;
            }
            Regex lettersOrDigitsOnly = new Regex(@"[^a-zA-Z0-9_]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
            string mmmm = lettersOrDigitsOnly.Replace(i_input, "").ToString();
            return mmmm;
        }

        public static string file_type_sanitize(string i_input)
        {
            if (String.IsNullOrWhiteSpace(i_input)) {
                return null;
            }
            Regex lettersOrDigitsOnly = new Regex(@"[^a-zA-Z0-9]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
            string mmmm = lettersOrDigitsOnly.Replace(i_input, "").ToString();
            return mmmm;
        }

        public static string redis_key_sanitize(string i_input)
        {
            if (String.IsNullOrWhiteSpace(i_input)) {
                return null;
            }
            Regex lettersOrDigitsOnly = new Regex(@"[^a-zA-Z0-9]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
            string mmmm = lettersOrDigitsOnly.Replace(i_input, "").ToString();
            return mmmm;
        }

        public static string generate_random_string() {
            char[] letters = "abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890abcdefghijklmnopqrstuvwxyz1234567890".ToCharArray();
            Random r = new Random();
            string randomString = "";
            for (int i = 0; i < 100; i++)
            {
                randomString += letters[r.Next(0, 35)].ToString();
            }
            return randomString;
        }

        public static bool is_acceptable_video_file_type(string processOutput) {
            if (processOutput != "mp4" && processOutput != "mov" && processOutput != "mkv" && processOutput != "webm" && processOutput != "quicktime" && processOutput != "x-flv") {
                return false;
            } else {
                return true;
            }
        }

        public static string url_sanitize(string data) {
            if (data == null) {
                return null;
            }
            data = Regex.Replace(data, "[^a-zA-Z0-9_-]", ""); // replace all non-alphanumeric characters with empty string
            return data.ToString();
        }

        public static bool is_number(string value) {
            return Regex.IsMatch(value, "[0-9]"); // check if value contains any digit
        }

        public static string GenerateJwtToken(object payload, string privateKeyData)
        {
            byte[] privateKeyDataBytes = Convert.FromBase64String(privateKeyData);
            // var payload = new Dictionary<string, object> ()  {
            //     { "hi", true },
            //     { "username", "lmao" }
            // };

            // Create an ECDsa key from the private key data bytes
            var privateKey = ECDsa.Create();
            privateKey.ImportPkcs8PrivateKey(privateKeyDataBytes, out _);

            // Encode the payload with ES512 algorithm
            string token = JWT.Encode(payload, privateKey, JwsAlgorithm.ES512);

            return token;
        }
    }
}