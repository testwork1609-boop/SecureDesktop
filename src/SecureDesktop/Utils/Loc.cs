using System;
using System.Collections.Generic;
using System.IO;

namespace SecureDesktop.Utils
{
    public static class Loc
    {
        public static event Action LanguageChanged;

        private static string _current = "pl";
        public static string Current { get { return _current; } }
        public static bool IsEnglish { get { return _current == "en"; } }

        private static readonly Dictionary<string, Dictionary<string, string>> _dict =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly string _cfgPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "language.cfg");

        static Loc()
        {
            BuildPolish();
            BuildEnglish();
        }

        public static void LoadFromDisk()
        {
            try
            {
                if (File.Exists(_cfgPath))
                {
                    var lang = File.ReadAllText(_cfgPath).Trim().ToLowerInvariant();
                    if (lang == "en" || lang == "pl") _current = lang;
                }
            }
            catch { }
        }

        public static void SaveToDisk()
        {
            try { File.WriteAllText(_cfgPath, _current); } catch { }
        }

        public static void SetLanguage(string lang)
        {
            if (string.IsNullOrEmpty(lang)) return;
            lang = lang.ToLowerInvariant();
            if (lang != "pl" && lang != "en") return;
            if (_current == lang) return;
            _current = lang;
            SaveToDisk();
            var h = LanguageChanged;
            if (h != null) h();
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            var d = _dict[_current];
            string v;
            if (d.TryGetValue(key, out v)) return v;
            if (_dict["pl"].TryGetValue(key, out v)) return v;
            return key;
        }

        public static string T(string key, params object[] args)
        {
            var s = T(key);
            if (args != null && args.Length > 0)
            {
                try { return string.Format(s, args); } catch { return s; }
            }
            return s;
        }

        private static void BuildPolish()
        {
            var d = new Dictionary<string, string>();

            d["login.window_title"] = "SecureDesktop — Logowanie";
            d["login.subtitle"] = "Zaloguj się, aby kontynuować";
            d["login.id"] = "Numer identyfikacyjny (PIN)";
            d["login.password"] = "Hasło zmiany";
            d["login.submit"] = "Zaloguj się";
            d["login.hint"] = "Domyślnie: admin / admin";
            d["login.err.empty"] = "Podaj PIN i hasło zmiany";
            d["login.err.invalid"] = "Nieprawidłowy PIN lub hasło zmiany";
            d["login.err.no_pass"] = "Zmiana nie ma ustawionego hasła";
            d["login.err.no_shift"] = "Konto nie ma przypisanej zmiany";
            d["login.err.generic"] = "Błąd: ";

            d["common.ok"] = "OK";
            d["common.cancel"] = "Anuluj";
            d["common.error"] = "Błąd";
            d["common.info"] = "Info";
            d["common.success"] = "Sukces";
            d["common.confirm"] = "Potwierdzenie";

            d["dash.window_title"] = "SecureDesktop — Panel główny";
            d["dash.view_home"] = "Panel główny";
            d["dash.header_brand"] = "SecureDesktop";
            d["dash.user_admin"] = "Admin";
            d["dash.user_user"] = "User";
            d["dash.sb.home"] = "Panel główny";
            d["dash.sb.lock_all"] = "Blokuj cały ekran";
            d["dash.sb.lock_pattern"] = "Blokuj z patternem";
            d["dash.sb.checkpoint"] = "CheckPoint";
            d["dash.sb.section_admin"] = "ADMINISTRACJA";
            d["dash.sb.config"] = "Konfiguracja";
            d["dash.sb.history"] = "Historia zdarzeń";
            d["dash.sb.backup"] = "Wykonaj backup";
            d["dash.sb.logout"] = "Wyloguj";
            d["dash.home.greeting"] = "Witaj, {0} 👋";
            d["dash.home.logged_at"] = "Zalogowano: {0}";
            d["dash.home.stat_patterns"] = "Wzorców";
            d["dash.home.stat_events"] = "Zdarzeń";
            d["dash.home.stat_status"] = "Status";
            d["dash.home.stat_patterns_sub"] = "w tym aktywnych: {0}";
            d["dash.home.stat_events_sub"] = "w bazie danych";
            d["dash.home.stat_status_sub"] = "system sprawny";
            d["dash.home.tips"] = "Wskazówki";
            d["dash.home.tips_empty"] = "Brak wskazówek. Możesz je dodać w Konfiguracja → Wskazówki.";
            d["dash.msg.no_patterns"] = "Brak używalnych wzorców (aktywnych, z zapisanym obrazem). Dodaj wzorzec w Konfiguracji.";
            d["dash.msg.backup_done"] = "Backup utworzony!";
            d["dash.msg.no_db"] = "Brak bazy danych.";
            d["dash.msg.err"] = "Błąd: ";
            d["dash.msg.patternlock_title"] = "Pattern Lock";

            d["cfg.title"] = "Konfiguracja";
            d["cfg.subtitle"] = "Ustawienia aplikacji, wzorce, kopie zapasowe i użytkownicy";
            d["cfg.tab.general"] = "Ogólne";
            d["cfg.tab.patterns"] = "Wzorce";
            d["cfg.tab.tips"] = "Wskazówki";
            d["cfg.tab.shifts"] = "Zmiany";
            d["cfg.tab.checkpoint"] = "CheckPoint";
            d["cfg.tab.backup"] = "Backup";
            d["cfg.tab.users"] = "Użytkownicy";
            d["cfg.btn.save_all"] = "💾   Zapisz wszystkie ustawienia";
            d["cfg.btn.back"] = "Powrót do panelu";

            d["cfg.gen.pass_label"] = "Nowe hasło administratora (legacy)";
            d["cfg.gen.pass_hint"] = "Pozostaw puste, aby nie zmieniać. Hasła zmian ustaw w zakładce Zmiany.";
            d["cfg.gen.autostart"] = "Uruchamiaj przy starcie Windows";
            d["cfg.gen.tray"] = "Minimalizuj do zasobnika systemowego";

            d["cfg.pat.list"] = "Lista wzorców";
            d["cfg.pat.btn_add"] = "➕   Zaznacz z ekranu";
            d["cfg.pat.btn_del"] = "🗑   Usuń zaznaczony";
            d["cfg.pat.btn_test"] = "🔍   Testuj wzorzec (na ekranie)";
            d["cfg.pat.preview"] = "Podgląd";
            d["cfg.pat.params"] = "Parametry rozpoznawania";
            d["cfg.pat.threshold"] = "Próg NCC (%)";
            d["cfg.pat.interval"] = "Interwał (ms)";
            d["cfg.pat.margin"] = "Margines (px)";
            d["cfg.pat.btn_apply"] = "Zastosuj do zaznaczonego";
            d["cfg.pat.msg.applied"] = "Zastosowano!";
            d["cfg.pat.msg.select"] = "Zaznacz wzorzec na liście.";
            d["cfg.pat.msg.select_test"] = "Zaznacz wzorzec do przetestowania.";
            d["cfg.pat.msg.no_image"] = "Ten wzorzec nie ma obrazu.";
            d["cfg.pat.msg.test_err"] = "Błąd testu: ";
            d["cfg.pat.msg.unknown"] = "nieznany";
            d["cfg.pat.verdict_good"] = "✅ WZORZEC DOBRY";
            d["cfg.pat.verdict_ok"] = "⚠️ WZORZEC ŚREDNI";
            d["cfg.pat.verdict_weak"] = "❌ WZORZEC SŁABY (wiele miejsc)";
            d["cfg.pat.verdict_similar"] = "⚠️ NIE widoczny, coś podobnego jest";
            d["cfg.pat.verdict_notvisible"] = "❌ Wzorzec NIE jest widoczny na ekranie";
            d["cfg.pat.test_header"] = "Wzorzec: \"{0}\"  ({1}x{2} px, kontrast: {3})";
            d["cfg.pat.dlg_title"] = "Nazwa wzorca";
            d["cfg.pat.dlg_label"] = "Podaj nazwę wzorca";
            d["cfg.pat.dlg_btn"] = "Zapisz";
            d["cfg.pat.dlg_err_no_name"] = "Podaj nazwę wzorca.";
            d["cfg.pat.desc_prefix"] = "Dodany ";
            d["cfg.pat.del_confirm"] = "Czy na pewno usunąć zaznaczony wzorzec?";
            d["cfg.pat.del_select"] = "Zaznacz wzorzec do usunięcia.";

            d["cfg.tips.title"] = "Wskazówki na panelu głównym";
            d["cfg.tips.btn_new"] = "➕   Nowa wskazówka";
            d["cfg.tips.btn_del"] = "🗑   Usuń zaznaczoną";
            d["cfg.tips.editing_new"] = "Nowa wskazówka";
            d["cfg.tips.editing_edit"] = "Edycja wskazówki";
            d["cfg.tips.field_title"] = "Tytuł (wyświetlany WIELKIMI literami)";
            d["cfg.tips.field_content"] = "Treść";
            d["cfg.tips.btn_save"] = "💾   Zapisz wskazówkę";
            d["cfg.tips.btn_clear"] = "Wyczyść";
            d["cfg.tips.err_no_title"] = "Podaj tytuł wskazówki.";
            d["cfg.tips.err_no_content"] = "Podaj treść wskazówki.";
            d["cfg.tips.saved"] = "Wskazówka zapisana!";
            d["cfg.tips.select_to_delete"] = "Zaznacz wskazówkę do usunięcia.";
            d["cfg.tips.del_confirm"] = "Czy na pewno usunąć zaznaczoną wskazówkę?";

            d["cfg.shifts.title"] = "Zmiany — hasła logowania";
            d["cfg.shifts.hint"] = "Każda zmiana ma własne hasło. Użytkownik loguje się przez swój PIN + hasło zmiany.";
            d["cfg.shifts.btn_new"] = "➕   Nowa zmiana";
            d["cfg.shifts.btn_del"] = "🗑   Usuń zaznaczoną";
            d["cfg.shifts.editing_new"] = "Nowa zmiana";
            d["cfg.shifts.editing_edit"] = "Edycja zmiany";
            d["cfg.shifts.field_name"] = "Nazwa zmiany";
            d["cfg.shifts.field_pass"] = "Hasło zmiany";
            d["cfg.shifts.pass_hint"] = "Pozostaw puste, aby nie zmieniać hasła.";
            d["cfg.shifts.btn_save"] = "💾   Zapisz zmianę";
            d["cfg.shifts.btn_clear"] = "Wyczyść";
            d["cfg.shifts.err_no_name"] = "Podaj nazwę zmiany.";
            d["cfg.shifts.err_no_pass_new"] = "Podaj hasło dla nowej zmiany.";
            d["cfg.shifts.saved"] = "Zmiana zapisana!";
            d["cfg.shifts.select_to_delete"] = "Zaznacz zmianę do usunięcia.";
            d["cfg.shifts.del_confirm"] = "Czy na pewno usunąć zaznaczoną zmianę?";
            d["cfg.shifts.del_has_users"] = "Do tej zmiany są przypisani aktywni użytkownicy.";

            d["cfg.cp.path"] = "Ścieżka do pliku EXE";
            d["cfg.cp.args"] = "Parametry uruchomienia";
            d["cfg.cp.btn_test"] = "▶   Testuj uruchomienie";
            d["cfg.cp.btn_browse"] = "Przeglądaj";

            d["cfg.bk.folder"] = "Folder docelowy backupu";
            d["cfg.bk.file"] = "Plik do monitorowania (backup przy logowaniu)";
            d["cfg.bk.btn_run"] = "💾   Wykonaj backup teraz";
            d["cfg.bk.err_no_file"] = "Wybierz plik do backupu.";
            d["cfg.bk.done"] = "Backup utworzony!";

            d["cfg.usr.title"] = "Zarządzanie użytkownikami";
            d["cfg.usr.col_id"] = "ID";
            d["cfg.usr.col_ident"] = "Numer identyfikacyjny";
            d["cfg.usr.col_shift"] = "Zmiana";
            d["cfg.usr.col_role"] = "Rola";
            d["cfg.usr.col_active"] = "Aktywny";
            d["cfg.usr.role_admin"] = "Administrator";
            d["cfg.usr.role_user"] = "Użytkownik";
            d["cfg.usr.yes"] = "Tak";
            d["cfg.usr.no"] = "Nie";
            d["cfg.usr.btn_add"] = "➕   Dodaj";
            d["cfg.usr.btn_deactivate"] = "🗑   Dezaktywuj";
            d["cfg.usr.btn_toggle"] = "↺   Zmień rolę";
            d["cfg.usr.dlg_title"] = "Dodaj użytkownika";
            d["cfg.usr.dlg_ident"] = "Numer identyfikacyjny (PIN)";
            d["cfg.usr.dlg_shift"] = "Zmiana";
            d["cfg.usr.dlg_admin"] = "Uprawnienia administratora";
            d["cfg.usr.dlg_add"] = "Dodaj";
            d["cfg.usr.dlg_cancel"] = "Anuluj";
            d["cfg.usr.err_empty_ident"] = "Wprowadź numer identyfikacyjny.";
            d["cfg.usr.err_no_shift"] = "Wybierz zmianę.";
            d["cfg.usr.err_exists"] = "Użytkownik o takim numerze już istnieje.";
            d["cfg.usr.err_cant_del_admin"] = "Nie można usunąć domyślnego administratora.";
            d["cfg.usr.err_cant_change_admin"] = "Nie można zmienić roli domyślnego administratora.";
            d["cfg.usr.select_del"] = "Zaznacz użytkownika do usunięcia.";
            d["cfg.usr.select_role"] = "Zaznacz użytkownika.";
            d["cfg.usr.del_confirm"] = "Czy na pewno dezaktywować użytkownika {0}?";
            d["cfg.usr.role_changed"] = "Rola zmieniona na: {0}";
            d["cfg.usr.no_shifts"] = "Brak zdefiniowanych zmian. Dodaj zmiany w Konfiguracja → Zmiany.";

            d["cfg.msg.saved"] = "Ustawienia zapisane!";
            d["cfg.msg.no_db"] = "Brak połączenia z bazą.";
            d["cfg.msg.save_err"] = "Błąd zapisu: ";
            d["cfg.msg.save_patterns_err"] = "Błąd zapisu wzorców: ";
            d["cfg.msg.save_tips_err"] = "Błąd zapisu wskazówek: ";

            d["hist.title"] = "Historia zdarzeń";
            d["hist.subtitle"] = "Wszystkie operacje zarejestrowane w bazie";
            d["hist.btn_refresh"] = "🔄   Odśwież";
            d["hist.btn_clear"] = "🗑   Wyczyść wszystko";
            d["hist.btn_back"] = "Powrót do panelu";
            d["hist.header_fmt"] = "═══ HISTORIA ZDARZEŃ  ({0} wpisów) ═══";
            d["hist.empty"] = "  Brak zapisanych zdarzeń.";
            d["hist.load_err"] = "Błąd wczytywania historii: ";
            d["hist.clear_confirm"] = "Czy na pewno usunąć CAŁĄ historię zdarzeń z bazy?";
            d["hist.clear_err"] = "Błąd czyszczenia: ";

            d["lock.dlg_title"] = "Odblokuj ekran";
            d["lock.dlg_prompt"] = "Wprowadź hasło zmiany, aby odblokować";
            d["lock.btn_unlock"] = "Odblokuj";
            d["lock.btn_cancel"] = "Anuluj";
            d["lock.err_wrong"] = "Nieprawidłowe hasło!";
            d["lock.hint"] = "Odblokuj (Ctrl+L)";

            d["sel.hint"] = "Zaznacz obszar. ENTER = zatwierdź, ESC = anuluj";

            _dict["pl"] = d;
        }

        private static void BuildEnglish()
        {
            var d = new Dictionary<string, string>();

            d["login.window_title"] = "SecureDesktop — Sign in";
            d["login.subtitle"] = "Sign in to continue";
            d["login.id"] = "Identification number (PIN)";
            d["login.password"] = "Shift password";
            d["login.submit"] = "Sign in";
            d["login.hint"] = "Default: admin / admin";
            d["login.err.empty"] = "Please enter your PIN and shift password";
            d["login.err.invalid"] = "Invalid PIN or shift password";
            d["login.err.no_pass"] = "Shift has no password set";
            d["login.err.no_shift"] = "Account has no shift assigned";
            d["login.err.generic"] = "Error: ";

            d["common.ok"] = "OK";
            d["common.cancel"] = "Cancel";
            d["common.error"] = "Error";
            d["common.info"] = "Info";
            d["common.success"] = "Success";
            d["common.confirm"] = "Confirm";

            d["dash.window_title"] = "SecureDesktop — Dashboard";
            d["dash.view_home"] = "Dashboard";
            d["dash.header_brand"] = "SecureDesktop";
            d["dash.user_admin"] = "Admin";
            d["dash.user_user"] = "User";
            d["dash.sb.home"] = "Dashboard";
            d["dash.sb.lock_all"] = "Lock entire screen";
            d["dash.sb.lock_pattern"] = "Lock with pattern";
            d["dash.sb.checkpoint"] = "CheckPoint";
            d["dash.sb.section_admin"] = "ADMINISTRATION";
            d["dash.sb.config"] = "Settings";
            d["dash.sb.history"] = "Event history";
            d["dash.sb.backup"] = "Run backup";
            d["dash.sb.logout"] = "Log out";
            d["dash.home.greeting"] = "Welcome, {0} 👋";
            d["dash.home.logged_at"] = "Signed in: {0}";
            d["dash.home.stat_patterns"] = "Patterns";
            d["dash.home.stat_events"] = "Events";
            d["dash.home.stat_status"] = "Status";
            d["dash.home.stat_patterns_sub"] = "active: {0}";
            d["dash.home.stat_events_sub"] = "in the database";
            d["dash.home.stat_status_sub"] = "system OK";
            d["dash.home.tips"] = "Tips";
            d["dash.home.tips_empty"] = "No tips yet. Add them in Settings → Tips.";
            d["dash.msg.no_patterns"] = "No usable patterns (active with saved image). Add a pattern in Settings.";
            d["dash.msg.backup_done"] = "Backup created!";
            d["dash.msg.no_db"] = "Database file missing.";
            d["dash.msg.err"] = "Error: ";
            d["dash.msg.patternlock_title"] = "Pattern Lock";

            d["cfg.title"] = "Settings";
            d["cfg.subtitle"] = "Application settings, patterns, backups and users";
            d["cfg.tab.general"] = "General";
            d["cfg.tab.patterns"] = "Patterns";
            d["cfg.tab.tips"] = "Tips";
            d["cfg.tab.shifts"] = "Shifts";
            d["cfg.tab.checkpoint"] = "CheckPoint";
            d["cfg.tab.backup"] = "Backup";
            d["cfg.tab.users"] = "Users";
            d["cfg.btn.save_all"] = "💾   Save all settings";
            d["cfg.btn.back"] = "Back to dashboard";

            d["cfg.gen.pass_label"] = "New administrator password (legacy)";
            d["cfg.gen.pass_hint"] = "Leave empty to keep current. Set shift passwords in the Shifts tab.";
            d["cfg.gen.autostart"] = "Run on Windows startup";
            d["cfg.gen.tray"] = "Minimize to system tray";

            d["cfg.pat.list"] = "Patterns list";
            d["cfg.pat.btn_add"] = "➕   Capture from screen";
            d["cfg.pat.btn_del"] = "🗑   Delete selected";
            d["cfg.pat.btn_test"] = "🔍   Test pattern (on screen)";
            d["cfg.pat.preview"] = "Preview";
            d["cfg.pat.params"] = "Recognition parameters";
            d["cfg.pat.threshold"] = "NCC threshold (%)";
            d["cfg.pat.interval"] = "Interval (ms)";
            d["cfg.pat.margin"] = "Margin (px)";
            d["cfg.pat.btn_apply"] = "Apply to selected";
            d["cfg.pat.msg.applied"] = "Applied!";
            d["cfg.pat.msg.select"] = "Select a pattern in the list.";
            d["cfg.pat.msg.select_test"] = "Select a pattern to test.";
            d["cfg.pat.msg.no_image"] = "This pattern has no image.";
            d["cfg.pat.msg.test_err"] = "Test error: ";
            d["cfg.pat.msg.unknown"] = "unknown";
            d["cfg.pat.verdict_good"] = "✅ GOOD PATTERN";
            d["cfg.pat.verdict_ok"] = "⚠️ MEDIUM PATTERN";
            d["cfg.pat.verdict_weak"] = "❌ WEAK PATTERN (multiple matches)";
            d["cfg.pat.verdict_similar"] = "⚠️ Not visible, something similar exists";
            d["cfg.pat.verdict_notvisible"] = "❌ Pattern is NOT visible on screen";
            d["cfg.pat.test_header"] = "Pattern: \"{0}\"  ({1}x{2} px, contrast: {3})";
            d["cfg.pat.dlg_title"] = "Pattern name";
            d["cfg.pat.dlg_label"] = "Enter pattern name";
            d["cfg.pat.dlg_btn"] = "Save";
            d["cfg.pat.dlg_err_no_name"] = "Enter pattern name.";
            d["cfg.pat.desc_prefix"] = "Added ";
            d["cfg.pat.del_confirm"] = "Delete the selected pattern?";
            d["cfg.pat.del_select"] = "Select a pattern to delete.";

            d["cfg.tips.title"] = "Tips on the dashboard";
            d["cfg.tips.btn_new"] = "➕   New tip";
            d["cfg.tips.btn_del"] = "🗑   Delete selected";
            d["cfg.tips.editing_new"] = "New tip";
            d["cfg.tips.editing_edit"] = "Editing tip";
            d["cfg.tips.field_title"] = "Title (shown in UPPERCASE)";
            d["cfg.tips.field_content"] = "Content";
            d["cfg.tips.btn_save"] = "💾   Save tip";
            d["cfg.tips.btn_clear"] = "Clear";
            d["cfg.tips.err_no_title"] = "Enter a tip title.";
            d["cfg.tips.err_no_content"] = "Enter tip content.";
            d["cfg.tips.saved"] = "Tip saved!";
            d["cfg.tips.select_to_delete"] = "Select a tip to delete.";
            d["cfg.tips.del_confirm"] = "Delete the selected tip?";

            d["cfg.shifts.title"] = "Shifts — login passwords";
            d["cfg.shifts.hint"] = "Each shift has its own password. Users log in with their PIN + shift password.";
            d["cfg.shifts.btn_new"] = "➕   New shift";
            d["cfg.shifts.btn_del"] = "🗑   Delete selected";
            d["cfg.shifts.editing_new"] = "New shift";
            d["cfg.shifts.editing_edit"] = "Editing shift";
            d["cfg.shifts.field_name"] = "Shift name";
            d["cfg.shifts.field_pass"] = "Shift password";
            d["cfg.shifts.pass_hint"] = "Leave empty to keep current password.";
            d["cfg.shifts.btn_save"] = "💾   Save shift";
            d["cfg.shifts.btn_clear"] = "Clear";
            d["cfg.shifts.err_no_name"] = "Enter shift name.";
            d["cfg.shifts.err_no_pass_new"] = "Enter password for the new shift.";
            d["cfg.shifts.saved"] = "Shift saved!";
            d["cfg.shifts.select_to_delete"] = "Select a shift to delete.";
            d["cfg.shifts.del_confirm"] = "Delete the selected shift?";
            d["cfg.shifts.del_has_users"] = "There are active users assigned to this shift.";

            d["cfg.cp.path"] = "Path to EXE file";
            d["cfg.cp.args"] = "Launch arguments";
            d["cfg.cp.btn_test"] = "▶   Test launch";
            d["cfg.cp.btn_browse"] = "Browse";

            d["cfg.bk.folder"] = "Backup target folder";
            d["cfg.bk.file"] = "Monitored file (backup on login)";
            d["cfg.bk.btn_run"] = "💾   Run backup now";
            d["cfg.bk.err_no_file"] = "Choose a file to back up.";
            d["cfg.bk.done"] = "Backup created!";

            d["cfg.usr.title"] = "User management";
            d["cfg.usr.col_id"] = "ID";
            d["cfg.usr.col_ident"] = "Identification number";
            d["cfg.usr.col_shift"] = "Shift";
            d["cfg.usr.col_role"] = "Role";
            d["cfg.usr.col_active"] = "Active";
            d["cfg.usr.role_admin"] = "Administrator";
            d["cfg.usr.role_user"] = "User";
            d["cfg.usr.yes"] = "Yes";
            d["cfg.usr.no"] = "No";
            d["cfg.usr.btn_add"] = "➕   Add";
            d["cfg.usr.btn_deactivate"] = "🗑   Deactivate";
            d["cfg.usr.btn_toggle"] = "↺   Change role";
            d["cfg.usr.dlg_title"] = "Add user";
            d["cfg.usr.dlg_ident"] = "Identification number (PIN)";
            d["cfg.usr.dlg_shift"] = "Shift";
            d["cfg.usr.dlg_admin"] = "Administrator privileges";
            d["cfg.usr.dlg_add"] = "Add";
            d["cfg.usr.dlg_cancel"] = "Cancel";
            d["cfg.usr.err_empty_ident"] = "Enter identification number.";
            d["cfg.usr.err_no_shift"] = "Choose a shift.";
            d["cfg.usr.err_exists"] = "User with that number already exists.";
            d["cfg.usr.err_cant_del_admin"] = "Cannot delete the default administrator.";
            d["cfg.usr.err_cant_change_admin"] = "Cannot change the default administrator's role.";
            d["cfg.usr.select_del"] = "Select a user to delete.";
            d["cfg.usr.select_role"] = "Select a user.";
            d["cfg.usr.del_confirm"] = "Deactivate user {0}?";
            d["cfg.usr.role_changed"] = "Role changed to: {0}";
            d["cfg.usr.no_shifts"] = "No shifts defined. Add shifts in Settings → Shifts.";

            d["cfg.msg.saved"] = "Settings saved!";
            d["cfg.msg.no_db"] = "No database connection.";
            d["cfg.msg.save_err"] = "Save error: ";
            d["cfg.msg.save_patterns_err"] = "Pattern save error: ";
            d["cfg.msg.save_tips_err"] = "Tips save error: ";

            d["hist.title"] = "Event history";
            d["hist.subtitle"] = "All operations recorded in the database";
            d["hist.btn_refresh"] = "🔄   Refresh";
            d["hist.btn_clear"] = "🗑   Clear all";
            d["hist.btn_back"] = "Back to dashboard";
            d["hist.header_fmt"] = "═══ EVENT HISTORY  ({0} entries) ═══";
            d["hist.empty"] = "  No events recorded.";
            d["hist.load_err"] = "Failed to load history: ";
            d["hist.clear_confirm"] = "Delete the ENTIRE event history?";
            d["hist.clear_err"] = "Clear error: ";

            d["lock.dlg_title"] = "Unlock screen";
            d["lock.dlg_prompt"] = "Enter shift password to unlock";
            d["lock.btn_unlock"] = "Unlock";
            d["lock.btn_cancel"] = "Cancel";
            d["lock.err_wrong"] = "Incorrect password!";
            d["lock.hint"] = "Unlock (Ctrl+L)";

            d["sel.hint"] = "Select area. ENTER = confirm, ESC = cancel";

            _dict["en"] = d;
        }
    }
}
