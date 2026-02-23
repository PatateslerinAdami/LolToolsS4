using LolFormats;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;

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
            ofd.Filter = "League Files (*.inibin;*.troybin;*.luaobj)|*.inibin;*.troybin;*.luaobj|All Files (*.*)|*.*";

            if (ofd.ShowDialog() == true)
            {
                LoadFile(ofd.FileName);
            }
        }

        private void LoadFile(string path)
        {
            TxtSearch.Text = string.Empty;
            _currentFilePath = path;
            string extension = System.IO.Path.GetExtension(path).ToLower();
            if (extension == ".luaobj")
            {
                var reader = new LolFormats.LuaObjReader();
                var luaFile = reader.Read(path);

                var interpreter = new LolFormats.LuaInterpreter();
                var globals = interpreter.Interpret(luaFile);

                var fakeFile = new InibinFile();
                var mainSec = new InibinSection { Name = "Lua Globals" };

                int GetLuaTypeId(object obj)
                {
                    if (obj is string) return 12; // String
                    if (obj is bool) return 5;    // Boolean
                    if (obj is float || obj is double) return 1; // Float
                    if (obj is int || obj is long) return 0;     // Int
                    return 12; 
                }

                foreach (var kvp in globals)
                {
                    if (kvp.Value is Dictionary<object, object> subTable)
                    {
                        var subSec = new InibinSection { Name = kvp.Key.ToString() };

                        var sortedKeys = new List<object>(subTable.Keys);
                        sortedKeys.Sort((k1, k2) =>
                        {
                            if (k1 is double d1 && k2 is double d2) return d1.CompareTo(d2);
                            if (k1 is int i1 && k2 is int i2) return i1.CompareTo(i2);
                            return k1.ToString().CompareTo(k2.ToString());
                        });

                        foreach (var subKey in sortedKeys)
                        {
                            object val = subTable[subKey];

                            string displayName = (subKey is double || subKey is int)
                                ? $"[{subKey}]"
                                : subKey.ToString();

                            subSec.Properties.Add(new InibinProperty
                            {
                                Name = displayName,
                                Value = val,
                                Hash = 0,
                                TypeId = GetLuaTypeId(val)
                            });
                        }
                        fakeFile.Sections.Add(subSec);
                    }
                    else
                    {
                        mainSec.Properties.Add(new InibinProperty
                        {
                            Name = kvp.Key.ToString(),
                            Value = kvp.Value,
                            Hash = 0,
                            TypeId = GetLuaTypeId(kvp.Value)
                        });
                    }
                }

                if (mainSec.Properties.Count > 0)
                    fakeFile.Sections.Insert(0, mainSec);

                FileTree.ItemsSource = fakeFile.Sections;
                BtnSave.IsEnabled = true;
                BtnAddProp.IsEnabled = false;
                return;
            }
            if (extension == ".troybin")
            {
                _dictionary = InibinDictionary.LoadFromFile("troybin_dict.txt");
            }
            else
            {
                _dictionary = InibinDictionary.LoadFromFile("inibin_dict.txt");
            }

            var inibinReader = new InibinReader();
            var rawFile = inibinReader.Read(path);
            _currentFile = OrganizeFile(rawFile);
            if (path.EndsWith(".troybin"))
            {
                string propListPath = "troy_properties.txt";
                if (System.IO.File.Exists(propListPath))
                {
                    var props = System.IO.File.ReadAllLines(propListPath)
                                              .Where(l => !string.IsNullOrWhiteSpace(l))
                                              .Select(l => l.Trim());

                    var resolver = new LolFormats.TroybinResolver(props);
                    resolver.Resolve(_currentFile);
                }
            }
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
                string ext = System.IO.Path.GetExtension(_currentFilePath).ToLower();

                if (ext == ".luaobj")
                {
                    var writer = new LolFormats.LuaObjWriter();
                    writer.Write(_currentFilePath, _currentFile);
                }
                else
                {
                    var writer = new InibinWriter();
                    writer.Write(_currentFilePath, _currentFile);
                }

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
                menuItem.DataContext is InibinSection viewSection)
            {
                var realSection = _currentFile.Sections.FirstOrDefault(s => s.Hash == viewSection.Hash && s.Name == viewSection.Name);
                if (realSection == null) return;

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
                        newName = known != null ? (known.Contains("*") ? known.Split('*')[0] : known) : $"Unknown_{newHash}";
                    }
                    else
                    {
                        newHash = InibinHash.Hash(realSection.Name, dialog.PropName);
                        newName = dialog.PropName;
                    }

                    var newProp = new InibinProperty
                    {
                        Name = newName,
                        Hash = newHash,
                        TypeId = dialog.SelectedTypeId,
                        Value = ConvertValue(dialog.PropValue, dialog.SelectedTypeId)
                    };

                    realSection.Properties.Add(newProp);

                    if (!string.IsNullOrWhiteSpace(TxtSearch.Text)) ApplyFilter();
                    else FileTree.Items.Refresh();
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
                if (!string.IsNullOrWhiteSpace(TxtSearch.Text)) ApplyFilter();
                else FileTree.Items.Refresh();
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
                    if (!string.IsNullOrWhiteSpace(TxtSearch.Text)) ApplyFilter();
                    else FileTree.Items.Refresh();
                }
            }
        }
        private object ConvertValue(string input, int typeId)
        {
            try
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;

                if (typeId >= 6 && typeId <= 11)
                {
                    var parts = input.Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                    return parts.Select(p => float.Parse(p, culture)).ToArray();
                }
                switch (typeId)
                {
                    case 0: return int.Parse(input);
                    case 1: return float.Parse(input, culture);
                    case 2: return float.Parse(input, culture);
                    case 3: return short.Parse(input);
                    case 4: return byte.Parse(input);
                    case 5: return bool.Parse(input);
                    case 12: return input;
                    default: return input;
                }
            }
            catch
            {
                MessageBox.Show($"Could not convert '{input}' to the selected type.");
                return input; 
            }
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                TxtSearch.Focus();
                TxtSearch.SelectAll();
                e.Handled = true;
            }
        }
        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_currentFile == null) return;

            string query = TxtSearch.Text.ToLower();
            if (string.IsNullOrWhiteSpace(query))
            {
                FileTree.ItemsSource = _currentFile.Sections;
                return;
            }

            var filteredSections = new List<InibinSection>();
            foreach (var sec in _currentFile.Sections)
            {
                bool secMatches = sec.Name != null && sec.Name.ToLower().Contains(query);

                var matchingProps = sec.Properties.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(query)) ||
                    (p.ValueStr != null && p.ValueStr.ToLower().Contains(query)) ||
                    p.Hash.ToString().Contains(query)
                ).ToList();

                if (secMatches || matchingProps.Count > 0)
                {
                    filteredSections.Add(new InibinSection
                    {
                        Name = sec.Name,
                        Hash = sec.Hash,
                        Properties = secMatches ? sec.Properties.ToList() : matchingProps
                    });
                }
            }

            FileTree.ItemsSource = filteredSections;
        }
        private void BtnCopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile == null) return;

            var itemsToCopy = FileTree.ItemsSource as IEnumerable<InibinSection> ?? _currentFile.Sections;
            var sb = new StringBuilder();

            foreach (var sec in itemsToCopy)
            {
                sb.AppendLine($"");
                foreach (var prop in sec.Properties)
                {
                    sb.AppendLine($"{prop.Name} = {prop.ValueStr}");
                }
                sb.AppendLine();
            }

            CopyToClipboard(sb.ToString(), "All visible results copied to clipboard!");
        }
        private void BtnCopySection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is InibinSection sec)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"");
                foreach (var prop in sec.Properties)
                {
                    sb.AppendLine($"{prop.Name} = {prop.ValueStr}");
                }
                CopyToClipboard(sb.ToString());
            }
        }

        private void BtnCopyProperty_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is InibinProperty prop)
            {
                CopyToClipboard($"{prop.Name} = {prop.ValueStr}");
            }
        }

        private void CopyToClipboard(string text, string successMessage = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                Clipboard.SetText(text);
                if (successMessage != null)
                    MessageBox.Show(successMessage, "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show("Failed to copy to clipboard. Another application might be using it.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}