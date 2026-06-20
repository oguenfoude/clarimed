using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FocusMed.Data;
using FocusMed.Data.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FocusMed.Dashboard.Api;

public static class QuickAssignEndpoints
{
    public static void MapQuickAssignEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/quickassign");

        // Loopback-only filter
        group.AddEndpointFilter(async (context, next) =>
        {
            var ip = context.HttpContext.Connection.RemoteIpAddress;
            if (ip == null || !IPAddress.IsLoopback(ip))
            {
                return Results.Forbid();
            }
            return await next(context);
        });

        // 1. GET /api/quickassign/pending
        group.MapGet("/pending", async (FocusMedDbContext db) =>
        {
            var pending = await db.Documents
                .AsNoTracking()
                .Where(d => d.StudyId == null && d.Status == DocumentStatus.Converted)
                .OrderBy(d => d.ReceivedAt)
                .Select(d => new
                {
                    id = d.Id,
                    name = d.OriginalFileName,
                    receivedAt = d.ReceivedAt
                })
                .ToListAsync();

            return Results.Ok(pending);
        });

        // 2. GET /api/quickassign/unassigned-studies?search=...
        group.MapGet("/unassigned-studies", async (string? search, FocusMedDbContext db) =>
        {
            var query = db.Studies
                .AsNoTracking()
                .Include(s => s.Patient)
                .Where(s => !s.IsDeleted && !db.Documents.Any(d => d.StudyId == s.Id));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchClean = search.Trim().ToLower();
                query = query.Where(s =>
                    s.Patient.Name.ToLower().Contains(searchClean) ||
                    s.Patient.PatientId.ToLower().Contains(searchClean) ||
                    s.AccessionNumber.ToLower().Contains(searchClean));
            }

            var studies = await query
                .OrderByDescending(s => s.StudyDate ?? s.CreatedAt)
                .Take(50)
                .Select(s => new
                {
                    studyId = s.Id,
                    patientName = s.Patient.Name,
                    patientCode = s.Patient.PatientId,
                    modality = s.Modality,
                    studyDate = s.StudyDate
                })
                .ToListAsync();

            return Results.Ok(studies);
        });

        // 3. POST /api/quickassign/{id}/assign
        group.MapPost("/{id:int}/assign", async (int id, [FromBody] AssignRequest req, FocusMedDbContext db) =>
        {
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return Results.NotFound("Document not found.");

            var studyExists = await db.Studies.AnyAsync(s => s.Id == req.StudyId);
            if (!studyExists) return Results.NotFound("Study not found.");

            doc.StudyId = req.StudyId;
            await db.SaveChangesAsync();

            return Results.Ok(new { success = true, studyId = req.StudyId });
        });

        // 3.5 DELETE /api/quickassign/{id}
        group.MapDelete("/{id:int}", async (int id, FocusMedDbContext db) =>
        {
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return Results.NotFound("Document not found.");

            db.Documents.Remove(doc);
            await db.SaveChangesAsync();

            return Results.Ok(new { success = true });
        });

        // 4. POST /api/quickassign/{id}/new-patient-and-assign
        group.MapPost("/{id:int}/new-patient-and-assign", async (int id, [FromBody] NewPatientAssignRequest req, FocusMedDbContext db) =>
        {
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return Results.NotFound("Document not found.");

            using var transaction = await db.Database.BeginTransactionAsync();

            try
            {
                // Find or create patient
                Patient? patient = null;
                if (!string.IsNullOrWhiteSpace(req.PatientCode))
                {
                    patient = await db.Patients.FirstOrDefaultAsync(p => p.PatientId == req.PatientCode);
                }

                if (patient == null)
                {
                    patient = new Patient
                    {
                        Name = req.Name,
                        PatientId = !string.IsNullOrWhiteSpace(req.PatientCode) ? req.PatientCode : $"MANUAL-{DateTime.UtcNow:yyyyMMddHHmmss}",
                        CreatedAt = DateTime.UtcNow
                    };
                    db.Patients.Add(patient);
                    await db.SaveChangesAsync(); // save to get Id
                }

                // Create Manual Study
                var study = new Study
                {
                    PatientId = patient.Id,
                    StudyInstanceUid = $"MANUAL-{Guid.NewGuid()}",
                    Modality = "Manual",
                    StudyDate = DateTime.UtcNow,
                    AccessionNumber = $"ACC-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    CreatedAt = DateTime.UtcNow,
                    Status = StudyStatus.Complete // Manual studies are instantly complete
                };
                db.Studies.Add(study);
                await db.SaveChangesAsync(); // save to get Id

                // Assign document
                doc.StudyId = study.Id;
                await db.SaveChangesAsync();

                await transaction.CommitAsync();

                return Results.Ok(new { success = true, studyId = study.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Results.Problem(ex.Message);
            }
        });
    }
}

public class AssignRequest
{
    public int StudyId { get; set; }
}

public class NewPatientAssignRequest
{
    public string Name { get; set; } = string.Empty;
    public string? PatientCode { get; set; }
}
