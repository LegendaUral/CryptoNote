using Microsoft.Win32;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CryptoNote
{
    public partial class MainWindow : Window
    {
        private string currentFileName = "Новый файл.txt";
        private const string EncryptedHeader = "CryptoNoteEncrypted";

        public MainWindow() => InitializeComponent();

        private void UpdateTitle(string name)
        {
            currentFileName = name;
            TitleTextBlock.Text = name;
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            MainTextBox.Clear();
            string? name = PromptInput("Введите имя нового файла", "Новый файл.txt");
            if (!string.IsNullOrEmpty(name))
                UpdateTitle(name.EndsWith(".txt") ? name : name + ".txt");
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;

            byte[] fileData = File.ReadAllBytes(dlg.FileName);
            bool encrypted = fileData.Length > EncryptedHeader.Length &&
                             Encoding.UTF8.GetString(fileData, 0, EncryptedHeader.Length) == EncryptedHeader;

            if (encrypted)
            {
                string? password = PromptPassword("Введите пароль для открытия файла");
                if (string.IsNullOrEmpty(password)) return;
                try { MainTextBox.Text = DecryptString(fileData, password); }
                catch { MessageBox.Show("Неверный пароль или поврежденный файл!"); return; }
            }
            else MainTextBox.Text = File.ReadAllText(dlg.FileName);

            UpdateTitle(System.IO.Path.GetFileName(dlg.FileName));
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e) =>
            SaveText(MainTextBox.Text, false);

        private void SaveEncrypted_Click(object sender, RoutedEventArgs e)
        {
            string? password = PromptPassword("Введите пароль для шифрования");
            if (string.IsNullOrEmpty(password)) return;
            SaveText(MainTextBox.Text, true, password);
        }

        private void CutText_Click(object sender, RoutedEventArgs e) => MainTextBox.Cut();
        private void CopyText_Click(object sender, RoutedEventArgs e) => MainTextBox.Copy();
        private void PasteText_Click(object sender, RoutedEventArgs e) => MainTextBox.Paste();

        private void SaveText(string text, bool encrypt, string password = "")
        {
            var dlg = new SaveFileDialog
            {
                FileName = currentFileName,
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            if (encrypt)
            {
                File.WriteAllBytes(dlg.FileName, EncryptString(text, password));
            }
            else File.WriteAllText(dlg.FileName, text);

            UpdateTitle(System.IO.Path.GetFileName(dlg.FileName));
        }

        public static string? PromptPassword(string message) => PromptInput(message, "", true);

        public static string? PromptInput(string message, string defaultValue = "", bool isPassword = false)
        {
            var inputWindow = new Window
            {
                Width = 300,
                Height = 150,
                Title = message,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.DarkSlateGray
            };

            Control inputControl = isPassword
                ? new PasswordBox { Margin = new Thickness(10), Background = System.Windows.Media.Brushes.Black, Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0) }
                : new TextBox { Margin = new Thickness(10), Text = defaultValue, Background = System.Windows.Media.Brushes.Black, Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0) };

            var okButton = new Button
            {
                Content = "OK",
                Width = 70,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = System.Windows.Media.Brushes.Black,
                Foreground = System.Windows.Media.Brushes.White
            };
            okButton.Click += (s, e) => inputWindow.DialogResult = true;

            var panel = new StackPanel();
            panel.Children.Add(inputControl);
            panel.Children.Add(okButton);
            inputWindow.Content = panel;

            if (inputWindow.ShowDialog() != true) return null;
            return isPassword ? (inputControl as PasswordBox)?.Password : (inputControl as TextBox)?.Text;
        }

        private static byte[] EncryptString(string text, string password)
        {
            using var aes = Aes.Create();
            aes.Key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password));
            aes.GenerateIV();
            using var ms = new MemoryStream();
            ms.Write(Encoding.UTF8.GetBytes(EncryptedHeader));
            ms.Write(aes.IV);
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs)) sw.Write(text);
            return ms.ToArray();
        }

        private static string DecryptString(byte[] data, string password)
        {
            using var aes = Aes.Create();
            aes.Key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password));

            byte[] iv = new byte[16];
            Array.Copy(data, EncryptedHeader.Length, iv, 0, iv.Length);
            aes.IV = iv;

            using var ms = new MemoryStream(data, EncryptedHeader.Length + 16, data.Length - EncryptedHeader.Length - 16);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
    }
}
