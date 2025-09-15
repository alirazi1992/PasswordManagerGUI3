using System;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;

namespace PasswordManagerGUI3
{
    public partial class Form1 : Form
    {
        private string _dbPath;
        private string _connStr;

        // Demo key only — use a secure key/IV strategy in real apps.
        private static readonly string DemoKey = "MySuperSecretKey123";

        public Form1()
        {
            InitializeComponent();

            _dbPath = Path.Combine(AppContext.BaseDirectory, "passwords.db");
            _connStr = "Data Source=" + _dbPath;

            try
            {
                EnsureDatabase();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup error:\n" + ex, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- DB bootstrap ----------
        private void EnsureDatabase()
        {
            if (!File.Exists(_dbPath))
                using (File.Create(_dbPath)) { }

            using (var conn = new SqliteConnection(_connStr))
            {
                conn.Open();
                const string sql = @"
CREATE TABLE IF NOT EXISTS Accounts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Website TEXT NOT NULL,
    Username TEXT NOT NULL,
    Password TEXT NOT NULL
);";
                using (var cmd = new SqliteCommand(sql, conn))
                    cmd.ExecuteNonQuery();
            }
        }

        // ---------- CRUD ----------
        private void btnAdd_Click(object sender, EventArgs e)
        {
            try
            {
                var website = (txtWebsite.Text ?? "").Trim();
                var username = (txtUsername.Text ?? "").Trim();
                var password = (txtPassword.Text ?? "").Trim();

                if (website.Length == 0 || username.Length == 0 || password.Length == 0)
                {
                    MessageBox.Show("Please fill Website, Username, and Password.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var encrypted = Encrypt(password);

                using (var conn = new SqliteConnection(_connStr))
                {
                    conn.Open();
                    const string sql = "INSERT INTO Accounts (Website, Username, Password) VALUES ($w,$u,$p)";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("$w", website);
                        cmd.Parameters.AddWithValue("$u", username);
                        cmd.Parameters.AddWithValue("$p", encrypted);
                        cmd.ExecuteNonQuery();
                    }
                }

                ClearInputs();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Add failed:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvAccounts.CurrentRow == null)
                {
                    MessageBox.Show("Select a row first.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                object idObj = dgvAccounts.CurrentRow.Cells["colId"].Value;
                if (idObj == null || idObj == DBNull.Value)
                {
                    MessageBox.Show("Selected row has no Id.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int id = Convert.ToInt32(idObj);

                if (MessageBox.Show("Delete this account?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                using (var conn = new SqliteConnection(_connStr))
                {
                    conn.Open();
                    const string sql = "DELETE FROM Accounts WHERE Id=$id";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("$id", id);
                        cmd.ExecuteNonQuery();
                    }
                }

                LoadData();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Delete failed:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnReveal_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvAccounts.CurrentRow == null)
                {
                    MessageBox.Show("Select a row first.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                object idObj = dgvAccounts.CurrentRow.Cells["colId"].Value;
                if (idObj == null || idObj == DBNull.Value)
                {
                    MessageBox.Show("Selected row has no Id.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int id = Convert.ToInt32(idObj);

                using (var conn = new SqliteConnection(_connStr))
                {
                    conn.Open();
                    const string sql = "SELECT Password FROM Accounts WHERE Id=$id";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("$id", id);
                        var encrypted = cmd.ExecuteScalar() as string;
                        if (!string.IsNullOrEmpty(encrypted))
                        {
                            string decrypted = Decrypt(encrypted);
                            MessageBox.Show("Password: " + decrypted, "Reveal",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("No password stored.", "Info",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Reveal failed:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- UPDATE ----------
        private void btnUpdate_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvAccounts.CurrentRow == null)
                {
                    MessageBox.Show("Select a row first.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                object idObj = dgvAccounts.CurrentRow.Cells["colId"].Value;
                if (idObj == null || idObj == DBNull.Value)
                {
                    MessageBox.Show("Selected row has no Id.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int id = Convert.ToInt32(idObj);

                string website = (txtWebsite.Text ?? "").Trim();
                string username = (txtUsername.Text ?? "").Trim();
                string password = (txtPassword.Text ?? "").Trim();

                if (website.Length == 0 || username.Length == 0)
                {
                    MessageBox.Show("Website and Username cannot be empty.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using (var conn = new SqliteConnection(_connStr))
                {
                    conn.Open();

                    if (string.IsNullOrWhiteSpace(password))
                    {
                        const string sql = "UPDATE Accounts SET Website=$w, Username=$u WHERE Id=$id";
                        using (var cmd = new SqliteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("$w", website);
                            cmd.Parameters.AddWithValue("$u", username);
                            cmd.Parameters.AddWithValue("$id", id);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        const string sql = "UPDATE Accounts SET Website=$w, Username=$u, Password=$p WHERE Id=$id";
                        using (var cmd = new SqliteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("$w", website);
                            cmd.Parameters.AddWithValue("$u", username);
                            cmd.Parameters.AddWithValue("$p", Encrypt(password));
                            cmd.Parameters.AddWithValue("$id", id);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                LoadData();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update failed:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- GRID → INPUTS ----------
        private void dgvAccounts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvAccounts.CurrentRow != null)
            {
                var w = dgvAccounts.CurrentRow.Cells["colWebsite"].Value;
                var u = dgvAccounts.CurrentRow.Cells["colUsername"].Value;
                txtWebsite.Text = w == null ? "" : w.ToString();
                txtUsername.Text = u == null ? "" : u.ToString();
                txtPassword.Clear(); // only set if user wants to change
            }
        }

        // ---------- SEARCH ----------
        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            string term = (txtSearch.Text ?? "").Trim();

            using (var conn = new SqliteConnection(_connStr))
            {
                conn.Open();
                const string sql = "SELECT Id, Website, Username FROM Accounts WHERE Website LIKE $s OR Username LIKE $s";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("$s", "%" + term + "%");
                    using (var reader = cmd.ExecuteReader())
                    {
                        var dt = new DataTable();
                        dt.Load(reader);
                        dgvAccounts.AutoGenerateColumns = false;
                        dgvAccounts.DataSource = dt;
                    }
                }
            }
        }

        // ---------- LOAD ----------
        private void LoadData()
        {
            using (var conn = new SqliteConnection(_connStr))
            {
                conn.Open();
                const string sql = "SELECT Id, Website, Username FROM Accounts";
                using (var cmd = new SqliteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    var dt = new DataTable();
                    dt.Load(reader);
                    dgvAccounts.AutoGenerateColumns = false;
                    dgvAccounts.DataSource = dt;
                }
            }
        }

        // ---------- EXPORT CSV (decrypted) ----------
        private void btnExport_Click(object sender, EventArgs e)
        {
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Export accounts to CSV";
                    sfd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    sfd.FileName = "passwords_export.csv";
                    if (sfd.ShowDialog() != DialogResult.OK) return;

                    using (var conn = new SqliteConnection(_connStr))
                    {
                        conn.Open();
                        const string sql = "SELECT Website, Username, Password FROM Accounts";
                        using (var cmd = new SqliteCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        using (var sw = new StreamWriter(sfd.FileName, false, Encoding.UTF8))
                        {
                            sw.WriteLine("Website,Username,Password");
                            while (reader.Read())
                            {
                                string website = reader.GetString(0);
                                string username = reader.GetString(1);
                                string encrypted = reader.GetString(2);
                                string decrypted = Decrypt(encrypted);

                                sw.WriteLine(
                                    CsvEscape(website) + "," +
                                    CsvEscape(username) + "," +
                                    CsvEscape(decrypted)
                                );
                            }
                        }
                    }

                    MessageBox.Show("Export complete.\n⚠️ CSV stores passwords in plain text.", "Export",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export failed:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- IMPORT CSV (encrypts on save, skips duplicates) ----------
        private void btnImport_Click(object sender, EventArgs e)
        {
            try
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = "Import accounts from CSV";
                    ofd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    if (ofd.ShowDialog() != DialogResult.OK) return;

                    int added = 0, skipped = 0, lineNo = 0;

                    using (var conn = new SqliteConnection(_connStr))
                    {
                        conn.Open();

                        using (var sr = new StreamReader(ofd.FileName, Encoding.UTF8))
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                lineNo++;
                                if (lineNo == 1)
                                {
                                    if (line.ToLower().Contains("website") && line.ToLower().Contains("username"))
                                        continue; // header
                                }

                                string[] parts = ParseCsvLine(line);
                                if (parts == null || parts.Length < 3) { skipped++; continue; }

                                string website = parts[0].Trim();
                                string username = parts[1].Trim();
                                string passwordPlain = parts[2];

                                if (website.Length == 0 || username.Length == 0) { skipped++; continue; }

                                // Skip duplicates on (Website, Username)
                                using (var check = new SqliteCommand(
                                    "SELECT COUNT(1) FROM Accounts WHERE Website=$w AND Username=$u", conn))
                                {
                                    check.Parameters.AddWithValue("$w", website);
                                    check.Parameters.AddWithValue("$u", username);
                                    long count = (long)check.ExecuteScalar();
                                    if (count > 0) { skipped++; continue; }
                                }

                                string encrypted = Encrypt(passwordPlain);
                                using (var ins = new SqliteCommand(
                                    "INSERT INTO Accounts (Website, Username, Password) VALUES ($w,$u,$p)", conn))
                                {
                                    ins.Parameters.AddWithValue("$w", website);
                                    ins.Parameters.AddWithValue("$u", username);
                                    ins.Parameters.AddWithValue("$p", encrypted);
                                    ins.ExecuteNonQuery();
                                    added++;
                                }
                            }
                        }
                    }

                    LoadData();
                    MessageBox.Show(
                        "Import complete.\nAdded: " + added + "\nSkipped: " + skipped,
                        "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Import failed:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- Helpers ----------
        private void ClearInputs()
        {
            txtWebsite.Clear();
            txtUsername.Clear();
            txtPassword.Clear();
            txtWebsite.Focus();
        }

        // CSV helpers
        private static string CsvEscape(string s)
        {
            if (s == null) s = "";
            bool needsQuotes = s.IndexOfAny(new char[] { ',', '"', '\n', '\r' }) >= 0;
            if (needsQuotes)
            {
                s = s.Replace("\"", "\"\"");
                return "\"" + s + "\"";
            }
            return s;
        }

        private static string[] ParseCsvLine(string line)
        {
            if (line == null) return new string[0];
            var list = new System.Collections.Generic.List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"'); // escaped quote
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == ',')
                    {
                        list.Add(sb.ToString());
                        sb.Length = 0;
                    }
                    else if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }
            list.Add(sb.ToString());
            return list.ToArray();
        }

        // AES helpers (demo)
        private string Encrypt(string plainText)
        {
            if (plainText == null) plainText = "";
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(DemoKey.PadRight(32).Substring(0, 32));
                aes.IV = new byte[16]; // demo only; use random IV per record in production

                using (var enc = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    byte[] input = Encoding.UTF8.GetBytes(plainText);
                    byte[] cipher = enc.TransformFinalBlock(input, 0, input.Length);
                    return Convert.ToBase64String(cipher);
                }
            }
        }

        private string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(DemoKey.PadRight(32).Substring(0, 32));
                aes.IV = new byte[16];

                using (var dec = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    byte[] input = Convert.FromBase64String(cipherText);
                    byte[] plain = dec.TransformFinalBlock(input, 0, input.Length);
                    return Encoding.UTF8.GetString(plain);
                }
            }
        }
    }
}
