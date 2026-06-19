using System;
using Microsoft.Data.Sqlite;

namespace Seeder;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("FocusMed Seeder - Injecting Fake Data...");
        
        string dbPath = @"D:\FocusMed\src\FocusMed.Worker\db\focusmed.db";
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            var random = new Random();
            string[] firstNames = { "Jean", "Pierre", "Marie", "Sophie", "Luc", "Emma", "Thomas", "Julie", "Nicolas", "Camille" };
            string[] lastNames = { "Dupont", "Martin", "Bernard", "Thomas", "Petit", "Robert", "Richard", "Durand", "Dubois", "Moreau" };
            string[] modalities = { "CT", "MR", "US", "CR", "DX" };
            string[] descriptions = { "CHEST", "BRAIN", "ABDOMEN", "PELVIS", "KNEE", "SPINE", "SHOULDER" };

            int insertedPatients = 0;
            int insertedStudies = 0;
            int insertedSeries = 0;

            for (int i = 0; i < 20; i++)
            {
                // Generate Patient
                string patientId = $"PAT-{random.Next(10000, 99999)}";
                string name = $"{lastNames[random.Next(lastNames.Length)]}^{firstNames[random.Next(firstNames.Length)]}";
                string birthDate = DateTime.Today.AddYears(-random.Next(20, 80)).ToString("yyyyMMdd");
                string gender = random.Next(2) == 0 ? "M" : "F";
                string now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                using var cmdPatient = connection.CreateCommand();
                cmdPatient.CommandText = @"
                    INSERT INTO Patients (PatientId, Name, BirthDate, Sex, CreatedAt)
                    VALUES (@pId, @name, @dob, @sex, @now);
                    SELECT last_insert_rowid();";
                cmdPatient.Parameters.AddWithValue("@pId", patientId);
                cmdPatient.Parameters.AddWithValue("@name", name);
                cmdPatient.Parameters.AddWithValue("@dob", birthDate);
                cmdPatient.Parameters.AddWithValue("@sex", gender);
                cmdPatient.Parameters.AddWithValue("@now", now);

                long patientPk = (long)cmdPatient.ExecuteScalar();
                insertedPatients++;

                // Generate 1-3 Studies per patient
                int numStudies = random.Next(1, 4);
                for (int j = 0; j < numStudies; j++)
                {
                    string studyUid = $"1.2.826.0.1.3680043.2.135.{random.Next(10000, 99999)}.{DateTime.UtcNow.Ticks}";
                    string studyDate = DateTime.Today.AddDays(-random.Next(0, 30)).ToString("yyyyMMdd");
                    string modality = modalities[random.Next(modalities.Length)];
                    string accession = $"ACC-{random.Next(100000, 999999)}";
                    string desc = descriptions[random.Next(descriptions.Length)];

                    using var cmdStudy = connection.CreateCommand();
                    cmdStudy.CommandText = @"
                        INSERT INTO Studies (StudyInstanceUid, StudyDate, Modality, AccessionNumber, StudyDescription, Status, IsDeleted, PatientId, CreatedAt, LastImageReceivedAt)
                        VALUES (@uid, @date, @mod, @acc, @desc, 0, 0, @pid, @now, @now);
                        SELECT last_insert_rowid();";
                    cmdStudy.Parameters.AddWithValue("@uid", studyUid);
                    cmdStudy.Parameters.AddWithValue("@date", studyDate);
                    cmdStudy.Parameters.AddWithValue("@mod", modality);
                    cmdStudy.Parameters.AddWithValue("@acc", accession);
                    cmdStudy.Parameters.AddWithValue("@desc", desc);
                    cmdStudy.Parameters.AddWithValue("@pid", patientPk);
                    cmdStudy.Parameters.AddWithValue("@now", now);

                    long studyPk = (long)cmdStudy.ExecuteScalar();
                    insertedStudies++;

                    // Generate 1-5 Series per study
                    int numSeries = random.Next(1, 6);
                    for (int k = 0; k < numSeries; k++)
                    {
                        string seriesUid = $"{studyUid}.{k + 1}";
                        using var cmdSeries = connection.CreateCommand();
                        cmdSeries.CommandText = @"
                            INSERT INTO Series (SeriesInstanceUid, SeriesNumber, Modality, SeriesDescription, StudyId, CreatedAt)
                            VALUES (@uid, @num, @mod, @desc, @sid, @now);";
                        cmdSeries.Parameters.AddWithValue("@uid", seriesUid);
                        cmdSeries.Parameters.AddWithValue("@num", k + 1);
                        cmdSeries.Parameters.AddWithValue("@mod", modality);
                        cmdSeries.Parameters.AddWithValue("@desc", $"Series {k + 1}");
                        cmdSeries.Parameters.AddWithValue("@sid", studyPk);
                        cmdSeries.Parameters.AddWithValue("@now", now);

                        cmdSeries.ExecuteNonQuery();
                        insertedSeries++;
                    }
                }
            }

            transaction.Commit();
            Console.WriteLine("Fake data injected successfully!");
            Console.WriteLine($"Added: {insertedPatients} Patients, {insertedStudies} Studies, {insertedSeries} Series.");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
