using System;
using Microsoft.Data.Sqlite;

namespace Seeder
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Seeding 1000 fake patients and studies...");
            string dbPath = @"D:\ClariMed\db\clarimed.db";
            
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();
            
            var random = new Random();
            var now = DateTime.UtcNow;

            using (var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table'", connection, transaction))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    Console.WriteLine("Table: " + reader.GetString(0));
                }
            }

            for (int i = 1; i <= 1000; i++)
            {
                string patientId = $"PT-{10000 + i}";
                string name = $"Fake Patient {i}";
                string sex = i % 2 == 0 ? "M" : "F";
                string birthDate = now.AddYears(-random.Next(20, 80)).ToString("yyyy-MM-dd");

                // Insert Patient
                string insertPatientSql = @"
                    INSERT INTO Patients (PatientId, Name, BirthDate, Sex, CreatedAt)
                    VALUES (@PatientId, @Name, @BirthDate, @Sex, @CreatedAt);
                    SELECT last_insert_rowid();";
                    
                using var command = new SqliteCommand(insertPatientSql, connection, transaction);
                command.Parameters.AddWithValue("@PatientId", patientId);
                command.Parameters.AddWithValue("@Name", name);
                command.Parameters.AddWithValue("@BirthDate", birthDate);
                command.Parameters.AddWithValue("@Sex", sex);
                command.Parameters.AddWithValue("@CreatedAt", now.ToString("yyyy-MM-dd HH:mm:ss"));

                var patientRowId = Convert.ToInt64(command.ExecuteScalar());

                // Insert 1 Study for this Patient
                string studyUid = Guid.NewGuid().ToString();
                string accession = $"ACC-{20000 + i}";
                string modality = i % 3 == 0 ? "CT" : (i % 2 == 0 ? "MRI" : "CR");
                
                string insertStudySql = @"
                    INSERT INTO Studies (StudyInstanceUid, PatientId, AccessionNumber, StudyDate, StudyDescription, Modality, ReferringPhysicianName, InstitutionName, CreatedAt, Status, LastImageReceivedAt, ImageCount, IsDeleted)
                    VALUES (@StudyInstanceUid, @PatientId, @AccessionNumber, @StudyDate, @StudyDescription, @Modality, @ReferringPhysicianName, @InstitutionName, @CreatedAt, 1, @LastImageReceivedAt, @ImageCount, 0);";
                    
                using var studyCmd = new SqliteCommand(insertStudySql, connection, transaction);
                studyCmd.Parameters.AddWithValue("@StudyInstanceUid", studyUid);
                studyCmd.Parameters.AddWithValue("@PatientId", patientRowId);
                studyCmd.Parameters.AddWithValue("@AccessionNumber", accession);
                studyCmd.Parameters.AddWithValue("@StudyDate", now.AddDays(-random.Next(0, 30)).ToString("yyyy-MM-dd HH:mm:ss"));
                studyCmd.Parameters.AddWithValue("@StudyDescription", $"Fake Study for {modality}");
                studyCmd.Parameters.AddWithValue("@Modality", modality);
                studyCmd.Parameters.AddWithValue("@ReferringPhysicianName", "Dr. Fake");
                studyCmd.Parameters.AddWithValue("@InstitutionName", "ClariMed Clinic");
                studyCmd.Parameters.AddWithValue("@CreatedAt", now.ToString("yyyy-MM-dd HH:mm:ss"));
                studyCmd.Parameters.AddWithValue("@LastImageReceivedAt", now.ToString("yyyy-MM-dd HH:mm:ss"));
                studyCmd.Parameters.AddWithValue("@ImageCount", random.Next(1, 50));
                
                studyCmd.ExecuteNonQuery();
                
                if (i % 100 == 0)
                {
                    Console.WriteLine($"Inserted {i} patients...");
                }
            }
            
            transaction.Commit();
            Console.WriteLine("Successfully inserted 1000 fake patients and studies.");
        }
    }
}
