public void Initialize()
{
    try
    {
        var dbDirectory = Path.GetDirectoryName(_dbPath);
        if (!Directory.Exists(dbDirectory))
            Directory.CreateDirectory(dbDirectory);

        if (File.Exists(_dbPath))
        {
            var json = File.ReadAllText(_dbPath);
            _data = JsonConvert.DeserializeObject<DatabaseData>(json) ?? new DatabaseData();

            // 🔍 Diagnostyka przy każdym uruchomieniu
            int userCount = _data.Users?.Count ?? 0;
            string users = string.Join(", ", _data.Users?.Select(u => u.IdentificationNumber) ?? new List<string>());
            MessageBox.Show(
                $"Wczytano bazę:\n{_dbPath}\n\n" +
                $"Liczba użytkowników: {userCount}\n" +
                $"Użytkownicy: {users}",
                "Diagnostyka Initialize()",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else
        {
            _data = new DatabaseData();
            InsertDefaultData();
            Save();
        }
    }
    catch (Exception ex)
    {
        throw new Exception("Database init failed: " + ex.Message, ex);
    }
}