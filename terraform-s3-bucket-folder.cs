using Microsoft.AspNetCore.StaticFiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace terraform_s3_bucket_folder
{
    static class TerraformResource
    {
        public static string Quote(string s) => '"' + s + '"';
        public static readonly string AwsS3BucketObject = Quote("aws_s3_bucket_object");
    }

    static class FileExtensionContentTypeProviderExtensions
    {
        public static string GetContentType(this FileExtensionContentTypeProvider fileExtensionContentTypeProvider, string fileName, string ifNone = "content/octet-stream")
        {
            if (!fileExtensionContentTypeProvider.TryGetContentType(fileName, out string mimeType))
            {
                mimeType = ifNone;
            }
            return mimeType;
        }
    }

    static class HashExtensions
    {
        // https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.md5
        public static string HashStringToHex(this HashAlgorithm hash, string input, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;

            // Convert the input string to a byte array and compute the hash.
            byte[] data = hash.ComputeHash(encoding.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var bucketName = args?.Skip(0).Take(1).FirstOrDefault();
            bucketName = bucketName ?? "${module.cdn.s3_bucket}";
            var readPath = args?.Skip(1).Take(1).FirstOrDefault();
            readPath = readPath ?? Environment.GetEnvironmentVariable("TF_VAR_build_path");
            readPath = $"{Environment.GetEnvironmentVariable("CODEBUILD_SRC_DIR")}{Path.DirectorySeparatorChar}{readPath}";

            Directory.CreateDirectory(readPath);

            var writePath = $"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}s3.{bucketName}.tf";

            Console.WriteLine($"bucketName {bucketName}");
            Console.WriteLine($"readPath {readPath}");
            Console.WriteLine($"writePath {writePath}");

            var mimeProvider = new FileExtensionContentTypeProvider();
            var hashProvider = MD5.Create();

            var files =
                from searchFile in Directory.EnumerateFiles(readPath, "*", SearchOption.AllDirectories)
                let filePath = Path.GetFullPath(searchFile)
                select filePath;

            var bucketObjects =
                from absPath in files
                let fileName = Path.GetFileName(absPath)
                let relPath = Path.GetRelativePath(readPath, absPath)
                //let hash = (uint)relPath.GetHashCode() // this is not consistent across platforms or even executions??
                let hash = hashProvider.HashStringToHex(relPath)
                let key = $"{relPath}"
                let mimeType = mimeProvider.GetContentType(fileName)
                let etag = "${md5(file(" + TerraformResource.Quote(absPath) + "))}"
                select new StringBuilder()
                .AppendLine($"  resource {TerraformResource.AwsS3BucketObject} {TerraformResource.Quote($"file-{hash}")} {{")
                .AppendLine($"  bucket = {TerraformResource.Quote(bucketName)}")
                .AppendLine($"  key = {TerraformResource.Quote(key)}")
                .AppendLine($"  source = {TerraformResource.Quote(absPath)}")
                .AppendLine($"  content_type = {TerraformResource.Quote(mimeType)}")
                .AppendLine($"  etag = {TerraformResource.Quote(etag)}")
                .AppendLine("}")
                .AppendLine()
                .ToString();

            var lines = bucketObjects.ToArray();

            File.WriteAllLines(writePath, lines);

            Console.WriteLine(File.ReadAllText(writePath));
        }
    }
}
