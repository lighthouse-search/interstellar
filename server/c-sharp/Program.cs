// Check HSTS.

using Interstellar;
using System.Text;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// builder.Services.AddSignalR();
builder.Services.AddCors();

Objects.Config default_config = Config.Get(null);
if (default_config == null)
{
    throw new Exception("Config object is null");
}

builder.WebHost.UseKestrel(options =>
{
    options.Limits.MaxRequestBodySize = default_config.request.limit.max_request_body_size;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(default_config.request.limit.keep_alive_timeout_from_minutes);
});

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
    policy  =>
    {
        policy.WithOrigins(default_config.request.cors.allowed_origins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        // TODO: Make AllowCredentials optional based on config value.
        .AllowCredentials();
    });
});


var app = builder.Build();

app.UseCors(MyAllowSpecificOrigins);

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        // TODO: Add URL here.
        context.Response.Headers["interstellar"] = "https://example.com";
        context.Response.Headers["server"] = "";
        return Task.CompletedTask;
    });

    await next();
});

// Redis.start();

app.UseExceptionHandler("/Error");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // TODO: should this be disabled?

app.UseCors();

app.UseRouting();

app.MapGet("/error", async (HttpContext context) => {
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(Responses.error(default_config.message.fatal_error));
    return;
});

app.MapGet("/mode/list", async (HttpContext context) => {
    Objects.Config config = Config.Get(null);
    Dictionary<string, Objects.Config_mode>.KeyCollection keys = config.modes.Keys;

    List<string> public_modes = new List<string>();
    // Iterate through the keys
    foreach (string key in keys)
    {
        Objects.Config_mode mode = config.modes[key];
        if (mode.invisible != true) {
            public_modes.Add(key);
        }
    }

    context.Response.StatusCode = 200;
    await context.Response.WriteAsJsonAsync(new Objects.endpoint_generic_data {
        ok = true,
        data = public_modes
    });
    return;
});

app.MapPost("/upload-chunk", async (HttpContext context) => {
    string asset_id = null;
    // This has the possibility to return the wrong configuration for the specified mode if it fails immediately before 'Config.Get(mode);' is done and could potentially give incorrect information in the catch {}, however this is unlikely, and very much worth it so that we can use config data in the catch {}.
    Objects.Config config = Config.Get(null);
try {
    Console.WriteLine("/upload-chunk");
    Objects.UploadSignedObject UploadSignedObject = JsonConvert.DeserializeObject<Objects.UploadSignedObject>(context.Request.Form["metadata"].ToString());

    string mode = UploadSignedObject.mode;
    config = Config.Get(mode);

    ConnectionMultiplexer conn_redis = null;
    IDatabase redis = null;
    if ((config.mode.database != null && config.mode.database.type == "redis") || (config.mode.authentication != null && config.mode.authentication.type == "redis")) {
        conn_redis = ConnectionMultiplexer.Connect(
        new ConfigurationOptions{
            EndPoints = { config.mode.database.hosts[0] },
            User = Environment.GetEnvironmentVariable(config.mode.database.username_env),
            Password = Environment.GetEnvironmentVariable(config.mode.database.password_env)
        });
        redis = conn_redis.GetDatabase();
    }

    asset_id = Security.file_name_sanitize(UploadSignedObject.asset_id);
    if (String.IsNullOrWhiteSpace(asset_id)) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("body.asset_id is null or whitespace."));
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

    // Chunk: A chunk allows the server to reconstruct the video bytes in the correct order during Post_processing. A "chunk" is any video packet. The client being able to upload chunks without waiting for another chunk to upload is essential for upload multithreading. A client can upload chunk 1, then chunk 3, then chunk 2, then chunk 8 and the server will reconstruct the video in the correct order. This prevents the video from becoming corrupt.
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
    int file_size = (int)file.Length;
    if (file_size == null || file_size == 0) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error("You cannot upload a file that is 0 bytes."));
        await context.Response.CompleteAsync();
        return;
    }

    Objects.Authentication_initalise authentication = await Authentication.initalise(context, config, redis, mode, asset_id, file_size, chunk);
    long already_uploaded = authentication.already_uplodaded;
    long max_size = authentication.max_size;

    if (already_uploaded == -1 || max_size == -1) {
        throw new Exception("already_uploaded or max_size wasn't set correctly.");
    }
    if (already_uploaded > max_size) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(Responses.error($"You cannot exceed authorised size of {max_size}"));
        await context.Response.CompleteAsync();
        return;
    }

    // Ensure the required directories exist.
    await Handler.setup(asset_id);

    // Write incoming chunk to disk.
    // TODO: move this to dedicated function.
    await using (FileStream aFile = new FileStream($"/tmp/user_ingest_before_reencoding/{asset_id}/chunks/{asset_id}_{chunk}.ingest_chunk", FileMode.Append, FileAccess.Write))
    await context.Request.Form.Files[0].CopyToAsync(aFile);

    Console.WriteLine("OK WHAT");
    Console.WriteLine(already_uploaded+file_size);
    Console.WriteLine(max_size);

    // For some storage types (e.g Redis) we need to update variables. This doesn't apply to the "remote_server" storage type.
    await Authentication.clock_in(config, redis, asset_id, already_uploaded, max_size);

    // Now that we've written the incoming chunk to disk, we'll check if the video has been uploaded based on the allocated size.
    if (already_uploaded+file_size == max_size) {
        // Video is fully uploaded and perfectly matches the allocated size. We'll begin video Post_processing.
        context.Response.StatusCode = 200;
        await context.Response.WriteAsJsonAsync(new Objects.errorD 
        {
            type = "processing"
        });
        await context.Response.CompleteAsync();

        await Post_processing.initalise(context, config, asset_id, file_type);
    } else {
        // Set 'uploading' and transfer progress as status.
        await Status.update(config, asset_id, "uploading", null, already_uploaded, file_size, null);

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
    // Report the causality.
    await Status.update(config, asset_id, "failed", null, -1, -1, null);
    
    context.Response.StatusCode = 500;
    await Handler.Fatal_error(context, asset_id);
    return;
}
});

app.Run();