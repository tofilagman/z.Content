using FluentFTP;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using z.Data;

namespace z.Content
{
    public class ContentService : IContentService
    {
        private readonly FileSystemOption Option;
        private readonly IHostEnvironment Environment;
        private readonly ILogger Logger;

        public ContentService(FileSystemOption option, IHostEnvironment environment, ILogger<ContentService> logger)
        {
            Option = option;
            Environment = environment;
            Logger = logger;
        }

        public async Task Connect()
        {
            Logger.LogInformation($"Testing {Option.Type} connection");
            switch (Option.Type)
            {
                case FileSystemOptionType.Ftp:
                    await FtpProcess(async client =>
                    {
                        client.Connect();
                        await Task.CompletedTask;
                    });
                    break;
                case FileSystemOptionType.Docker:
                    var folder = Path.Combine(Environment.ContentRootPath, Option.Address);
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    break;
                default:
                    throw new NotImplementedException();
            }
            Logger.LogInformation($"{Option.Type} connection success");
        }

        public async Task<FileContent> PutFile(byte[] fileData, string fileName)
        {
            if (fileData.Length <= 0)
                throw new Exception("File length is zero");

            var cfname = $"{Utils.GenerateCode()}{Path.GetExtension(fileName)}";
            var checkSum = Utils.CheckSum(fileData);

            var content = new FileContent
            {
                Name = fileName,
                FileName = cfname,
                Length = fileData.Length,
                CheckSum = checkSum
            };

            switch (Option.Type)
            {
                case FileSystemOptionType.Ftp:
                    await FtpPutFile(fileData, cfname);
                    break;
                case FileSystemOptionType.Docker:
                    await DockerPutFile(fileData, cfname);
                    break;
                default:
                    throw new NotImplementedException();
            }

            return content;
        }

        public async Task<FileContent> UpdateFile(byte[] fileData, string fileName)
        {
            if (fileData.Length <= 0)
                throw new Exception("File length is zero");

            var checkSum = Utils.CheckSum(fileData);

            var content = new FileContent
            {
                FileName = fileName,
                Length = fileData.Length,
                CheckSum = checkSum
            };

            switch (Option.Type)
            {
                case FileSystemOptionType.Ftp:
                    await FtpPutFile(fileData, fileName);
                    break;
                case FileSystemOptionType.Docker:
                    await DockerPutFile(fileData, fileName);
                    break;
                default:
                    throw new NotImplementedException();
            }

            return content;
        }

        public async Task<byte[]> GetFile(string fileName, string checkSum = null, bool throwIfNotExists = true)
        {
            if (fileName == null)
                throw new Exception($"File: {fileName} does not exists");

            var mfile = Option.Type switch
            {
                FileSystemOptionType.Ftp => await FtpGetFile(fileName, throwIfNotExists),
                FileSystemOptionType.Docker => await DockerGetFile(fileName, throwIfNotExists),
                _ => throw new NotImplementedException(),
            };

            if (mfile.Length == 0)
                return null;

            var chkSum = Utils.CheckSum(mfile);
            if (!string.IsNullOrEmpty(checkSum))
                if (chkSum != checkSum)
                    throw new Exception("File from the filesystem is modified and might breach the security of the system. aborted");

            return mfile;
        }

        public async Task<string> GetFileBase64(string fileName, string checkSum = null, bool throwIfNotExists = true)
        {
            var data = await GetFile(fileName, checkSum, throwIfNotExists);
            var base64 = Convert.ToBase64String(data);
            var contentType = Path.GetExtension(fileName).GetContentType();

            return $"data:{contentType};base64,{base64}";
        }

        public async Task<List<FileContentWithDate>> GetList()
        {
            return Option.Type switch
            {
                FileSystemOptionType.Ftp => await FtpGetList(),
                FileSystemOptionType.Docker => await DockerGetList(),
                _ => throw new NotImplementedException(),
            };
        }

        public async Task DeleteFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return;

            switch (Option.Type)
            {
                case FileSystemOptionType.Ftp:
                    await FtpDeleteFile(fileName);
                    break;
                case FileSystemOptionType.Docker:
                    await DockerDeleteFile(fileName);
                    break;
                default:
                    throw new NotImplementedException();
            };
        }

        public async Task<bool> FileExists(string fileName)
        {
            return Option.Type switch
            {
                FileSystemOptionType.Ftp => await FtpFileExists(fileName),
                FileSystemOptionType.Docker => await DockerFileExists(fileName),
                _ => throw new NotImplementedException(),
            };
        }

        #region FTP 

        private async Task<FtpClient> FtpSetup()
        {
            var client = Option.Port.HasValue ? new FtpClient(Option.Address, Option.Username, Option.Password, Option.Port.Value)
                                              : new FtpClient(Option.Address, Option.Username, Option.Password);
            client.Connect();

            return await Task.FromResult(client);
        }

        private async Task<T> FtpProcess<T>(Func<IFtpClient, Task<T>> action)
        {
            IFtpClient client = null;
            try
            {
                client = await FtpSetup();
                return await action(client);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                client?.Dispose();
            }
        }

        private async Task FtpProcess(Func<IFtpClient, Task> action)
        {
            await FtpProcess<object>(async client =>
            {
                await action(client);
                return null;
            });
        }

        private async Task FtpPutFile(byte[] fileData, string filename)
        {
            await FtpProcess(async client =>
            {
                client.UploadBytes(fileData, filename, FtpRemoteExists.Overwrite);
                await Task.CompletedTask;
            });
        }

        private async Task<byte[]> FtpGetFile(string filename, bool throwIfNotExists)
        {
            return await FtpProcess(async client =>
            {
                if (!client.FileExists(filename))
                {
                    if (throwIfNotExists)
                        throw new Exception($"Requested file: {filename} does not exists");
                    else
                        return await Task.FromResult(Array.Empty<byte>());
                }

                if (client.DownloadBytes(out var bts, filename))
                    return await Task.FromResult(bts);
                return await Task.FromResult(Array.Empty<byte>());
            });
        }

        private async Task<List<FileContentWithDate>> FtpGetList()
        {
            return await FtpProcess(async client =>
            {
                var lst = client.GetListing("/", FtpListOption.NameList | FtpListOption.SizeModify);
                return await Task.FromResult(lst.Select(x => new FileContentWithDate
                {
                    FileName = x.Name,
                    Length = x.Size,
                    DateCreated = x.Modified
                }).ToList());
            });
        }

        private async Task FtpDeleteFile(string fileName)
        {
            await FtpProcess(async client =>
            {
                if (client.FileExists(fileName))
                    client.DeleteFile(fileName);
                await Task.CompletedTask;
            });
        }

        private async Task<bool> FtpFileExists(string filename)
        {
            return await FtpProcess(async client =>
            {
                return await Task.FromResult(client.FileExists(filename));
            });
        }

        #endregion

        #region Docker

        private async Task DockerPutFile(byte[] fileData, string filename)
        {
            var pth = Path.Combine(Environment.ContentRootPath, Option.Address, filename);
            var folder = Path.GetDirectoryName(pth);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(pth, fileData);
        }

        private async Task<byte[]> DockerGetFile(string filename, bool throwIfNotExists)
        {
            var pth = Path.Combine(Environment.ContentRootPath, Option.Address, filename);
            if (!File.Exists(pth))
            {
                if (throwIfNotExists)
                    throw new Exception($"Requested file: {filename} does not exists");
                else
                    return Array.Empty<byte>();
            }

            using var fs = File.OpenRead(pth);
            return await Task.FromResult(fs.ToByteArray());
        }

        private async Task<List<FileContentWithDate>> DockerGetList()
        {
            var npath = Path.Combine(Environment.ContentRootPath, Option.Address);
            var lst = Directory.GetFiles(npath, "*.*", SearchOption.TopDirectoryOnly);

            var kd = new List<FileContentWithDate>();
            foreach (var s in lst)
            {
                var fld = new FileInfo(Path.Combine(npath, s));
                kd.Add(new FileContentWithDate
                {
                    FileName = fld.Name,
                    Length = fld.Length,
                    DateCreated = fld.CreationTime
                });
            }

            return await Task.FromResult(kd);
        }

        private async Task DockerDeleteFile(string filename)
        {
            var pth = Path.Combine(Environment.ContentRootPath, Option.Address, filename);
            if (File.Exists(pth))
                File.Delete(pth);
            await Task.CompletedTask;
        }

        private async Task<bool> DockerFileExists(string filename)
        {
            var pth = Path.Combine(Environment.ContentRootPath, Option.Address, filename);
            return await Task.FromResult(File.Exists(pth));
        }

        #endregion
    }
}
