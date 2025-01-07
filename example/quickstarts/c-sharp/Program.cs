// Check HSTS.

using External_server;
using System.Text;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MySql.Data.MySqlClient;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
    policy  =>
    {
        policy.WithOrigins("example.com")
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

var app = builder.Build();
// app.UseCors(MyAllowSpecificOrigins);
app.UseExceptionHandler("/Error");
app.UseHttpsRedirection();
app.UseCors();
app.UseRouting();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

string connectionString = "Server=localhost;Database=interstellar_test;User=example-user;Password=my_cool_secret;";
MySqlConnection connection = new MySqlConnection(connectionString);

app.MapGet("/error", async (HttpContext context) => {
    await Responses.error(context, 500, "Something went wrong.");
});

app.MapPost("/chunk/authorisation", async (HttpContext context) => {
try {
    await connection.OpenAsync();
    using var body_reader = new StreamReader(context.Request.Body);
    var request_body = await body_reader.ReadToEndAsync();

    Objects.request_body_chunk_authorisation body = JsonConvert.DeserializeObject<Objects.request_body_chunk_authorisation>(request_body);

    string asset_id = body.asset_id;
    if (String.IsNullOrWhiteSpace(asset_id)) {
        await Responses.error(context, 400, "body.asset_id is null or whitespace.");
        return;
    }
    string mode = body.mode;
    if (String.IsNullOrWhiteSpace(mode)) {
        await Responses.error(context, 400, "body.mode is null or whitespace.");
        return;
    }
    long size = body.size;
    if (size == -1) {
        await Responses.error(context, 400, "body.size is null or whitespace.");
        return;
    }
    long chunk = body.chunk;
    if (chunk == -1) {
        await Responses.error(context, 400, "body.chunk is null or whitespace.");
        return;
    }
    // string authorisation = body.authorisation.ToString();
    // if (String.IsNullOrWhiteSpace(authorisation)) {
    //     return Responses.error(context, 400, "body.authorisation is null or whitespace.");
    // }
    
    string query = "SELECT asset_id, status, bytes_uploaded, bytes_max FROM videos WHERE asset_id=@asset_id";
    MySqlCommand command = new MySqlCommand(query, connection);
    command.Parameters.AddWithValue("asset_id", asset_id);
    MySqlDataReader reader = await Sql.query(command);
    if (reader == null) {
        await Responses.error(context, 500, "asset_id is invalid.");
    }
    Console.WriteLine($"ID: {reader["asset_id"]} {reader["status"]} {reader["bytes_uploaded"]} {reader["bytes_max"]}");

    if (string.IsNullOrWhiteSpace(reader["bytes_uploaded"].ToString())) {
        throw new Exception("bytes_uploaded is null or whitespace.");
    }
    if (string.IsNullOrWhiteSpace(reader["bytes_max"].ToString())) {
        throw new Exception("bytes_max is null or whitespace.");
    }
    long uploaded = long.Parse(reader["bytes_uploaded"].ToString());
    long max_size = long.Parse(reader["bytes_max"].ToString());

    await reader.CloseAsync();

    if (uploaded > size || uploaded+size > size) {
    } else {
        MySqlCommand update_bytes = new MySqlCommand("UPDATE videos SET bytes_uploaded=@bytes_uploaded WHERE asset_id=@asset_id", connection);
        update_bytes.Parameters.AddWithValue("bytes_uploaded", uploaded+size);
        update_bytes.Parameters.AddWithValue("asset_id", asset_id);
        await Sql.query(update_bytes);
    }

    await connection.CloseAsync();
    await Responses.handle(context, 200, new Objects.response_body_chunk_authorisation {
        asset_id = asset_id,
        uploaded = uploaded,
        max_size = max_size
    });
} catch (WebException ex)
{
    Console.WriteLine("BAD ERROR");
    Console.WriteLine(ex);
    Console.WriteLine("BAD ERROR");
    await Responses.error(context, 500, "Something went wrong.");
}
});

app.MapPost("/get-upload-urls", async (HttpContext context) => {
try {
    using var body_reader = new StreamReader(context.Request.Body);
    var request_body = await body_reader.ReadToEndAsync();

    Objects.request_body_get_upload_urls body = JsonConvert.DeserializeObject<Objects.request_body_get_upload_urls>(request_body);

    string asset_id = body.asset_id;
    if (String.IsNullOrWhiteSpace(asset_id)) {
        await Responses.error(context, 400, "body.asset_id is null or whitespace.");
        return;
    }
    string authorisation = body.authorisation.ToString();
    if (String.IsNullOrWhiteSpace(authorisation)) {
        await Responses.error(context, 400, "body.authorisation is null or whitespace.");
        return;
    }

    List<S3.ObjectKey_URL> presigned_urls = S3.Generate_Presigned_URL(asset_id, S3.Get_S3_client(), Environment.GetEnvironmentVariable("s3_bucket"), body.object_keys);
    await Responses.handle(context, 200, new Objects.response_body_get_upload_urls {
        asset_id = asset_id,
        object_keys = presigned_urls
    });
} catch (WebException ex)
{
    Console.WriteLine(ex);
    await Responses.error(context, 500, "Something went wrong.");
}
});

app.MapPost("/status/update", async (HttpContext context) => {
try {
    await connection.OpenAsync();
    using var body_reader = new StreamReader(context.Request.Body);
    var request_body = await body_reader.ReadToEndAsync();

    Objects.request_body_status_update body = JsonConvert.DeserializeObject<Objects.request_body_status_update>(request_body);

    string asset_id = body.asset_id;
    if (String.IsNullOrWhiteSpace(asset_id)) {
        await Responses.error(context, 400, "body.asset_id is null or whitespace.");
        return;
    }
    string section = body.section;
    if (String.IsNullOrWhiteSpace(section)) {
        await Responses.error(context, 400, "body.section is null or whitespace.");
        return;
    }
    long stage = body.stage;
    if (stage == -1) {
        await Responses.error(context, 400, "body.stage is null or whitespace.");
        return;
    }
    long out_of = body.out_of;
    if (out_of == -1) {
        await Responses.error(context, 400, "body.out_of is null or whitespace.");
        return;
    }
    // string authorisation = body.authorisation.ToString();
    // if (String.IsNullOrWhiteSpace(authorisation)) {
    //     return Responses.error(context, 400, "body.authorisation is null or whitespace.");
    // }

    MySqlCommand update_bytes = new MySqlCommand("UPDATE videos SET section=@section, stage=@stage, out_of=@out_of WHERE asset_id=@asset_id", connection);
    update_bytes.Parameters.AddWithValue("section", section);
    update_bytes.Parameters.AddWithValue("stage", stage);
    update_bytes.Parameters.AddWithValue("out_of", out_of);
    update_bytes.Parameters.AddWithValue("asset_id", asset_id);
    await Sql.query(update_bytes);

    await Responses.handle(context, 200, null);
} catch (WebException ex)
{
    Console.WriteLine(ex);
    await Responses.error(context, 500, "Something went wrong.");
}
});

app.Run();