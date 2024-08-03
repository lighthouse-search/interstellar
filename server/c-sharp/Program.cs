// Check HSTS.

using Interstellar;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Net;
using StackExchange.Redis;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSignalR();
builder.Services.AddCors();

builder.WebHost.UseKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 45000000;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(60);
});

var app = builder.Build();

Redis.start();

ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(
    new ConfigurationOptions{
        EndPoints = { "127.0.0.1:6379" },    
    });

app.UseExceptionHandler("/Error");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// app.UseHttpsRedirection();
app.UseStaticFiles(); // TODO: should this be disabled?

app.UseCors();

app.UseRouting();

app.MapGet("/error", async (HttpContext context) => {
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(Responses.error("Sorry, a fatal error occured on the server. These are rare! Please try again."));
    return;
});

app.MapPost("/upload-chunk", async(HttpContext context) => {
    try {
    Console.WriteLine("/upload-chunk");

    Objects.UploadSignedObject UploadSignedObject = JsonConvert.DeserializeObject<Objects.UploadSignedObject>(context.Request.Form["signedObject"].ToString());

    string videoID = Security.file_name_sanitize(UploadSignedObject.video_id);
    if (String.IsNullOrWhiteSpace(videoID)) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("body.video_id is null or whitespace."));
        return;
    }

    string authorization = context.Request.Headers["Authorization"].ToString();
    if (String.IsNullOrWhiteSpace(authorization)) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("headers.Authorization is null or whitespace."));
        return;
    }

    string filetype = Security.file_type_sanitize(UploadSignedObject.filetype.Replace("video/", ""));
    if (String.IsNullOrWhiteSpace(filetype)) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("body.file_type is null or whitespace."));
        return;
    }

    if (UploadSignedObject.chunk < 0) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("body.chunk is not a number."));
        return;
    }
    long chunk = UploadSignedObject.chunk;
    
    if (filetype == "x-matroska") {
        filetype = "mkv";
    }

    if (filetype != "mp4" && filetype != "mkv" && filetype != "mov" && filetype != "flv" && filetype != "xflv" && filetype != "webm" && filetype != "avi" && filetype != "xmatroska" && filetype != "quicktime") {
        // bad filetype - make response. Use this as a "glad we found that out before we uploaded it and ran it through FFMPEG". THIS IS JUST WHAT THE CLIENT IS CLAIMING THE FILETYPE IS. DO NOT TREAT THIS VIDEO AS ACTUALLY BEING SPECIFIED FILETYPE.
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error($"Unsupported filetype, gave .{filetype}, must be either mp4/mkv/mov/flv/webm/avi"));
        return;
    }

    long already_uploaded = -1;
    long max_size = -1;

    var form = await context.Request.ReadFormAsync();
    var file = form.Files["file"];
    int fileSize = (int)file.Length;
    if (fileSize == null || fileSize == 0) {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(Responses.error("Uploaded Filesize cannot be 0."));
        return;
    }

    Console.WriteLine($"ADDRESS {Environment.GetEnvironmentVariable("apiaddress")}/ingest/chunk-authorization");
    WebRequest request = WebRequest.Create($"{Environment.GetEnvironmentVariable("apiaddress")}/ingest/chunk-authorization");
    // Set the Method property of the request to POST.
    request.Method = "POST";
    request.ContentType = "application/json";

    var signed_data = new Objects.chunkAuthorizationBody {
        videoid = videoID,
        size = fileSize,
        chunk = chunk,
    };

    string chunkAuthBody = JsonConvert.SerializeObject(signed_data);
    var chunkAuthBodyBytes = Encoding.Default.GetBytes(chunkAuthBody);

    Stream newStream = request.GetRequestStream();
    newStream.Write(chunkAuthBodyBytes, 0, chunkAuthBodyBytes.Length);
    newStream.Close();

    WebResponse response = request.GetResponse();

    string DANGEROUS_chunk = null;
    
    using (Stream request_stream = response.GetResponseStream())
    {
        // Open the stream using a StreamReader for easy access.
        StreamReader reader = new StreamReader(request_stream);
        // Read the content.
        string responseFromServer = reader.ReadToEnd();
        // Display the content.
        if (!String.IsNullOrWhiteSpace(responseFromServer)) {
            DANGEROUS_chunk = responseFromServer;
            Console.WriteLine("RESPONSE FROM SERVER:");
            Console.WriteLine(responseFromServer);
        } // chunkAuthorization
    }

    response.Close();

    Objects.chunkAuthorization chunkAuthorizationOutput = JsonConvert.DeserializeObject<Objects.chunkAuthorization>(DANGEROUS_chunk);

    if (chunkAuthorizationOutput.error == true) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error($"Unauthorized."));
        return;
    }

    if (chunkAuthorizationOutput.process_state != "ingest_handoff" && chunkAuthorizationOutput.process_state != "waiting") {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("Video state is not acceptable (this video might have already been uploaded?)"));
        return;
    }

    System.IO.Directory.CreateDirectory($"/tmp/user_ingest_before_reencoding/{videoID}");

    // File.Delete($"/tmp/user_ingest_before_reencoding/{videoID}/{videoID}.{filetype}");

    // TODO: SANITIZE THAT VIDEOID HOLY SHIT BUD. DO NOT FORGET.
    System.IO.Directory.CreateDirectory($"/tmp/user_ingest_before_reencoding/{videoID}/chunks");

    var db = redis.GetDatabase();
    string already_uploaded_raw = await db.StringGetAsync($"{Security.redis_key_sanitize(videoID)}_alreadyuploaded");
    string max_size_raw = await db.StringGetAsync($"{Security.redis_key_sanitize(videoID)}_maxsize");

    if (already_uploaded_raw == null) {
        already_uploaded = chunkAuthorizationOutput.uploaded+fileSize;
        max_size = chunkAuthorizationOutput.maxSize;
        Console.WriteLine("NOT EXISTS");
        Console.WriteLine(already_uploaded);
        Console.WriteLine(max_size);
    } else {
        already_uploaded = long.Parse(already_uploaded_raw)+fileSize; // add the incoming file size
        max_size = long.Parse(max_size_raw);
        Console.WriteLine("EXISTS");
        Console.WriteLine(already_uploaded);
        Console.WriteLine(max_size);
    }

    if (already_uploaded == -1 || max_size == -1) {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(Responses.error("Sorry, something went wrong [ERR: 8820]"));
        return;
    }

    if (already_uploaded > max_size) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error($"Video size ({already_uploaded}) larger than expected size {max_size}"));
        return;
    }

    db.StringSet($"{Security.redis_key_sanitize(videoID)}_alreadyuploaded", $"{already_uploaded}");
    db.StringSet($"{Security.redis_key_sanitize(videoID)}_maxsize", $"{max_size}");

    await using (FileStream aFile = new FileStream($"/tmp/user_ingest_before_reencoding/{videoID}/chunks/{videoID}_{chunk}.ingest_chunk", FileMode.Append, FileAccess.Write))
    await context.Request.Form.Files[0].CopyToAsync(aFile);

    Console.WriteLine("OK WHAT");
    Console.WriteLine(already_uploaded);
    Console.WriteLine(max_size);

    try {
        if (already_uploaded == max_size) {
            // PROCESSING LOGIC
        }
    } catch (WebException ex)
    {
        Console.WriteLine("ERROR1!");
        Console.WriteLine(ex);
        if (ex.Response is HttpWebResponse errorResponse)
        {
            Console.WriteLine("Error occurred. Response from server:");
            using (Stream responseStream = errorResponse.GetResponseStream())
            {
                StreamReader reader = new StreamReader(responseStream);
                string responseFromServer = reader.ReadToEnd();
                Console.WriteLine(responseFromServer);
            }
        }
        else
        {
            Console.WriteLine("Error occurred. Failed to get response from server.");
        }

        File_operations.DeleteDirectory($"/tmp/user_ingest_before_reencoding/{videoID}");
        File_operations.DeleteDirectory($"/tmp/user_ingest_reencoded/{videoID}");
        
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new Objects.errorD 
        {
            error = true,
            message = "Sorry, something went wrong. [error 12457]"
        });
        return;
    }
    } catch (WebException ex)
    {
        Console.WriteLine("ERROR2!");
        Console.WriteLine(ex);
        if (ex.Response is HttpWebResponse errorResponse)
        {
            Console.WriteLine("Error occurred. Response from server:");
            using (Stream responseStream = errorResponse.GetResponseStream())
            {
                StreamReader reader = new StreamReader(responseStream);
                string responseFromServer = reader.ReadToEnd();
                Console.WriteLine(responseFromServer);
            }
        }
        else
        {
            Console.WriteLine("Error occurred. Failed to get response from server.");
        }
        
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new Objects.errorD 
        {
            error = true,
            message = "Sorry, something went wrong. [error 14739]"
        });
        return;
    }
});

app.Run();