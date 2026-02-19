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
        private string _currentFilePath;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "League Files (*.inibin;*.troybin)|*.inibin;*.troybin|All Files (*.*)|*.*";

            if (ofd.ShowDialog() == true)
            {
                LoadFile(ofd.FileName);
            }
        }

        private void LoadFile(string path)
        {
            _currentFilePath = path;
            string extension = System.IO.Path.GetExtension(path).ToLower();

            if (extension == ".troybin")
            {
                _dictionary = InibinDictionary.LoadFromFile("troybin_dict.txt");
            }
            else
            {
                _dictionary = InibinDictionary.LoadFromFile("inibin_dict.txt");
            }
            var reader = new InibinReader();
            var rawFile = reader.Read(path);

            _currentFile = OrganizeFile(rawFile);

            FileTree.ItemsSource = _currentFile.Sections;

            BtnSave.IsEnabled = true;
            BtnAddProp.IsEnabled = true;
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
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile == null || string.IsNullOrEmpty(_currentFilePath)) return;

            try
            {
                string backupPath = _currentFilePath + ".bak";

                if (!System.IO.File.Exists(backupPath))
                {
                    System.IO.File.Copy(_currentFilePath, backupPath);
                }
                var writer = new InibinWriter();
                writer.Write(_currentFilePath, _currentFile);

                MessageBox.Show("File saved successfully!\nBackup created at: " + backupPath);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Error saving file: " + ex.Message);
            }
        }
        private void BtnAddProperty_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem &&
                menuItem.DataContext is InibinSection section)
            {
                var dialog = new AddPropertyWindow();
                dialog.Owner = this;

                if (dialog.ShowDialog() == true)
                {
                    uint newHash;
                    string newName;

                    if (dialog.UseRawHash)
                    {
                        newHash = dialog.FinalHash;

                        string known = _dictionary.GetName(newHash);
                        if (known != null)
                        {
                            newName = known.Contains("*") ? known.Split('*')[1] : known;
                        }
                        else
                        {
                            newName = $"Unknown_{newHash}";
                        }
                    }
                    else
                    {
                        newHash = InibinHash.Hash(section.Name, dialog.PropName);
                        newName = dialog.PropName;
                    }
                    var newProp = new InibinProperty
                    {
                        Name = newName,
                        Hash = newHash,
                        TypeId = dialog.SelectedTypeId,
                        Value = ConvertValue(dialog.PropValue, dialog.SelectedTypeId)
                    };

                    section.Properties.Add(newProp);
                    FileTree.Items.Refresh();
                }
            }
        }
        private void BtnAddProp_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile == null) return;

            var dialog = new AddPropertyWindow();
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                InibinSection targetSection = null;
                uint newHash = 0;
                string newName = "";

                if (dialog.UseRawHash)
                {
                    newHash = dialog.FinalHash;

                    string known = _dictionary.GetName(newHash);
                    newName = known != null ? known : $"Unknown_{newHash}";

                    targetSection = FindOrCreateSection("Unknowns");
                }
                else
                {
                    newHash = InibinHash.Hash(dialog.SectionName, dialog.PropName);
                    newName = dialog.PropName;

                    targetSection = FindOrCreateSection(dialog.SectionName);
                }

                var newProp = new InibinProperty
                {
                    Name = newName,
                    Hash = newHash,
                    TypeId = dialog.SelectedTypeId,
                    Value = ConvertValue(dialog.PropValue, dialog.SelectedTypeId)
                };

                targetSection.Properties.Add(newProp);
                FileTree.Items.Refresh();
            }
        }
        private InibinSection FindOrCreateSection(string name)
        {
            foreach (var sec in _currentFile.Sections)
            {
                if (sec.Name == name) return sec;
            }

            var newSec = new InibinSection
            {
                Name = name,
                Hash = InibinHash.Hash(name)
            };

            _currentFile.Sections.Add(newSec);
            _currentFile.Sections.Sort((a, b) => string.Compare(a.Name, b.Name));

            return newSec;
        }
        private void BtnDeleteProperty_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem &&
                menuItem.DataContext is InibinProperty propToDelete)
            {
                var result = MessageBox.Show($"Are you sure you want to delete '{propToDelete.Name}'?",
                                             "Confirm Delete",
                                             MessageBoxButton.YesNo,
                                             MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var section in _currentFile.Sections)
                    {
                        if (section.Properties.Contains(propToDelete))
                        {
                            section.Properties.Remove(propToDelete);
                            break; 
                        }
                    }
                    FileTree.Items.Refresh();
                }
            }
        }
        private object ConvertValue(string input, int typeId)
        {
            try
            {
                switch (typeId)
                {
                    case 0: return int.Parse(input);          // Int32
                    case 1: return float.Parse(input);        // Float
                    case 4: return byte.Parse(input);         // Byte
                    case 5: return bool.Parse(input);         // Bool
                    case 12: return input;                    // String
                    default: return input;
                }
            }
            catch
            {
                MessageBox.Show($"Could not convert '{input}' to the selected type.");
                return input; 
            }
        }
    }
}