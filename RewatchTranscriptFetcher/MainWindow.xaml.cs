using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using RewatchTranscriptFetcher.Helpers;
using RewatchTranscriptFetcher.Models;
using RewatchTranscriptFetcher.Services;

namespace RewatchTranscriptFetcher
{
    public partial class MainWindow : Window
    {
        private readonly RewatchService _rewatchService;
        private readonly EncryptionService _encryptionService;

        public MainWindow()
        {
            InitializeComponent();
            _rewatchService = new RewatchService();
            _encryptionService = new EncryptionService();

            SetDefaultDates();
            LoadSettings();
        }

        private void SetDefaultDates()
        {
            DateTime today = DateTime.Today;
            StartDatePicker.SelectedDate = today;
            EndDatePicker.SelectedDate = today;
        }

        private void LoadSettings()
        {
            var settings = JsonHelper.LoadSettings();
            if (settings != null)
            {
                DomainTextBox.Text = settings.Domain;
                ApiKeyPasswordBox.Password = _encryptionService.Decrypt(settings.EncryptedApiKey);
            }
        }

        private void SaveSettings(string domain, string apiKey)
        {
            var settings = new RewatchSettings
            {
                Domain = domain,
                EncryptedApiKey = _encryptionService.Encrypt(apiKey)
            };
            JsonHelper.SaveSettings(settings);
        }

        private async void OnFetchAndSaveClick(object sender, RoutedEventArgs e)
        {
            string subdomain = DomainTextBox.Text;
            string apiKey = ApiKeyPasswordBox.Password;
            DateTime startDate = StartDatePicker.SelectedDate ?? DateTime.Today;
            DateTime endDate = EndDatePicker.SelectedDate ?? DateTime.Today;

            if (string.IsNullOrWhiteSpace(subdomain) || string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show("Please enter both the subdomain and API key.");
                return;
            }

            SaveSettings(subdomain, apiKey);

            SpinnerGrid.Visibility = Visibility.Visible;

            try
            {
                var transcripts = await _rewatchService.FetchTranscriptsAsync(subdomain, apiKey, startDate, endDate);
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    FileName = $"Transcripts_{DateTime.Today:yyyyMMdd}.txt",
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, transcripts);
                    MessageBox.Show("Transcripts saved successfully.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
            finally
            {
                SpinnerGrid.Visibility = Visibility.Collapsed;
            }
        }
    }
}
