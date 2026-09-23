using System;
using System.Collections.Generic;
using System.Linq;
using SecureDesktop.Models;

namespace SecureDesktop.Database.Repositories
{
    public class ShiftRepository
    {
        private readonly DatabaseInitializer _db;

        public ShiftRepository(DatabaseInitializer db)
        {
            _db = db;
        }

        public List<Shift> GetAllShifts()
        {
            var data = _db.GetData();
            if (data == null || data.Shifts == null) return new List<Shift>();
            return data.Shifts.ToList();
        }

        public Shift GetById(int id)
        {
            var data = _db.GetData();
            if (data == null || data.Shifts == null) return null;
            return data.Shifts.FirstOrDefault(s => s.Id == id);
        }

        public void AddShift(Shift shift)
        {
            var data = _db.GetData();
            shift.Id = data.NextShiftId++;
            shift.CreatedAt = DateTime.Now;
            shift.IsActive = true;
            if (data.Shifts == null) data.Shifts = new List<Shift>();
            data.Shifts.Add(shift);
            _db.Save();
        }

        public void UpdateShift(Shift shift)
        {
            var data = _db.GetData();
            if (data == null || data.Shifts == null) return;
            var existing = data.Shifts.FirstOrDefault(s => s.Id == shift.Id);
            if (existing == null) return;
            existing.Name = shift.Name;
            existing.UpdatedAt = DateTime.Now;
            existing.IsActive = shift.IsActive;
            _db.Save();
        }

        /// <summary>
        /// Ustawia nowe hasło dla zmiany. Hasło jest hashowane SHA256 + sól.
        /// </summary>
        public void SetPassword(int shiftId, string plainPassword)
        {
            var data = _db.GetData();
            if (data == null || data.Shifts == null) return;
            var s = data.Shifts.FirstOrDefault(x => x.Id == shiftId);
            if (s == null) return;

            s.Salt = Utils.SecurityHelper.GenerateSalt();
            s.PasswordHash = Utils.SecurityHelper.HashPassword(plainPassword, s.Salt);
            s.UpdatedAt = DateTime.Now;
            _db.Save();
        }

        public void DeleteShift(int shiftId)
        {
            var data = _db.GetData();
            if (data == null || data.Shifts == null) return;

            // Nie pozwól usunąć zmiany, do której przypisani są aktywni użytkownicy.
            if (data.Users != null && data.Users.Any(u => u.IsActive && u.ShiftId == shiftId))
                throw new InvalidOperationException("Do tej zmiany są przypisani aktywni użytkownicy.");

            var s = data.Shifts.FirstOrDefault(x => x.Id == shiftId);
            if (s != null)
            {
                data.Shifts.Remove(s);
                _db.Save();
            }
        }
    }
}
