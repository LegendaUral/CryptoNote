using Microsoft.Win32;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CryptoNote
{
    public partial class MainWindow : Window
    {
        private string currentFileName = "Новый файл.txt";
        private const string EncryptedHeader = "---CryptoNote Encrypted File (Base64)---";

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
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                string fileContent = File.ReadAllText(dlg.FileName);

                if (fileContent.StartsWith(EncryptedHeader))
                {
                    string? password = PromptPassword("Введите пароль для открытия файла");
                    if (string.IsNullOrEmpty(password)) return;

                    try
                    {
                        MainTextBox.Text = DecryptString(fileContent, password);
                    }
                    catch (FormatException)
                    {
                        System.Windows.MessageBox.Show("Файл поврежден (неверный формат Base64)!"); return;
                    }
                    catch
                    {
                        System.Windows.MessageBox.Show("Неверный пароль или поврежденный файл!"); return;
                    }
                }
                else
                {
                    MainTextBox.Text = fileContent;
                }

                UpdateTitle(Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Не удалось открыть файл: {ex.Message}");
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e) =>
            SaveText(MainTextBox.Text, false);

        private void SaveEncrypted_Click(object sender, RoutedEventArgs e)
        {
            string? password = PromptPassword("Введите пароль для шифрования");
            if (string.IsNullOrEmpty(password)) return;
            SaveText(MainTextBox.Text, true, password);
        }

        private void SaveText(string text, bool encrypt, string password = "")
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = currentFileName,
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                if (encrypt)
                {
                    string encryptedContent = EncryptString(text, password);
                    File.WriteAllText(dlg.FileName, encryptedContent);
                }
                else
                {
                    File.WriteAllText(dlg.FileName, text);
                }
                UpdateTitle(Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Не удалось сохранить файл: {ex.Message}");
            }
        }

        private void CutText_Click(object sender, RoutedEventArgs e) => MainTextBox.Cut();

        private void CopyText_Click(object sender, RoutedEventArgs e) => MainTextBox.Copy();

        private void PasteText_Click(object sender, RoutedEventArgs e) => MainTextBox.Paste();

        private void ChangeFont_Click(object sender, RoutedEventArgs e)
        {
            var fontDialog = new System.Windows.Forms.FontDialog();

            try
            {
                var currentStyle = System.Drawing.FontStyle.Regular;
                if (MainTextBox.FontWeight == FontWeights.Bold) currentStyle |= System.Drawing.FontStyle.Bold;
                if (MainTextBox.FontStyle == FontStyles.Italic) currentStyle |= System.Drawing.FontStyle.Italic;

                fontDialog.Font = new System.Drawing.Font(
                    MainTextBox.FontFamily.Source,
                    (float)MainTextBox.FontSize,
                    currentStyle
                );
            }
            catch { }

            if (fontDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var selectedFont = fontDialog.Font;

                MainTextBox.FontFamily = new System.Windows.Media.FontFamily(selectedFont.Name);
                MainTextBox.FontSize = selectedFont.Size;
                MainTextBox.FontWeight = selectedFont.Bold ? FontWeights.Bold : FontWeights.Regular;
                MainTextBox.FontStyle = selectedFont.Italic ? FontStyles.Italic : FontStyles.Normal;

                MainTextBox.TextDecorations.Clear();
                if (selectedFont.Underline) MainTextBox.TextDecorations.Add(TextDecorations.Underline);
                if (selectedFont.Strikeout) MainTextBox.TextDecorations.Add(TextDecorations.Strikethrough);
            }
        }

        private string? CreateTempShareFile()
        {
            var result = System.Windows.MessageBox.Show("Вы хотите отправить зашифрованный файл?", "Выбор типа файла", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel) return null;

            bool encrypt = result == MessageBoxResult.Yes;
            string tempFilePath = Path.Combine(Path.GetTempPath(), "CryptoNote_Share.txt");

            if (encrypt)
            {
                string? password = PromptPassword("Введите пароль для шифрования");
                if (string.IsNullOrEmpty(password)) return null;
                string encryptedContent = EncryptString(MainTextBox.Text, password);
                File.WriteAllText(tempFilePath, encryptedContent);
            }
            else
            {
                File.WriteAllText(tempFilePath, MainTextBox.Text);
            }
            return tempFilePath;
        }

        private void Share_Telegram(object sender, RoutedEventArgs e)
        {
            string? tempFilePath = CreateTempShareFile();
            if (string.IsNullOrEmpty(tempFilePath)) return;

            try
            {
                var fileCollection = new StringCollection { tempFilePath };
                System.Windows.Clipboard.SetFileDropList(fileCollection);
                System.Windows.MessageBox.Show("Файл скопирован в буфер обмена.\n\nОткройте нужный чат в Telegram и нажмите Ctrl+V, чтобы отправить его.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Не удалось скопировать файл в буфер обмена: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = "tg://", UseShellExecute = true });
            }
            catch
            {
                Process.Start(new ProcessStartInfo { FileName = "https://web.telegram.org/", UseShellExecute = true });
            }
        }

        private void Share_Email(object sender, RoutedEventArgs e)
        {
            string? tempFilePath = CreateTempShareFile();
            if (string.IsNullOrEmpty(tempFilePath)) return;

            try
            {
                var fileCollection = new StringCollection { tempFilePath };
                System.Windows.Clipboard.SetFileDropList(fileCollection);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Не удалось скопировать файл в буфер обмена: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string[] providers = { "Gmail", "Yandex", "Mail.ru", "Клиент по умолчанию" };
            string? choice = PromptSelection("Выбор почтового сервиса", "Выберите сервис для отправки письма:", providers);
            if (string.IsNullOrEmpty(choice)) return;

            string subject = Uri.EscapeDataString("Файл из CryptoNote");
            string body = Uri.EscapeDataString("Отправлено из приложения CryptoNote.");
            string url = "";

            switch (choice)
            {
                case "Gmail": url = $"https://mail.google.com/mail/?view=cm&fs=1&su={subject}&body={body}"; break;
                case "Yandex": url = $"https://mail.yandex.com/compose?subject={subject}&body={body}"; break;
                case "Mail.ru": url = $"https://e.mail.ru/compose?subject={subject}&body={body}"; break;
                case "Клиент по умолчанию": url = $"mailto:?subject={subject}&body={body}"; break;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Не удалось открыть почтовый сервис: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            System.Windows.MessageBox.Show("Ваш почтовый сервис открыт.\n\nФайл скопирован в буфер обмена. Просто нажмите Ctrl+V в окне письма, чтобы прикрепить его.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static string? PromptPassword(string message) => PromptInput(message, "", true);

        public static string? PromptInput(string message, string defaultValue = "", bool isPassword = false)
        {
            var inputWindow = new Window { Width = 300, Height = 150, Title = message, WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize, Background = System.Windows.Media.Brushes.DarkSlateGray };
            System.Windows.Controls.Control inputControl = isPassword ? new PasswordBox { Margin = new Thickness(10), Background = System.Windows.Media.Brushes.Black, Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0) } : new System.Windows.Controls.TextBox { Margin = new Thickness(10), Text = defaultValue, Background = System.Windows.Media.Brushes.Black, Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0) };
            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 70, Margin = new Thickness(10), HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Background = System.Windows.Media.Brushes.Black, Foreground = System.Windows.Media.Brushes.White };
            okButton.Click += (s, e) => inputWindow.DialogResult = true;
            var panel = new StackPanel();
            panel.Children.Add(inputControl);
            panel.Children.Add(okButton);
            inputWindow.Content = panel;
            if (inputWindow.ShowDialog() != true) return null;
            return isPassword ? (inputControl as PasswordBox)?.Password : (inputControl as System.Windows.Controls.TextBox)?.Text;
        }

        public static string? PromptSelection(string title, string message, string[] options)
        {
            var selectionWindow = new Window { Title = title, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = System.Windows.Media.Brushes.DarkSlateGray, ResizeMode = ResizeMode.NoResize };
            var panel = new StackPanel { Margin = new Thickness(15) };
            panel.Children.Add(new TextBlock { Text = message, Margin = new Thickness(5), FontSize = 14, Foreground = System.Windows.Media.Brushes.White, HorizontalAlignment = System.Windows.HorizontalAlignment.Center });
            string? selectedOption = null;
            var buttonPanel = new WrapPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
            foreach (var option in options)
            {
                var button = new System.Windows.Controls.Button { Content = option, Margin = new Thickness(5), Padding = new Thickness(10, 5, 10, 5), Background = System.Windows.Media.Brushes.Black, Foreground = System.Windows.Media.Brushes.White };
                button.Click += (s, e) => { selectedOption = option; selectionWindow.DialogResult = true; selectionWindow.Close(); };
                buttonPanel.Children.Add(button);
            }
            panel.Children.Add(buttonPanel);
            selectionWindow.Content = panel;
            selectionWindow.ShowDialog();
            return selectedOption;
        }

        private static string EncryptString(string text, string password)
        {
            using var aes = Aes.Create();
            aes.Key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password));
            aes.GenerateIV();

            byte[] encryptedBytes;
            using (var ms = new MemoryStream())
            {
                ms.Write(aes.IV, 0, aes.IV.Length);
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(text);
                }
                encryptedBytes = ms.ToArray();
            }

            return EncryptedHeader + "\n" + Convert.ToBase64String(encryptedBytes);
        }

        private static string DecryptString(string fileContent, string password)
        {
            string base64Content = fileContent.Substring(EncryptedHeader.Length).Trim();

            byte[] data = Convert.FromBase64String(base64Content);

            using var aes = Aes.Create();
            aes.Key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password));

            byte[] iv = new byte[16];
            Array.Copy(data, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var ms = new MemoryStream(data, 16, data.Length - 16);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
    }
}