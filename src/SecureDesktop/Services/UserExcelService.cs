using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using SecureDesktop.Database;
using SecureDesktop.Models;

namespace SecureDesktop.Services
{
    public class ImportResult
    {
        public int Added;
        public int Updated;
        public int Skipped;
        public List<string> Errors = new List<string>();
    }

    public class ExportResult
    {
        public int Written;
        public string Path;
    }

    public static class UserExcelService
    {
        /// <summary>
        /// Buduje maskowaną nazwę wyświetlaną: 2 litery imienia + "**" +
        /// 2 litery nazwiska + "**". Gdy część jest krótsza, bierzemy ile jest.
        /// </summary>
        public static string MakeMaskedDisplayName(string firstName, string lastName)
        {
            string f = (firstName ?? "").Trim();
            string l = (lastName ?? "").Trim();

            string f2 = f.Length >= 2 ? f.Substring(0, 2) : f;
            string l2 = l.Length >= 2 ? l.Substring(0, 2) : l;

            return f2 + "**" + l2 + "**";
        }

        /// <summary>
        /// Import z XLSX. Kolumny: [Imię, Nazwisko, PIN]. Pierwszy wiersz
        /// to nagłówek (pomijany). Istniejący użytkownik o tym samym PIN
        /// jest aktualizowany (imię/nazwisko/display).
        /// </summary>
        public static ImportResult ImportFromXlsx(string filePath, DatabaseInitializer db)
        {
            var result = new ImportResult();

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                result.Errors.Add("Plik nie istnieje: " + filePath);
                return result;
            }

            var data = db.GetData();
            if (data == null)
            {
                result.Errors.Add("Baza danych niedostępna.");
                return result;
            }
            if (data.Users == null) data.Users = new List<User>();
            if (data.Shifts == null) data.Shifts = new List<Shift>();

            int defaultShiftId = 0;
            if (data.Shifts.Count > 0) defaultShiftId = data.Shifts[0].Id;

            try
            {
                using (var wb = new XLWorkbook(filePath))
                {
                    var ws = wb.Worksheet(1);
                    var usedRows = ws.RowsUsed().ToList();
                    if (usedRows.Count < 2)
                    {
                        result.Errors.Add("Arkusz jest pusty lub zawiera tylko nagłówek.");
                        return result;
                    }

                    int rowNum = 0;
                    foreach (var row in usedRows)
                    {
                        rowNum++;
                        if (rowNum == 1) continue;

                        try
                        {
                            string firstName = (row.Cell(1).GetString() ?? "").Trim();
                            string lastName = (row.Cell(2).GetString() ?? "").Trim();
                            string pin = (row.Cell(3).GetString() ?? "").Trim();

                            if (string.IsNullOrEmpty(pin))
                            {
                                result.Skipped++;
                                continue;
                            }

                            string displayName = MakeMaskedDisplayName(firstName, lastName);

                            var existing = data.Users.FirstOrDefault(u =>
                                string.Equals(u.IdentificationNumber, pin, StringComparison.Ordinal));

                            if (existing != null)
                            {
                                existing.FirstName = firstName;
                                existing.LastName = lastName;
                                existing.DisplayName = displayName;
                                if (!existing.ShiftId.HasValue && defaultShiftId > 0)
                                    existing.ShiftId = defaultShiftId;
                                result.Updated++;
                            }
                            else
                            {
                                data.Users.Add(new User
                                {
                                    Id = data.NextUserId++,
                                    IdentificationNumber = pin,
                                    FirstName = firstName,
                                    LastName = lastName,
                                    DisplayName = displayName,
                                    IsAdmin = false,
                                    IsActive = true,
                                    ShiftId = defaultShiftId > 0 ? (int?)defaultShiftId : null,
                                    CreatedAt = DateTime.Now
                                });
                                result.Added++;
                            }
                        }
                        catch (Exception exRow)
                        {
                            result.Errors.Add("Wiersz " + rowNum + ": " + exRow.Message);
                        }
                    }
                }

                db.Save();
            }
            catch (Exception ex)
            {
                result.Errors.Add("Błąd odczytu XLSX: " + ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Eksport do XLSX. Kolumny: [Imię, Nazwisko, PIN].
        /// Dla użytkowników bez zapisanych imion (np. utworzonych ręcznie,
        /// jak "admin") pola Imię/Nazwisko będą puste — wypełni się tylko PIN.
        /// </summary>
        public static ExportResult ExportToXlsx(string filePath, DatabaseInitializer db)
        {
            var result = new ExportResult { Path = filePath };

            var data = db.GetData();
            if (data == null || data.Users == null) return result;

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Users");

                ws.Cell(1, 1).Value = "Imie";
                ws.Cell(1, 2).Value = "Nazwisko";
                ws.Cell(1, 3).Value = "PIN";
                ws.Row(1).Style.Font.Bold = true;

                int row = 2;
                foreach (var u in data.Users.OrderBy(x => x.Id))
                {
                    ws.Cell(row, 1).Value = u.FirstName ?? "";
                    ws.Cell(row, 2).Value = u.LastName ?? "";
                    ws.Cell(row, 3).Value = u.IdentificationNumber ?? "";
                    row++;
                    result.Written++;
                }

                ws.Columns().AdjustToContents();

                wb.SaveAs(filePath);
            }

            return result;
        }
    }
}
