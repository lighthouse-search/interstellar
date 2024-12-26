// Check HSTS.

using Interstellar;
using System.Text;
using System.Net;
using StackExchange.Redis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System.Net.Http;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// builder.Services.AddSignalR();
builder.Services.AddCors();

Objects.Config config = Config.Get("test");

if (config == null)
{
    throw new Exception("Config object is null");
}

builder.WebHost.UseKestrel(options =>
{
    options.Limits.MaxRequestBodySize = config.request.limit.max_request_body_size;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(config.request.limit.keep_alive_timeout_from_minutes);
});

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
    policy  =>
    {
        policy.WithOrigins(config.request.cors.allowed_origins)
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors(MyAllowSpecificOrigins);

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["interstellar"] = "https://example.com";
        context.Response.Headers["server"] = "";
        return Task.CompletedTask;
    });

    await next();
});

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
    await context.Response.WriteAsJsonAsync(Responses.error(config.message.fatal_error));
    return;
});

app.MapPost("/upload-chunk", async (HttpContext context) => {
    string video_id = null;
try {
    Objects.Config config = Config.Get("test");
    Console.WriteLine("/upload-chunk");

    Objects.UploadSignedObject UploadSignedObject = JsonConvert.DeserializeObject<Objects.UploadSignedObject>(context.Request.Form["metadata"].ToString());

    video_id = Security.file_name_sanitize(UploadSignedObject.video_id);
    if (String.IsNullOrWhiteSpace(video_id)) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("body.video_id is null or whitespace."));
        await context.Response.CompleteAsync();
        return;
    }

    // Authorisation: The string we'll pass (sent by the client) to the external server for authorisation. It is recommended this be a JWT.
    string authorization = context.Request.Headers["Authorization"].ToString();
    if (String.IsNullOrWhiteSpace(authorization)) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("headers.Authorization is null or whitespace."));
        await context.Response.CompleteAsync();
        return;
    }

    string file_type = Security.file_type_sanitize(UploadSignedObject.file_type.Replace("video/", ""));
    if (String.IsNullOrWhiteSpace(file_type)) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("body.file_type is null or whitespace."));
        await context.Response.CompleteAsync();
        return;
    }

    // Chunk: A chunk allows the server to reconstruct the video bytes in the correct order during processing. A "chunk" is any video packet. The client being able to upload chunks without waiting for another chunk to upload is essential for upload multithreading. A client can upload chunk 1, then chunk 3, then chunk 2, then chunk 8 and the server will reconstruct the video in the correct order. This prevents the video from becoming corrupt.
    long chunk = UploadSignedObject.chunk;
    if (chunk < 0) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("body.chunk is not a number."));
        await context.Response.CompleteAsync();
        return;
    }
    
    // Check if an overwrite for this file type has been defined in the configuration file.
    if (config.mode.features.file_alias.ContainsKey(file_type)) {
        file_type = config.mode.features.file_alias[file_type];
    }
    // Overwrites certain file types, e.g converts filetypes like "x-matroska" to proper file extensions like ".mkv" when defined by the administrator.
    if (config.mode.features.file_types.Contains(file_type) == false) {
        // bad file_type - make response. Use this as a "glad we found that out before we uploaded it and ran it through FFMPEG". THIS IS JUST WHAT THE CLIENT IS CLAIMING THE file_type IS. DO NOT TREAT THIS VIDEO AS ACTUALLY BEING SPECIFIED file_type.
        context.Response.StatusCode = 400;
        var json = new JObject
        {
            "acceptable_file_types", config.mode.features.file_types
        };
        await context.Response.WriteAsJsonAsync(Responses.error($"Unsupported file_type, gave .{file_type}.", json));
        await context.Response.CompleteAsync();
        return;
    }

    var form = await context.Request.ReadFormAsync();
    var file = form.Files["file"];
    int fileSize = (int)file.Length;
    if (fileSize == null || fileSize == 0) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("You cannot upload a file that is 0 bytes."));
        await context.Response.CompleteAsync();
        return;
    }

    Objects.HTTPS_Request_Response chunk_authorisation_response = await Https.Request_POST(
        config.authorisation.chunk_authorisation,
        new Objects.chunkAuthorizationBody {
            video_id = video_id,
            size = fileSize,
            chunk = chunk,
        }
    );
    Objects.chunkAuthorization chunkAuthorizationOutput = JsonConvert.DeserializeObject<Objects.chunkAuthorization>(chunk_authorisation_response.response_body);

    // External server didn't like that.
    if (chunkAuthorizationOutput.error == true) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error($"Error from server: {chunkAuthorizationOutput.message}"));
        await context.Response.CompleteAsync();
        return;
    }
    
    long already_uploaded = chunkAuthorizationOutput.uploaded;
    long max_size = chunkAuthorizationOutput.max_size;

    if (already_uploaded > max_size) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error($"You cannot exceed authorised size of {max_size}"));
        await context.Response.CompleteAsync();
        return;
    }

    // Ensure the required directories exist.
    System.IO.Directory.CreateDirectory($"/tmp/user_ingest_before_reencoding/{video_id}");
    System.IO.Directory.CreateDirectory($"/tmp/user_ingest_before_reencoding/{video_id}/chunks");

    // Write incoming chunk to disk.
    await using (FileStream aFile = new FileStream($"/tmp/user_ingest_before_reencoding/{video_id}/chunks/{video_id}_{chunk}.ingest_chunk", FileMode.Append, FileAccess.Write))
    await context.Request.Form.Files[0].CopyToAsync(aFile);

    Console.WriteLine("OK WHAT");
    Console.WriteLine(already_uploaded+fileSize);
    Console.WriteLine(max_size);

    // Now that we've written the incoming chunk to disk, we'll check if the video has been uploaded based on the allocated size.
    if (already_uploaded+fileSize == max_size) {
        // Video is fully uploaded and perfectly matches the allocated size. We'll begin video processing.
        try {
            await Processing.video(context,video_id, file_type);
        } catch (Exception ex)
        {
            Console.WriteLine("ERROR1!");
            Console.WriteLine(ex);
            Handler.Fatal_error(context, video_id);
            return;
        }
    } else {
        context.Response.StatusCode = 200;
        await context.Response.WriteAsJsonAsync(new Objects.errorD 
        {
            type = "chunk_completed"
        });
        await context.Response.CompleteAsync();
        return;
    }
} catch (WebException ex)
{
    Console.WriteLine("ERROR2!", ex);
    Handler.Fatal_error(context, video_id);
    return;
}
});

app.Run();