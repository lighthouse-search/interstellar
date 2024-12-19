// Check HSTS.

using Interstellar;
using System.Text;
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

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
    policy  =>
    {
        policy.WithOrigins("http://127.0.0.1:3001")
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors(MyAllowSpecificOrigins);

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

app.MapPost("/upload-chunk", async (HttpContext context) => {
    try {
    Console.WriteLine("/upload-chunk");

    Objects.UploadSignedObject UploadSignedObject = JsonConvert.DeserializeObject<Objects.UploadSignedObject>(context.Request.Form["metadata"].ToString());

    string video_id = Security.file_name_sanitize(UploadSignedObject.video_id);
    if (String.IsNullOrWhiteSpace(video_id)) {
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

    string file_type = Security.file_type_sanitize(UploadSignedObject.file_type.Replace("video/", ""));
    if (String.IsNullOrWhiteSpace(file_type)) {
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
    
    if (file_type == "x-matroska") {
        file_type = "mkv";
    }

    if (file_type != "mp4" && file_type != "mkv" && file_type != "mov" && file_type != "flv" && file_type != "xflv" && file_type != "webm" && file_type != "avi" && file_type != "xmatroska" && file_type != "quicktime") {
        // bad file_type - make response. Use this as a "glad we found that out before we uploaded it and ran it through FFMPEG". THIS IS JUST WHAT THE CLIENT IS CLAIMING THE file_type IS. DO NOT TREAT THIS VIDEO AS ACTUALLY BEING SPECIFIED file_type.
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error($"Unsupported file_type, gave .{file_type}, must be either mp4/mkv/mov/flv/webm/avi"));
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
    } // http://127.0.0.1:4455/api/native-v1

    Console.WriteLine($"ADDRESS {Environment.GetEnvironmentVariable("api_address")}/api/native-v1/video/chunk-authorisation");
    WebRequest request = WebRequest.Create($"{Environment.GetEnvironmentVariable("api_address")}/video/chunk-authorisation");
    // Set the Method property of the request to POST.
    request.Method = "POST";
    request.ContentType = "application/json";

    var signed_data = new Objects.chunkAuthorizationBody {
        video_id = video_id,
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

    // if (chunkAuthorizationOutput.process_state != "ingest_handoff" && chunkAuthorizationOutput.process_state != "waiting") {
    //     context.Response.StatusCode = 400;
    //     await context.Response.WriteAsJsonAsync(Responses.error("Video state is not acceptable (this video might have already been uploaded?)"));
    //     return;
    // }

    System.IO.Directory.CreateDirectory($"/tmp/user_ingest_before_reencoding/{video_id}");

    // File.Delete($"/tmp/user_ingest_before_reencoding/{video_id}/{video_id}.{file_type}");

    // TODO: SANITIZE THAT video_id HOLY SHIT BUD. DO NOT FORGET.
    System.IO.Directory.CreateDirectory($"/tmp/user_ingest_before_reencoding/{video_id}/chunks");

    var db = redis.GetDatabase();
    Console.WriteLine($"{Security.redis_key_sanitize(video_id)}_alreadyuploaded");
    Console.WriteLine($"{Security.redis_key_sanitize(video_id)}_maxsize");
    string already_uploaded_raw = await db.StringGetAsync($"{Security.redis_key_sanitize(video_id)}_alreadyuploaded");
    string max_size_raw = await db.StringGetAsync($"{Security.redis_key_sanitize(video_id)}_maxsize");

    // TODO: Remove the 1 == 1 placeholder.
    if (already_uploaded_raw == null || max_size_raw == null || 1 == 1) {
        already_uploaded = chunkAuthorizationOutput.uploaded+fileSize;
        max_size = chunkAuthorizationOutput.max_size;
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
        await context.Response.WriteAsJsonAsync(Responses.error($"You cannot exceed authorized size of {max_size}"));
        return; 
    }

    db.StringSet($"{Security.redis_key_sanitize(video_id)}_alreadyuploaded", $"{already_uploaded}");
    db.StringSet($"{Security.redis_key_sanitize(video_id)}_maxsize", $"{max_size}");

    await using (FileStream aFile = new FileStream($"/tmp/user_ingest_before_reencoding/{video_id}/chunks/{video_id}_{chunk}.ingest_chunk", FileMode.Append, FileAccess.Write))
    await context.Request.Form.Files[0].CopyToAsync(aFile);

    Console.WriteLine("OK WHAT");
    Console.WriteLine(already_uploaded);
    Console.WriteLine(max_size);

    try {
        if (already_uploaded == max_size) {
            // PROCESSING LOGIC
            await Processing.video(video_id, file_type);
        } else {
            context.Response.StatusCode = 200;
            await context.Response.WriteAsJsonAsync(new Objects.errorD 
            {
                type = "chunk_completed"
            });
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

        File_operations.DeleteDirectory($"/tmp/user_ingest_before_reencoding/{video_id}");
        File_operations.DeleteDirectory($"/tmp/user_ingest_reencoded/{video_id}");
        
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