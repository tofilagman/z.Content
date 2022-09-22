using System.Collections.Generic;
using System.Threading.Tasks;

namespace z.Content
{
    public interface IContentService
    {
        Task Connect();
        Task<FileContent> PutFile(byte[] fileData, string fileName);
        Task<FileContent> UpdateFile(byte[] fileData, string fileName);
        Task<byte[]> GetFile(string fileName, string checkSum = null, bool throwIfNotExists = true);
        Task<List<FileContentWithDate>> GetList();
        Task DeleteFile(string fileName);
        Task<bool> FileExists(string filename);
    }
}
