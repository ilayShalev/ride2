using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.Services.MapServiceClasses
{
    /// <summary>
    /// Manages API key acquisition and storage
    /// </summary>
    public class ApiKeyManager
    {
        private string _apiKey;

        public ApiKeyManager(string apiKey = null)
        {
            _apiKey = apiKey;
        }

        public string GetApiKey()
        {
            if (!string.IsNullOrEmpty(_apiKey))
            {
                return _apiKey;
            }

            _apiKey = LoadApiKeyFromConfig();

            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = PromptForApiKey();
            }

            return _apiKey;
        }

        private string LoadApiKeyFromConfig()
        {
            return ConfigurationManager.AppSettings["GoogleApiKey"];
        }

        private string PromptForApiKey()
        {
            string enteredKey = string.Empty;

            using (var form = new Form())
            {
                form.Width = 400;
                form.Height = 150;
                form.Text = "Google API Key Required";

                var label = new Label { Left = 20, Top = 20, Text = "Please enter your Google Maps API Key:", Width = 360 };
                var textBox = new TextBox { Left = 20, Top = 50, Width = 360 };
                var button = new Button { Text = "OK", Left = 160, Top = 80, DialogResult = DialogResult.OK };

                form.Controls.Add(label);
                form.Controls.Add(textBox);
                form.Controls.Add(button);
                form.AcceptButton = button;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    enteredKey = textBox.Text;
                    SaveApiKeyToConfig(enteredKey);
                }
            }

            return enteredKey;
        }

        private void SaveApiKeyToConfig(string apiKey)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["GoogleApiKey"].Value = apiKey;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch
            {
                // Silently handle config save errors
            }
        }
    }

}
