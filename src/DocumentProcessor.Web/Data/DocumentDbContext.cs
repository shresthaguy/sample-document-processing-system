using Microsoft.EntityFrameworkCore;
using DocumentProcessor.Web.Models;

namespace DocumentProcessor.Web.Data
{
    public class DocumentDbContext : DbContext
    {
        public DocumentDbContext(DbContextOptions<DocumentDbContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }
    }
}
