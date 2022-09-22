using System;

namespace z.Content
{
    public class FileContent
    {
        /// <summary>
        /// Returns the actual file name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Returns the hash name of the file
        /// </summary>
        public string FileName { get; set; }
        public long Length { get; set; }
        public string CheckSum { get; set; }
    }

    public class FileContentWithDate : FileContent
    {
        public DateTime DateCreated { get; set; }
    }
}
