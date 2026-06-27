using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ReadyOrNotModManager.Core.Archives;

namespace ReadyOrNotModManager.App;

public partial class ArchiveSelectionWindow : Window
{
    private readonly ObservableCollection<ArchiveGroupChoice> _choices;

    public ArchiveSelectionWindow(IReadOnlyList<DeployableArchiveGroup> groups)
    {
        InitializeComponent();
        _choices = new ObservableCollection<ArchiveGroupChoice>(
            groups.Select(group => new ArchiveGroupChoice(group)));
        GroupsGrid.ItemsSource = _choices;
    }

    public IReadOnlyList<string> SelectedEntryPaths => _choices
        .Where(choice => choice.IsSelected)
        .SelectMany(choice => choice.EntryPaths)
        .ToArray();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var choice in _choices)
        {
            choice.IsSelected = true;
        }
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var choice in _choices)
        {
            choice.IsSelected = false;
        }
    }

    private void DeploySelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedEntryPaths.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "Choose at least one file group to deploy.", "Ready or Not Mod Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed class ArchiveGroupChoice : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public ArchiveGroupChoice(DeployableArchiveGroup group)
        {
            DisplayName = group.DisplayName;
            EntryPaths = group.Files.Select(file => file.EntryPath).ToArray();
            FilesDisplay = string.Join(", ", group.Files.Select(file => file.FileName));
        }

        public string DisplayName { get; }
        public string FilesDisplay { get; }
        public IReadOnlyList<string> EntryPaths { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
