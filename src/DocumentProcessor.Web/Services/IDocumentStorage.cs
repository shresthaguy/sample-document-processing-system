using System.IO;

namespace DocumentProcessor.Web.Services
{
    public interface IDocumentStorage
    {
        /// <summary>Saves the upload and returns its path relative to the storage root.</summary>
        string Save(Stream content, string fileName);

        Stream OpenRead(string storagePath);
    }
}
