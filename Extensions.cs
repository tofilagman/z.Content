using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using z.Data;

namespace z.Content
{
    public static class Extensions
    {
        public static async Task<byte[]> ToByteArray(this IFormFile formFile)
        {
            using var ms = new MemoryStream();
            await formFile.CopyToAsync(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms.ToByteArray();
        }
         
        public static string GetContentType(this string extension, string alt = "")
        {
            return new ContentType().GetContentType(extension, alt);
        }
    }
}
