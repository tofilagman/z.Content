

## z.Content

a library that manage contents of docker, filesystem and Ftp in a single code base

[![NuGet Version and Downloads count](https://buildstats.info/nuget/z.Content?includePreReleases=true)](https://www.nuget.org/packages/z.Content/)

File Types:
	- Docker/FileSystem: 1
	- FTP: 2

Initialize 
```c#

services.AddContent((provider, options) =>
    {
        configuration.GetSection("FileSystem").Bind(options);
        var fileEnv = Environment.GetEnvironmentVariable("FileString");
        if (!string.IsNullOrEmpty(fileEnv))
            options.Load(fileEnv);
    }); 
```
appsettings.json
```json

 "FileSystem": {
    "Type": 1,
    "Address": "AutoGen/FileData",
    "Port": null,
    "Username": null,
    "Password": null,
    "Comments": [
      "1 = Docker, AutoGen/FileData",
      "2 = Ftp"
    ]
  }

```
Environment Variable
```bash 
-e FileString="Type=1;Address=AutoGen/FileData"
# or
-e FileString="Type=2;Address=ftp.source.com;Username=ftpusername;Password=password01" 
```
  
Code Implementation

```c#
  
public class AppController 
    { 
        private readonly IContentService Content;

        public AppController(IContentService content) 
        {
            Content = content;
        }
  
        [HttpPost] 
        public async Task<IActionResult> Upload(IFormFile upload)
        { 
            FileContent content = await Content.PutFile(await token.File.ToByteArray(), 
                        token.File.FileName);
            ...
            return Ok();
        }

        [HttpGet] 
        public async Task<IActionResult> Get(string filename, string checkSum)
        { 
            var bt = await Content.GetFile(fileName, checkSum);
            var base64 = Convert.ToBase64String(bt);
            var contentType = Path.GetExtension(fileName).GetContentType();

            return Ok($"data:{contentType};base64,{base64}");
        }
    }
```