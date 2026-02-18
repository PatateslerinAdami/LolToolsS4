using LolFormats;
using Microsoft.Win32;
using System.Windows;
using System.Collections.Generic;

namespace LolEditor
{
    public partial class MainWindow : Window
    {
        private InibinDictionary _dictionary;
        private InibinFile _currentFile;

        public MainWindow()
        {
            InitializeComponent();
            _dictionary = InibinDictionary.LoadDefault();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Inibin Files (*.inibin)|*.inibin|All Files (*.*)|*.*";

            if (ofd.ShowDialog() == true)
            {
                LoadFile(ofd.FileName);
            }
        }

        private void LoadFile(string path)
        {
            var reader = new InibinReader();
            var rawFile = reader.Read(path);

            _currentFile = OrganizeFile(rawFile);

            FileTree.ItemsSource = _currentFile.Sections;
        }

        private InibinFile OrganizeFile(InibinFile rawFile)
        {
            var organizedFile = new InibinFile();
            organizedFile.Version = rawFile.Version;

            var sectionMap = new Dictionary<string, InibinSection>();

            InibinSection GetSection(string name)
            {
                if (!sectionMap.TryGetValue(name, out var section))
                {
                    uint secHash = InibinHash.Hash(name);

                    section = new InibinSection { Name = name, Hash = secHash };
                    sectionMap[name] = section;
                    organizedFile.Sections.Add(section);
                }
                return section;
            }

            foreach (var rawSection in rawFile.Sections)
            {
                foreach (var prop in rawSection.Properties)
                {
                    string knownName = _dictionary.GetName(prop.Hash);

                    if (knownName != null)
                    {
                        if (knownName.Contains("*"))
                        {
                            var parts = knownName.Split('*');
                            string secName = parts[0];
                            string propName = parts[1];

                            var targetSection = GetSection(secName);

                            prop.Name = propName; 
                            targetSection.Properties.Add(prop);
                        }
                        else
                        {
                            var targetSection = GetSection("Globals");
                            prop.Name = knownName;
                            targetSection.Properties.Add(prop);
                        }
                    }
                    else
                    {
                        var targetSection = GetSection("Unknowns");
                        prop.Name = $"Unknown_{prop.Hash}";
                        targetSection.Properties.Add(prop);
                    }
                }
            }
            organizedFile.Sections.Sort((a, b) => string.Compare(a.Name, b.Name));

            return organizedFile;
        }
        private void BtnTestHash_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Hash Guesser",
                Width = 300,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };

            var lblSec = new System.Windows.Controls.TextBlock { Text = "Section:" };
            var txtSec = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 0, 0, 10) };

            var lblProp = new System.Windows.Controls.TextBlock { Text = "Property:" };
            var txtProp = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 0, 0, 10) };

            var btnCheck = new System.Windows.Controls.Button { Content = "Check", Height = 30 };

            stack.Children.Add(lblSec);
            stack.Children.Add(txtSec);
            stack.Children.Add(lblProp);
            stack.Children.Add(txtProp);
            stack.Children.Add(btnCheck);
            dialog.Content = stack;

            btnCheck.Click += (s, args) =>
            {
                string section = txtSec.Text;
                string prop = txtProp.Text;

                uint hash = InibinHash.Hash(section, prop);

                bool found = false;

                if (_currentFile != null)
                {
                    foreach (var sec in _currentFile.Sections)
                    {
                        foreach (var p in sec.Properties)
                        {
                            if (p.Hash == hash)
                            {
                                MessageBox.Show($"Match Found\nHash: {hash}\nCurrent Value: {p.Value}\n\nYou should add '{section}*{prop}' to dictionary.txt");
                                found = true;
                                break;
                            }
                        }
                    }
                }

                if (!found)
                {
                    MessageBox.Show($"Hash {hash} calculated, but not found in this file.");
                }
            };

            dialog.ShowDialog();
        }
    }
}