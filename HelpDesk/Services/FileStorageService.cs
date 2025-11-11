using Amazon.S3;
using Amazon.S3.Model;
using HelpDesk.Models;
using HelpDesk.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mime;

namespace HelpDesk.Services
{
    public class FileStorageService
    {
        private readonly IAmazonS3 _s3;
        private readonly S3Options _opts;

        public FileStorageService(IAmazonS3 s3, IOptions<S3Options> opts)
            => (_s3, _opts) = (s3, opts.Value);

        private static string UrlEncodePerSegment(string key)
        {
            var segments = key.Split('/', StringSplitOptions.RemoveEmptyEntries)
                              .Select(WebUtility.UrlEncode);
            return string.Join('/', segments);
        }

        public async Task<(string Key, string Url)> SaveAsync(
            IFormFile file,
            string key,                      
            CancellationToken ct = default)
        {
            using var stream = file.OpenReadStream();

            var put = new PutObjectRequest
            {
                BucketName = _opts.Bucket,
                Key = key,                   
                InputStream = stream,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            
            put.Metadata["original-filename"] = Path.GetFileName(file.FileName);

            var resp = await _s3.PutObjectAsync(put, ct);
            if ((int)resp.HttpStatusCode >= 300)
                throw new InvalidOperationException("Falha ao enviar ao S3.");

            
            var encodedKey = UrlEncodePerSegment(key);
            var baseUrl = _opts.PublicBaseUrl.TrimEnd('/');
            var url = $"{baseUrl}/{encodedKey}";

            return (key, url);
        }

        public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
        {
            try
            {
                var resp = await _s3.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _opts.Bucket,
                    Key = key
                }, ct);

                return (int)resp.HttpStatusCode is >= 200 and < 300;
            }
            catch
            {
                return false;
            }
        }


    }
}
