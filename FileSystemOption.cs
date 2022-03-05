using System;
using System.Linq;

namespace z.Content
{
    public class FileSystemOption
    {
        public FileSystemOptionType Type { get; set; }
        public int? Port { get; set; }
        public string Address { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        /// <summary>
        /// Engine=1;Path=<path>;Relative=true
        /// </summary>
        /// <param name="FileConfig"></param>
        public void Load(string fileEnv)
        {
            var gf = fileEnv.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in gf)
            {
                var val = item.Split('=').Select(x => x.Trim()).ToArray();
                switch (val[0])
                {
                    case nameof(Type):
                        if (Enum.TryParse(val[1], out FileSystemOptionType type))
                            Type = type;
                        else
                            throw new Exception($"File type: { val[1] } not found");
                        break;
                    case nameof(Port):
                        if (int.TryParse(val[1], out int port))
                            Port = port;
                        break;
                    case nameof(Address):
                        Address = val[1];
                        break;
                    case nameof(Username):
                        Username = val[1];
                        break;
                    case nameof(Password):
                        Password = val[1];
                        break;
                }
            }
        }
    }

}
