using System;

namespace DocumentProcessor.Web.Components.Controls.Controls
{
    /// <summary>
    /// Payload of the DocumentList component's ViewSummaryRequested and DeleteRequested callbacks.
    /// Carried over unchanged from the Web Forms DocumentList user control.
    /// </summary>
    public class DocumentEventArgs : EventArgs
    {
        public DocumentEventArgs(Guid documentId)
        {
            DocumentId = documentId;
        }

        public Guid DocumentId { get; private set; }
    }
}
