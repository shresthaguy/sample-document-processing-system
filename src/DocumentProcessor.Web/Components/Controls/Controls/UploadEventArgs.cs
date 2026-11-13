using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Components.Forms;

namespace DocumentProcessor.Web.Components.Controls.Controls
{
    /// <summary>
    /// Raised by <see cref="DocumentUploader"/> once the picked files have passed the size and
    /// extension checks. Replaces the Web Forms UploadEventArgs that carried HttpPostedFile.
    /// </summary>
    public class UploadEventArgs : EventArgs
    {
        public UploadEventArgs(IList<IBrowserFile> files, long maxAllowedSize)
        {
            Files = files;
            MaxAllowedSize = maxAllowedSize;
        }

        public IList<IBrowserFile> Files { get; private set; }

        /// <summary>Storage.MaxFileSizeMegabytes converted to bytes.</summary>
        public long MaxAllowedSize { get; private set; }

        /// <summary>
        /// Opens the upload for reading with the configured size cap. Replaces
        /// HttpPostedFile.InputStream; the stream must be read while the circuit is alive.
        /// </summary>
        public Stream OpenReadStream(IBrowserFile file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            return file.OpenReadStream(maxAllowedSize: MaxAllowedSize);
        }
    }
}
