using System.Collections.Generic;
using System.Threading.Tasks;

namespace z.Content
{
    public interface IContentService
    {
        Task Connect();
        Task<FileContent> PutFile(byte[] fileData, string fileName);
        Task<FileContent> UpdateFile(byte[] fileData, string fileName);
        Task<byte[]> GetFile(string fileName, string checkSum = null);
        Task<List<string>> GetList(string folder);
        Task DeleteFile(string fileName);
    }
}
