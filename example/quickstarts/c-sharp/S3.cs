using System;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Interstellar;

public class S3
{
    public class ObjectKey_URL {
        public string object_key { get; set; }
        public string url { get; set; }
    }

    public static IAmazonS3 Get_S3_client()
    {
        // Specify the AWS Region of your Amazon S3 bucket. If it is
        // different from the Region defined for the default user,
        // pass the Region to the constructor for the client. For
        // example: new AmazonS3Client(RegionEndpoint.USEast1);

        // If using the Region us-east-1, and server-side encryption with AWS KMS, you must specify Signature Version 4.
        // Region us-east-1 defaults to Signature Version 2 unless explicitly set to Version 4 as shown below.
        // For more details, see https://docs.aws.amazon.com/AmazonS3/latest/userguide/UsingAWSSDK.html#specify-signature-version
        // and https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/Amazon/TAWSConfigsS3.html
    
        AWSConfigsS3.UseSignatureVersion4 = true;
        var awsAccessKeyId = Environment.GetEnvironmentVariable("s3_access_key_env");
        var awsSecretAccessKey = Environment.GetEnvironmentVariable("s3_secret_access_key_env");
        IAmazonS3 s3_client = new AmazonS3Client(awsAccessKeyId, awsSecretAccessKey);
        return s3_client;
    }

    /// <summary>
    /// Generate a presigned URL that can be used to access the file named
    /// in the objectKey parameter for the amount of time specified in the
    /// duration parameter.
    /// </summary>
    /// <param name="client">An initialized S3 client object used to call
    /// the GetPresignedUrl method.</param>
    /// <param name="bucketName">The name of the S3 bucket containing the
    /// object for which to create the presigned URL.</param>
    /// <param name="objectKey">The name of the object to access with the
    /// presigned URL.</param>
    /// <param name="duration">The length of time for which the presigned
    /// URL will be valid.</param>
    /// <returns>A string representing the generated presigned URL.</returns>
    /// 

    public static List<ObjectKey_URL> Generate_Presigned_URL(string asset_id, IAmazonS3 client, string bucket_name, string[] object_keys, double duration = 43200)
    {
        // TODO: We should call the S3 server and ensure the existing objects do not exist and add an overwrite flag to the upload-chunk. This is so if an admin messes up their S3 pathing in the config we don't potentially allow an attacker to tamper with data stores in the S3 bucket (e.g. download files).
        List<ObjectKey_URL> presigned_urls = new List<ObjectKey_URL>();
        foreach (string object_key in object_keys) {
            string url_string = string.Empty;
            try
            {
                var request = new GetPreSignedUrlRequest()
                {
                    BucketName = bucket_name,
                    Key = object_key,
                    Expires = DateTime.UtcNow.AddHours(duration),
                };
                url_string = client.GetPreSignedURL(request);

                presigned_urls.Add(new ObjectKey_URL {
                    object_key = $"{asset_id}/{object_key}",
                    url = url_string
                });
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error:'{ex.Message}'");
            }
        }

        return presigned_urls;
    }
}