using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Net;

namespace Interstellar
{
    public class Objects
    {
        // Input objects.
        public class videoChunkObject {
            public string video_byte { get; set; }
            public string id { get; set; }
            public string deviceid { get; set; }
            public string signature { get; set; }
            public string timestamp { get; set; }
            public string file_type { get; set; }
        }

        // Internal objects.
        public class UploadSignedObject {
            public string asset_id { get; set; }
            public string deviceid { get; set; }
            public string file_type { get; set; }
            public long chunk { get; set; } = -1;
            public string video_hash { get; set; }
            public string mode { get; set; }
        }
        public class chunkAuthorizationBody {
            public string signature { get; set; }
            public string signedObject { get; set; }
            public string ingestid { get; set; }
            public string client_deviceid { get; set; }
            public string client_data { get; set; }
            public string client_signature { get; set; }
            public long size { get; set; }
            public long chunk { get; set; }
            public string asset_id { get; set; }
            public string jwt { get; set; }
            public string mode { get; set; }
        }
        public class presignedUrlsBody {
            public string asset_id { get; set; }
            public UInt16[] signature { get; set; }
            public UInt16[] presigned_url_signature { get; set; }
            public string data { get; set; }
            public string presigned_url_data { get; set; }
            public string ingestid { get; set; }
            public string[] object_keys { get; set; }
            public string jwt { get; set; }
            public string client_deviceid { get; set; }
        }
        public class sizing {
            public Objects.streams[] streams { get; set; }
        }
        public class streams {
            public int width { get; set; }
            public int height { get; set; }
        }
        public class ffprobevideoinfo {
            public Objects.ffprobevideoinfostream[] streams { get; set; }
            public Objects.ffprobevideoinfoformat format { get; set; }
        }
        public class ffprobevideoinfostream {
            public int index { get; set; } = -1;
            public string codec_name { get; set; }
            public string codec_long_name { get; set; }
            public string profile { get; set; }
            public string codec_type { get; set; }
            public string codec_tag { get; set; }
            public int width { get; set; } = -1;
            public int height { get; set; } = -1;
            public int coded_width { get; set; } = -1;
            public int coded_height { get; set; } = -1;
            public int closed_captions { get; set; } = -1;
            public int has_b_frames { get; set; } = -1;
            public string sample_aspect_ration { get; set; }
            public string display_aspect_ratio { get; set; }
            public string pix_fmt { get; set; }
            public int level { get; set; } = -1;
            public string color_range { get; set; }
            public string color_space { get; set; }
            public string color_transfer { get; set; }
            public string color_primaries { get; set; }
            public string chroma_location { get; set; }
            public string field_order { get; set; }
            public int refs { get; set; } = -1;
            public string is_avc { get; set; }
            public string nal_length_size { get; set; }
            public string r_frame_rate { get; set; }
            public string avg_frame_rate { get; set; }
            public string time_base { get; set; }
            public int start_pts { get; set; } = -1;
            public string start_time { get; set; }
            public string bits_per_raw_sample { get; set; }
            public Objects.ffprobevideoinfostream_disposition disposition { get; set; }
            public Dictionary<String, String> tags { get; set; }
        }
        public class ffprobevideoinfostream_disposition {
            public int dub { get; set; } = -1;
            public int original { get; set; } = -1;
            public int comment { get; set; } = -1;
            public int lyrics { get; set; } = -1;
            public int karaoke { get; set; } = -1;
            public int forced { get; set; } = -1;
            public int hearing_impaired { get; set; } = -1;
            public int visual_impaired { get; set; } = -1;
            public int clean_effects { get; set; } = -1;
            public int attached_pic { get; set; } = -1;
            public int timed_thumbnails { get; set; } = -1;
        }
        public class ffprobevideoinfoformat {
            public string filename { get; set; }
            public int nb_streams { get; set; } = -1;
            public int nb_programs { get; set; } = -1;
            public int nb_stream_groups { get; set; } = -1;
            public string format_name { get; set; }
            public string format_long_name { get; set; }
            public string start_time { get; set; }
            public string duration { get; set; }
            public string size { get; set; }
            public string bit_rate { get; set; }
            public int probe_score { get; set; } = -1;
            public Dictionary<String, String> tags { get; set; }
        }
        // TODO: Remove this, it should just be an object or Dictionary<String, String>.
        // public class ffprobevideoinfotags {
        //     public string ENCODER { get; set; }
        // }
        public class chunkAuthorization {
            public string asset_id { get; set; }
            public long uploaded { get; set; }
            public long max_size { get; set; }
            public string process_state { get; set; }
            public bool error { get; set; }
            public string message { get; set; }
        }
        public class presignedUrlAuthorization {
            public bool ok { get; set; }
            public bool error { get; set; }
            public Objects.ObjectKey_URL[] files { get; set; }
        }

        // Response objects.
        public class reqok {
            public bool ok { get; set; }
            public string reencoded_chunk { get; set; }
        }
        public class errorD {
            public bool error { get; set; }
            public string type { get; set; }
            public string message { get; set; }
            public object additional { get; set; }
        }
        public class ingestStatusUpdate {
            public string type { get; set; }
            public string message { get; set; }
        }

        public class Process_item
        {
            public string program { get; set; }
            public string[] commands { get; set; }
            public Process_item_handoff handoff { get; set; }
        }
        public class Process_item_handoff
        {
            public string endpoint { get; set; }
        }

        public class Config
        {
            public Dictionary<String, Config_mode> modes { get; set; }
            public Dictionary<String, Config_storage> storage { get; set; }
            public Dictionary<String, Config_authentication> authentication { get; set; }
            public Dictionary<String, Config_database> database { get; set; }
            public Config_request request { get; set; }
            public Config_message message { get; set; }
            public Config_mode mode { get; set; }
        }

        public class Config_features
        {
            public string[] file_types { get; set; } = null;
            public Dictionary<string, string> file_alias { get; set; }
            public Config_features_chunks chunks { get; set; } = new Config_features_chunks();
        }

        public class Config_features_chunks
        {
            public bool save_to_disk { get; set; } = true;
        }

        public class Config_storage
        {
            public string type { get; set; }
            public string host { get; set; }
            public string bucket_name { get; set; }
            public string access_key_env { get; set; }
            public string secret_access_key_env { get; set; }
            public string region { get; set; } = "auto";
            public string get_upload_urls { get; set; }
            public Config_storage_multithreaded_transfer multithreaded_transfer { get; set; } = new Config_storage_multithreaded_transfer {};
        }

        public class Config_storage_multithreaded_transfer
        {
            public bool enabled { get; set; } = true;
            public int thread_limit { get; set; } = 4;
        }

        public class Config_authentication
        {
            public string type { get; set; }
            public string chunk_authorisation { get; set; }
        }

        public class Config_database
        {
            public string type { get; set; }
            public string[] hosts { get; set; }
            public string username_env { get; set; }
            public string password_env { get; set; }
        }

        public class Config_status
        {
            public string status_update { get; set; }
        }

        public class Config_mode
        {
            public string type { get; set; }
            public bool use_default { get; set; } = true;
            public Config_mode_storage_reference storage_reference { get; set; }
            public Config_storage storage { get; set; }
            public Config_authentication authentication { get; set; }
            public Config_mode_authentication_reference authentication_reference { get; set; }
            public Config_mode_database_reference database_reference { get; set; }
            public Config_database database { get; set; }
            public Config_status status { get; set; }
            public Config_features features { get; set; }
            public Config_mode_video_limits video_limits { get; set; }
            public Config_mode_Post_processing post_processing { get; set; }
            public bool invisible { get; set; }
        }

        public class Config_mode_storage_reference
        {
            public string id { get; set; }
            public string path { get; set; } = "/%asset_id%";
        }

        public class Config_mode_authentication_reference
        {
            public string id { get; set; }
        }

        public class Config_mode_database_reference
        {
            public string id { get; set; }
        }

        /////////////////////////////////////////////////////
        // These objects handle (mode).video_limits in the Interstellar configuration.
        // These objects are the circles of hell. Some of the most annoying code in this codebase. Any time FFprobe adds anew value, I'll have to update this code.
        // I promise you this is better than making objects and not using classes or trying to merge them into 1.
        /////////////////////////////////////////////////////

        public class Config_mode_video_limits
        {
            public Config_mode_video_limits_min_max min { get; set; }
            public Config_mode_video_limits_min_max max { get; set; }
            public Config_mode_video_limits_must_be_and_must_not_be must_be { get; set; }
            public Config_mode_video_limits_must_be_and_must_not_be must_not_be { get; set; }
            public Config_mode_video_limits_includes_and_not_includes includes { get; set; }
            public Config_mode_video_limits_includes_and_not_includes does_not_include { get; set; }
        }

        public class Config_mode_video_limits_min_max {
            public int video_streams { get; set; } = -1;
            public int audio_streams { get; set; } = -1;
            public Config_mode_video_limits_min_max_stream stream { get; set; }
            public Objects.ffprobevideoinfoformat format { get; set; }
        }

        public class Config_mode_video_limits_must_be_and_must_not_be {
            public int video_streams { get; set; } = -1;
            public int audio_streams { get; set; } = -1;
            public Config_mode_video_limits_min_max_stream stream { get; set; }
            public Objects.ffprobevideoinfoformat format { get; set; }
        }

        // Includes is different from match where there is an array of possible values. For example, if you had '"format_name": "mov,mp4,m4a,3gp,3g2,mj2"', you could do an includes for "mp4" and the rule would match. If you were doing a match, your value would have to be exactly '"format_name": "mov,mp4,m4a,3gp,3g2,mj2"' - this is why the includes rule exists.
        public class Config_mode_video_limits_includes_and_not_includes {
            public int video_streams { get; set; } = -1;
            public int audio_streams { get; set; } = -1;
            // TODO: We're only using the int versions of stream here.
            public Config_mode_video_limits_min_max_stream stream { get; set; }
            public Objects.ffprobevideoinfoformat format { get; set; }
        }

        // Only FFprobe stream INT values.
        public class Config_mode_video_limits_min_max_stream {
            public int index { get; set; } = -1;
            public int width { get; set; } = -1;
            public int height { get; set; } = -1;
            public int coded_width { get; set; } = -1;
            public int coded_height { get; set; } = -1;
            public int closed_captions { get; set; } = -1;
            public int has_b_frames { get; set; } = -1;
            public int level { get; set; } = -1;
            public int refs { get; set; } = -1;
            public int start_pts { get; set; } = -1;
            // public Objects.ffprobevideoinfostream_disposition disposition { get; set; }
            // public Dictionary<String, String> tags { get; set; }
        }

        // Only FFprobe format INT values.
        public class Config_mode_video_limits_min_max_format {
            public int nb_streams { get; set; } = -1;
            public int nb_programs { get; set; } = -1;
            public int nb_stream_groups { get; set; } = -1;
            public object start_time { get; set; }
            public object duration { get; set; }
            public object size { get; set; }
            public object bit_rate { get; set; }
            public int probe_score { get; set; } = 90;
        }

        // // Only FFprobe stream string values.
        // public class Config_mode_video_limits_stream_must_be_and_must_not_be {
        //     public string codec_name { get; set; }
        //     public string codec_long_name { get; set; }
        //     public string profile { get; set; }
        //     public string codec_type { get; set; }
        //     public string codec_tag { get; set; }
        //     public string sample_aspect_ration { get; set; }
        //     public string display_aspect_ratio { get; set; }
        //     public string pix_fmt { get; set; }
        //     public string color_range { get; set; }
        //     public string color_space { get; set; }
        //     public string color_transfer { get; set; }
        //     public string color_primaries { get; set; }
        //     public string chroma_location { get; set; }
        //     public string field_order { get; set; }
        //     public string is_avc { get; set; }
        //     public string nal_length_size { get; set; }
        //     public string r_frame_rate { get; set; }
        //     public string avg_frame_rate { get; set; }
        //     public string time_base { get; set; }
        //     public string start_time { get; set; }
        //     public string bits_per_raw_sample { get; set; }
        //     // public Objects.ffprobevideoinfostream_disposition disposition { get; set; }
        //     // public Dictionary<String, String> tags { get; set; }
        // }

        // // Only FFprobe format string values.
        // public class Config_mode_video_limits_format_must_be_and_must_not_be {
        //     public string filename { get; set; }
        //     public string format_name { get; set; }
        //     public string format_long_name { get; set; }
        //     public string start_time { get; set; }
        //     public string duration { get; set; }
        //     public string size { get; set; }
        //     public string bit_rate { get; set; }
        // }

        // Only FFprobe stream values AS List<object>'s.
        public class Config_mode_video_limits_stream_includes_and_not_includes {
            public List<object> codec_name { get; set; }
            public List<object> codec_long_name { get; set; }
            public List<object> profile { get; set; }
            public List<object> codec_type { get; set; }
            public List<object> codec_tag { get; set; }
            public List<object> sample_aspect_ration { get; set; }
            public List<object> display_aspect_ratio { get; set; }
            public List<object> pix_fmt { get; set; }
            public List<object> color_range { get; set; }
            public List<object> color_space { get; set; }
            public List<object> color_transfer { get; set; }
            public List<object> color_primaries { get; set; }
            public List<object> chroma_location { get; set; }
            public List<object> field_order { get; set; }
            public List<object> is_avc { get; set; }
            public List<object> nal_length_size { get; set; }
            public List<object> r_frame_rate { get; set; }
            public List<object> avg_frame_rate { get; set; }
            public List<object> time_base { get; set; }
            public List<object> start_time { get; set; }
            public List<object> bits_per_raw_sample { get; set; }
            // public Objects.ffprobevideoinfostream_disposition disposition { get; set; }
            // public Dictionary<String, String> tags { get; set; }
        }

        // Only FFprobe format values AS List<object>'s.
        public class Config_mode_video_limits_format_includes_and_not_includes {
            public List<object> filename { get; set; }
            public List<object> nb_streams { get; set; }
            public List<object> nb_programs { get; set; }
            public int nb_stream_groups { get; set; } = -1;
            public List<object> format_name { get; set; }
            public List<object> format_long_name { get; set; }
            public List<object> start_time { get; set; }
            public List<object> duration { get; set; }
            public List<object> size { get; set; }
            public List<object> bit_rate { get; set; }
            public List<object> probe_score { get; set; }
            public Dictionary<String, String> tags { get; set; }
        }

        public class Config_request
        {
            public Config_request_limit limit { get; set; }
            public Config_request_cors cors { get; set; }
        }

        public class Config_request_limit
        {
            public int max_request_body_size { get; set; } = 1000000000; // 45000000 = 0.048mb // 1,000,000,000 = 1GB
            public int keep_alive_timeout_from_minutes { get; set; } = 7200;
        }

        public class Config_request_cors
        {
            public string[] allowed_origins { get; set; }
        }

        public class Config_mode_Post_processing
        {
            public Config_mode_Post_processing_Commands commands { get; set; }
        }

        public class Config_mode_Post_processing_Commands
        {
            public List<Process_item> validation { get; set; }
            public List<Process_item> processing { get; set; }
        }

        public class Config_message
        {
            public string fatal_error { get; set; } = "Sorry, something went wrong.";
        }

        public class Config_video_limit_issues
        {
            public string problem { get; set; }
            public string prop { get; set; }
            public object needs_to_be { get; set; }
            public object must_include { get; set; }
            public object cannot_be { get; set; }
            public object value_is { get; set; }
        }

        public class HTTPS_Request_Response
        {
            public string response_body { get; set; }
            public HttpStatusCode status_code { get; set; }
            public HttpClient client { get; set; }
        }

        public class ObjectKey_URL {
            public string object_key { get; set; }
            public string url { get; set; }
        }

        public class File_path_URL {
            public string file_path { get; set; }
            public string url { get; set; }
        }

        public class files_in_directory {
            public string full_path { get; set; }
            public string file_name { get; set; }
            public string cropped_path { get; set; }
        }

        public class endpoint_generic_data {
            public bool ok { get; set; }
            public object data { get; set; }
        }

        public class Authentication_initalise {
            public long already_uplodaded { get; set; } = -1;
            public long max_size { get; set; } = -1;
        }

        public class Status_update_body {
            public string asset_id { get; set; }
            public string type { get; set; }
            public string section { get; set; }
            public long stage { get; set; }
            public long out_of { get; set; }
            public Status_update_body_video video { get; set; }
        }
        public class Status_update_body_video {
            public long out_time_ms { get; set; }
            public string out_time { get; set; }
        }
    }
}