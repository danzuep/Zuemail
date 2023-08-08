using MailKit;
using System;
using System.IO;
using System.IO.Abstractions;

namespace Zuemail.Core.Models
{
    public sealed class ProtocolLoggerOptions
    {
        public const string SectionName = "ProtocolLogger";

        public const string DefaultTimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";
        public const string DefaultSmtpLogFilePath = "Logs/SmtpClient.txt";
        public const string DefaultImapLogFilePath = "Logs/ImapClient.txt";
        private static readonly string MockFileSystemName = "MockFileSystem";

        /// <summary>
        /// File to write to.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Whether to append to an existing file (if there is one) or create a new one.
        /// </summary>
        public bool AppendToExisting { get; set; } = false;

        /// <summary>
        /// The length of time the file-writing text queue will idle for when empty.
        /// </summary>
        public ushort FileWriteMaxDelayMs { get; set; } = 100;

        public string TimestampFormat { get; set; } = null;

        public string ServerPrefix { get; set; } = "S: ";

        public string ClientPrefix { get; set; } = "C: ";

        public IProtocolLogger CreateProtocolLogger(IFileSystem fileSystem = null)
        {
            IProtocolLogger protocolLogger = null;
            if (FilePath?.Equals("console", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                protocolLogger = new ProtocolLogger(Console.OpenStandardError());
            }
            else if (!string.IsNullOrWhiteSpace(FilePath))
            {
                bool isMockFileSystem = fileSystem != null &&
                    fileSystem.GetType().Name == MockFileSystemName;
                if (fileSystem == null)
                    fileSystem = new FileSystem();
                var directoryName = fileSystem.Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrWhiteSpace(directoryName))
                    fileSystem.Directory.CreateDirectory(directoryName);
                if (isMockFileSystem)
                    protocolLogger = new ProtocolLogger(Stream.Null);
                else
                    protocolLogger = new ProtocolLogger(FilePath, AppendToExisting);
            }
            return protocolLogger;
        }

        public ProtocolLoggerOptions Copy() => MemberwiseClone() as ProtocolLoggerOptions;

        public override string ToString() => FilePath;
    }
}
