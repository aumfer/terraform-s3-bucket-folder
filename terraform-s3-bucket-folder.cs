using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace terraform_s3_bucket_folder
{
    class Program
    {
        static void Main(string[] args)
        {
            var bucketName = "${module.cdn.s3_bucket}";
            var readPath = $"{Environment.GetEnvironmentVariable("CODEBUILD_SRC_DIR")}{Path.DirectorySeparatorChar}{Environment.GetEnvironmentVariable("TF_VAR_build_path")}";
            var bucketPath = Environment.GetEnvironmentVariable("TF_VAR_branch_name");
            Directory.CreateDirectory(readPath);

            var writePath = $"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}{bucketName}.tf";

            Console.WriteLine($"bucketName {bucketName}");
            Console.WriteLine($"readPath {readPath}");
            Console.WriteLine($"writePath {writePath}");

            var files =
                from searchFile in Directory.EnumerateFiles(readPath, "*", SearchOption.AllDirectories)
                let filePath = Path.GetFullPath(searchFile)
                select filePath;

            var bucketObjects =
                from absPath in files
                let fileName = Path.GetFileName(absPath)
                let hash = (uint)absPath.GetHashCode()
                let relPath = Path.GetRelativePath(readPath, absPath)
                let key = $"{bucketPath}{Path.DirectorySeparatorChar}{relPath}"
                select new StringBuilder()
                .AppendLine($"  resource {TerraformResource.AwsS3BucketObject} {TerraformResource.Quote($"file-{hash}")} {{")
                .AppendLine($"  bucket = {TerraformResource.Quote(bucketName)}")
                .AppendLine($"  key = {TerraformResource.Quote(key)}")
                .AppendLine($"  source = {TerraformResource.Quote(absPath)}")
                .AppendLine("}")
                .AppendLine()
                .ToString();

            var lines = bucketObjects.ToArray();

            File.WriteAllLines(writePath, lines);

            Console.WriteLine(File.ReadAllText(writePath));
        }

        static class TerraformResource
        {
            public static string Quote(string s) => '"' + s + '"';
            public static readonly string AwsS3BucketObject = Quote("aws_s3_bucket_object");
        }
    }
}
