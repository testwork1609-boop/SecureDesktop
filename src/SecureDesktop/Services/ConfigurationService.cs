namespace SecureDesktop.Services
{
    public class ConfigurationService
    {
        private readonly Database.DatabaseInitializer _db;

        public ConfigurationService(Database.DatabaseInitializer db)
        {
            _db = db;
        }

        public string GetSetting(string key)
        {
            var data = _db.GetData();
            return data.Settings.ContainsKey(key) ? data.Settings[key] : null;
        }

        public void SetSetting(string key, string value)
        {
            var data = _db.GetData();
            data.Settings[key] = value;
            _db.Save();
        }
    }
}