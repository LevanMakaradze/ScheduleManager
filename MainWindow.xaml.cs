using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ScheduleManager.Models;
using ScheduleManager.Services;

namespace ScheduleManager
{
    public partial class MainWindow : Window
    {
        private const string FilePath = "classes.json";
        private ObservableCollection<ClassOption> Classes { get; set; }
        private List<List<ClassOption>> _generatedSchedules;
        private int _currentScheduleIndex = 0;

        public MainWindow()
        {
            InitializeComponent();
            Classes = new ObservableCollection<ClassOption>();
            ClassesDataGrid.ItemsSource = Classes; // Bind DataGrid to ObservableCollection
            LoadClassesFromJson(); // Load existing classes from JSON
            SetupEventHandlers();
        }

        private void ClassesDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // Customize column headers based on property names
            switch (e.PropertyName)
            {
                case "Subject":
                    e.Column.Header = "Subject";
                    break;
                case "LecturerName":
                    e.Column.Header = "Lecturer";
                    break;
                case "DayOfWeek":
                    e.Column.Header = "Day";
                    e.Column.ClipboardContentBinding.StringFormat = "DD";
                    break;
                case "StartTime":
                    e.Column.Header = "Start Time";
                    e.Column.ClipboardContentBinding.StringFormat = "HH:mm";
                    break;
                case "EndTime":
                    e.Column.Header = "End Time";
                    e.Column.ClipboardContentBinding.StringFormat = "HH:mm";
                    break;
                default:
                    break; // Keep default behavior for other properties
            }
        }
        private void SetupEventHandlers()
        {
            AddClassButton.Click += AddClassButton_Click;
            RemoveClassButton.Click += RemoveClassButton_Click;
            GenerateScheduleButton.Click += GenerateScheduleButton_Click;
            NextScheduleButton.Click += NextScheduleButton_Click;
            PreviousScheduleButton.Click += PreviousScheduleButton_Click;
        }

        private void LoadClassesFromJson()
        {
            if (!File.Exists(FilePath)) return;

            var json = File.ReadAllText(FilePath);
            var classList = JsonSerializer.Deserialize<ClassList>(json);

            if (classList != null)
            {
                foreach (var classOption in classList.Classes)
                {
                    Classes.Add(classOption); // Add to ObservableCollection
                }
            }
        }

        private void SaveClassesToJson()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(new ClassList { Classes = Classes.ToList() }, options);
            File.WriteAllText(FilePath, json);
        }

        private void AddClassButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateClassInputs(out ClassOption newClass)) return;

            // Confirmation dialog
            var result = MessageBox.Show($"Are you sure you want to add:\n{newClass}?", "Confirm Add", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No) return;

            Classes.Add(newClass); // Add directly to ObservableCollection
            SaveClassesToJson(); // Save changes
            ClearClassInputs();
            MessageBox.Show("Class added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoveClassButton_Click(object sender, RoutedEventArgs e)
        {
            var classToRemove = ClassesDataGrid.SelectedItem;
            if (classToRemove != null)
            {

                // Confirmation dialog
                var result = MessageBox.Show($"Are you sure you want to remove:\n{classToRemove}?", "Confirm Remove", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.No) return;
                var pattern = @"^(?<Subject>[\w\s]+) \((?<LecturerName>[\w\s]+)\) - (?<DayOfWeek>\w+) (?<StartTime>\d{2}:\d{2})-(?<EndTime>\d{2}:\d{2})$";
                var match = System.Text.RegularExpressions.Regex.Match(classToRemove.ToString(), pattern);

                string subject = match.Groups["Subject"].Value;
                string lecturerName = match.Groups["LecturerName"].Value;
                DayOfWeek dayOfWeek = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), match.Groups["DayOfWeek"].Value, true);
                TimeOnly startTime = TimeOnly.Parse(match.Groups["StartTime"].Value);
                TimeOnly endTime = TimeOnly.Parse(match.Groups["EndTime"].Value);

                var existingClass = Classes.FirstOrDefault(c => c.Subject == subject &&
                                                            c.LecturerName == lecturerName &&
                                                            c.DayOfWeek == dayOfWeek &&
                                                            c.StartTime == startTime &&
                                                            c.EndTime == endTime);


                if (existingClass != null)
                {
                    Classes.Remove(existingClass); // Remove from ObservableCollection
                    SaveClassesToJson(); // Save changes
                    ClearClassInputs();
                    MessageBox.Show("Class removed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Class not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select the class to remove!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void GenerateScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Classes.Count == 0)
                {
                    MessageBox.Show("No classes available to generate schedule.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var preferences = GetSelectedPreferences();
                var scheduleService = new ScheduleService(Classes.ToList());
                _generatedSchedules = scheduleService.GenerateSchedules(preferences);

                if (_generatedSchedules.Count == 0)
                {
                    MessageBox.Show("No valid schedules found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var scoredSchedules = _generatedSchedules.Select(schedule => new
                {
                    Schedule = schedule,
                    Score = new ScheduleService(Classes.ToList()).CalculateScheduleScore(schedule, preferences)
                }).ToList();


                _currentScheduleIndex = 0;
                DisplayCurrentSchedule();

                MessageBox.Show($"{_generatedSchedules.Count} Schedules generated successfully.\n",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating schedule: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




        private void NextScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedSchedules == null || _generatedSchedules.Count == 0)
                return;

            _currentScheduleIndex = (_currentScheduleIndex + 1) % _generatedSchedules.Count;
            DisplayCurrentSchedule();
        }

        private void PreviousScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedSchedules == null || _generatedSchedules.Count == 0)
                return;

            _currentScheduleIndex = (_currentScheduleIndex - 1 + _generatedSchedules.Count) % _generatedSchedules.Count;
            DisplayCurrentSchedule();
        }

        private bool ValidateClassInputs(out ClassOption newClass)
        {
            newClass = null;

            // Input validations
            if (string.IsNullOrWhiteSpace(SubjectTextBox.Text))
            {
                MessageBox.Show("Subject cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(LecturerTextBox.Text))
            {
                MessageBox.Show("Lecturer name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (DayComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a day.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TimeOnly.TryParse(StartTimeTextBox.Text, out TimeOnly startTime))
            {
                MessageBox.Show("Invalid start time format. Use HH:mm.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TimeOnly.TryParse(EndTimeTextBox.Text, out TimeOnly endTime))
            {
                MessageBox.Show("Invalid end time format. Use HH:mm.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (endTime <= startTime)
            {
                MessageBox.Show("End time must be after start time.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                newClass = new ClassOption(
                    SubjectTextBox.Text,
                    LecturerTextBox.Text,
                    (DayOfWeek)Enum.Parse(typeof(DayOfWeek), DayComboBox.Text),
                    startTime,
                    endTime,
                    AuditoryTextBox.Text
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating class: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private List<SchedulePreference> GetSelectedPreferences()
        {
            var preferences = new List<SchedulePreference>();

            // Existing preferences
            if (NoMorning.IsChecked == true)
                preferences.Add(SchedulePreference.NoMorningClasses);
            if (NoEvening.IsChecked == true)
                preferences.Add(SchedulePreference.NoEveningClasses);
            if (Morning.IsChecked == true)
                preferences.Add(SchedulePreference.PreferMorningClasses);
            if (Evening.IsChecked == true)
                preferences.Add(SchedulePreference.PreferEveningClasses);
            if (NoGapDays.IsChecked == true)
                preferences.Add(SchedulePreference.NoGapDays);
            if (NoGapHours.IsChecked == true)
                preferences.Add(SchedulePreference.NoGapHours);
            if (LeastDays.IsChecked == true)
                preferences.Add(SchedulePreference.LeastClassDays);
            if (MostDays.IsChecked == true)
                preferences.Add(SchedulePreference.MostClassDays);
            if (MinimizeEarlyMorning.IsChecked == true)
                preferences.Add(SchedulePreference.MinimizeEarlyMorningClasses);
            if (MaximizeBreakTime.IsChecked == true)
                preferences.Add(SchedulePreference.MaximizeBreakTimeBetweenClasses);

            return preferences;
        }

        private void DisplayCurrentSchedule()
        {
            if (_generatedSchedules == null || _generatedSchedules.Count == 0) return;

            var currentSchedule = _generatedSchedules[_currentScheduleIndex];
            CurrentScheduleIndex.Text = "Schedule " + (_currentScheduleIndex + 1).ToString() + "/" + _generatedSchedules.Count;
            PopulateScheduleTable(currentSchedule);
        }

        private void PopulateScheduleTable(List<ClassOption> schedule)
        {
            // Clear previous content except headers
            for (int row = 1; row <= 5; row++)
            {
                for (int col = 0; col <= 5; col++)
                {
                    var cell = ScheduleGrid.Children
                        .Cast<UIElement>()
                        .FirstOrDefault(e => Grid.GetRow(e) == row && Grid.GetColumn(e) == col);

                    if (cell != null)
                    {
                        ScheduleGrid.Children.Remove(cell);
                    }
                }
            }

            foreach (var classOption in schedule)
            {
                int row = classOption.StartTime.Hour switch
                {
                    <= 10 => 1,
                    <= 12 => 2,
                    <= 15 => 3,
                    <= 18 => 4,
                    _ => 5
                };

                int col = classOption.DayOfWeek switch
                {
                    DayOfWeek.Monday => 0,
                    DayOfWeek.Tuesday => 1,
                    DayOfWeek.Wednesday => 2,
                    DayOfWeek.Thursday => 3,
                    DayOfWeek.Friday => 4,
                    DayOfWeek.Saturday => 5,
                    _ => throw new Exception()
                };

                if (row > 0 && col >= 0)
                {
                    var textBlock = new TextBlock
                    {
                        Text = $"{classOption.Subject} \n{classOption.LecturerName}\n{classOption.StartTime:HH:mm}-{classOption.EndTime:HH:mm}",
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(5)
                    };

                    Grid.SetRow(textBlock, row);
                    Grid.SetColumn(textBlock, col);
                    ScheduleGrid.Children.Add(textBlock);
                }
            }
        }



        // View Model for DataGrid
        private class ScheduleViewModel
        {
            public string Hours { get; set; } // Row labels: Morning, Midday, Evening
            public string Monday { get; set; }
            public string Tuesday { get; set; }
            public string Wednesday { get; set; }
            public string Thursday { get; set; }
            public string Friday { get; set; }
            public string Saturday { get; set; }
            public string Sunday { get; set; }
        }

        private void ClearClassInputs()
        {
            SubjectTextBox.Text = string.Empty;
            LecturerTextBox.Text = string.Empty;
            DayComboBox.SelectedIndex = -1;
            StartTimeTextBox.Text = string.Empty;
            EndTimeTextBox.Text = string.Empty;
            AuditoryTextBox.Text = string.Empty;
        }
    }
}

