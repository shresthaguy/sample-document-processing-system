using System;
using System.IO;
using DocumentProcessor.Web.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace DocumentProcessor.Web.Services
{
    /// <summary>
    /// Stores uploads on the local file system under a date-partitioned path below
    /// App_Data, which IIS will not serve directly.
    /// </summary>
    public class FileSystemDocumentStorage : IDocumentStorage
    {
        private readonly string _rootPath;

        public FileSystemDocumentStorage(IWebHostEnvironment environment)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            _rootPath = ResolveRootPath(environment.ContentRootPath, AppSettings.StorageRootPath);
        }

        /// <summary>
        /// Resolves the configured storage root against the content root (not wwwroot), so
        /// uploads are never served as static files. Accepts the legacy "~/App_Data/uploads"
        /// virtual form, a content-root-relative path, or an absolute path.
        /// </summary>
        private static string ResolveRootPath(string contentRootPath, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                configuredPath = "App_Data/uploads";
            }

            if (configuredPath.StartsWith("~", StringComparison.Ordinal))
            {
                configuredPath = configuredPath.Substring(1).TrimStart('/', '\\');
            }
            else if (Path.IsPathFullyQualified(configuredPath))
            {
                return configuredPath;
            }

            var relative = configuredPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(contentRootPath, relative));
        }

        public string Save(Stream content, string fileName)
        {
            var now = DateTimeOffset.UtcNow;

            var uniqueName = Path.GetFileNameWithoutExtension(fileName)
                + "_" + now.ToString("yyyyMMddHHmmssfff")
                + Path.GetExtension(fileName);

            var relativePath = Path.Combine(
                now.Year.ToString("D4"),
                now.Month.ToString("D2"),
                now.Day.ToString("D2"),
                uniqueName);

            var fullPath = Path.Combine(_rootPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            using (var file = File.Create(fullPath))
            {
                content.CopyTo(file);
            }

            return relativePath;
        }

        public Stream OpenRead(string storagePath)
        {
            return File.OpenRead(Path.Combine(_rootPath, storagePath));
        }
    }
}
