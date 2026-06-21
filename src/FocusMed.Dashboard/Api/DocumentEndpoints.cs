using FocusMed.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace FocusMed.Dashboard.Api;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/documents");

        // GET /documents/view/{id}
        group.MapGet("/view/{id:int}", async (int id, FocusMedDbContext db) =>
        {
            var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
            
            if (doc == null || string.IsNullOrEmpty(doc.PdfFilePath))
            {
                return Results.NotFound("Document not found.");
            }

            if (!File.Exists(doc.PdfFilePath))
            {
                return Results.NotFound("The physical PDF file could not be found.");
            }

            // DO NOT use "application/octet-stream" or add a FileDownloadName.
            // Using "application/pdf" without a download name will display it inline in the browser/iframe.
            return Results.File(doc.PdfFilePath, "application/pdf");
        });
    }
}
