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
                        await client.ConnectAsync();
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

            var cfname = $"{Utils.GenerateCode()}{ Path.GetExtension(fileName) }";
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

        public async Task<byte[]> GetFile(string fileName, string checkSum = null)
        {
            if (fileName == null)
                throw new Exception($"File: {fileName} does not exists");

            var mfile = Option.Type switch
            {
                FileSystemOptionType.Ftp => await FtpGetFile(fileName),
                FileSystemOptionType.Docker => await DockerGetFile(fileName),
                _ => throw new NotImplementedException(),
            };

            var chkSum = Utils.CheckSum(mfile);
            if (!string.IsNullOrEmpty(checkSum))
                if (chkSum != checkSum)
                    throw new Exception("File from the filesystem is modified and might breach the security of the system. aborted");

            return mfile;
        }

        public async Task<List<string>> GetList(string folder)
        {
            return Option.Type switch
            {
                FileSystemOptionType.Ftp => await FtpGetList(folder),
                FileSystemOptionType.Docker => await DockerGetList(folder),
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

        #region FTP 

        private async Task<FtpClient> FtpSetup()
        {
            var client = Option.Port.HasValue ? new FtpClient(Option.Address, Option.Port.Value, Option.Username, Option.Password)
                                              : new FtpClient(Option.Address, Option.Username, Option.Password);
            await client.ConnectAsync();
            return client;
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
                await client.UploadAsync(fileData, filename, FtpRemoteExists.Overwrite);
            });
        }

        private async Task<byte[]> FtpGetFile(string filename)
        {
            return await FtpProcess(async client =>
            {
                if (!await client.FileExistsAsync(filename))
                {
                    throw new Exception($"Requested file: {filename} does not exists");
                }
                return await client.DownloadAsync(filename, 0);
            });
        }

        private async Task<List<string>> FtpGetList(string folder)
        {
            return await FtpProcess(async client =>
            {
                var lst = await client.GetListingAsync(folder, FtpListOption.NameList);
                return lst.Select(x => x.Name).ToList();
            });
        }

        private async Task FtpDeleteFile(string fileName)
        {
            await FtpProcess(async client =>
            {
                if (await client.FileExistsAsync(fileName))
                {
                    await client.DeleteFileAsync(fileName);
                }
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

        private async Task<byte[]> DockerGetFile(string filename)
        {
            var pth = Path.Combine(Environment.ContentRootPath, Option.Address, filename);
            if (!File.Exists(pth))
                throw new Exception($"Requested file: {filename} does not exists");

            using var fs = File.OpenRead(pth);
            return await Task.FromResult(fs.ToByteArray());
        }

        private async Task<List<string>> DockerGetList(string folder)
        {
            var lst = Directory.GetFiles(Path.Combine(Environment.ContentRootPath, Option.Address, folder), "*.*", SearchOption.TopDirectoryOnly);
            return await Task.FromResult(lst.ToList());
        }

        private async Task DockerDeleteFile(string filename)
        {
            var pth = Path.Combine(Environment.ContentRootPath, Option.Address, filename);
            if (File.Exists(pth))
                File.Delete(pth);
            await Task.CompletedTask;
        }

        #endregion
    }
}
