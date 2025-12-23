using Microsoft.Maui.Controls;
using System;
using System.IO;
using System.Linq;
using GolfClubSelectionApp.Services;
using System.Collections.Generic;

namespace GolfClubSelectionApp
{
    public partial class AddNewGolfCourse : ContentPage
    {
        private readonly string filePath;
        private readonly GolfDataService _golfDataService = new();

        public AddNewGolfCourse()
        {
            InitializeComponent();
            WindowCenteringService.CenterWindow(500, 975);
            
            // Use FileSystem.AppDataDirectory for cross-platform compatibility
            // On Windows, this maps to LocalState: C:\Users\[Username]\AppData\Local\Packages\[PackageId]\LocalState
            filePath = Path.Combine(FileSystem.AppDataDirectory, "GolfCourseData.txt");
        }

        /// <summary>
        /// Handles the Save button click. Validates input and saves the course data.
        /// </summary>
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                string name = CourseNameEntry.Text?.Trim();
                string tee = TeeEntry.Text?.Trim();
                string yardage = YardageEntry.Text?.Trim();
                string par = CourseParEntry.Text?.Trim();
                string rating = CourseRatingEntry.Text?.Trim();
                string slope = CourseSlopeRatingEntry.Text?.Trim();

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(tee))
                {
                    await DisplayAlert("Error", "Please enter Course Name and Tee.", "OK");
                    return;
                }

                int holeCount = 18;

                var handicapEntries = Enumerable.Range(1, holeCount)
                    .Select(i => (this.FindByName<Entry>($"HoleHandicap{i}")?.Text ?? "").Trim())
                    .ToList();

                var parEntries = Enumerable.Range(1, holeCount)
                    .Select(i => (this.FindByName<Entry>($"HolePar{i}")?.Text ?? "").Trim())
                    .ToList();

                var yardageEntries = Enumerable.Range(1, holeCount)
                    .Select(i => (this.FindByName<Entry>($"HoleYardage{i}")?.Text ?? "").Trim())
                    .ToList();

                if (handicapEntries.Any(s => string.IsNullOrWhiteSpace(s)) ||
                    parEntries.Any(s => string.IsNullOrWhiteSpace(s)) ||
                    yardageEntries.Any(s => string.IsNullOrWhiteSpace(s)))
                {
                    await DisplayAlert("Error", $"Please fill in all {holeCount} Hole Handicaps, Pars, and Yardages.", "OK");
                    return;
                }

                // Save format: basic info, then handicaps, then pars, then yardages
                string line = string.Join(",", new[] { name, tee, yardage, par, rating, slope }
                    .Concat(handicapEntries)
                    .Concat(parEntries)
                    .Concat(yardageEntries));

                // Ensure directory exists (FileSystem.AppDataDirectory is created by MAUI, but being defensive)
                Directory.CreateDirectory(FileSystem.AppDataDirectory);

                File.AppendAllText(filePath, line + Environment.NewLine);

                await DisplayAlert("Success", "Course saved successfully.", "OK");
                ClearFields();
                await _golfDataService.ExportDatabaseAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save course: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Clears all form fields after saving or reset.
        /// </summary>
        private void ClearFields()
        {
            CourseNameEntry.Text = string.Empty;
            TeeEntry.Text = string.Empty;
            YardageEntry.Text = string.Empty;
            CourseParEntry.Text = string.Empty;
            CourseRatingEntry.Text = string.Empty;
            CourseSlopeRatingEntry.Text = string.Empty;

            for (int i = 1; i <= 18; i++)
            {
                var handicapEntry = this.FindByName<Entry>($"HoleHandicap{i}");
                var parEntry = this.FindByName<Entry>($"HolePar{i}");
                var yardageEntry = this.FindByName<Entry>($"HoleYardage{i}");

                if (handicapEntry != null) handicapEntry.Text = string.Empty;
                if (parEntry != null) parEntry.Text = string.Empty;
                if (yardageEntry != null) yardageEntry.Text = string.Empty;
            }
        }

        /// <summary>
        /// Handles the Return button click. Navigates back to the previous page.
        /// </summary>
        private async void OnReturnClicked(object sender, EventArgs e)
        {
            await Navigation.PopToRootAsync();
        }

        /// <summary>
        /// Exports the saved golf course data to OneDrive as a CSV file.
        /// </summary>
        private async Task ExportToOneDriveAsync()
        {
            try
            {
                // Define the CSV file type for the file picker
                var csvFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv" } },
                    { DevicePlatform.MacCatalyst, new[] { ".csv" } },
                    { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } },
                    { DevicePlatform.Android, new[] { "text/csv" } }
                });

                // Let the user choose where to save the file in their OneDrive
                var fileResult = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select or Create a CSV file",
                    FileTypes = csvFileType
                });

                if (fileResult != null)
                {
                    // Read the existing file (if any) and append the new data
                    string csvContent = string.Join(Environment.NewLine, File.ReadAllLines(filePath)
                        .Select(line => line + "," + string.Join(",", Enumerable.Repeat("", 18)))); // Adding empty columns for new holes

                    using (var stream = await fileResult.OpenReadAsync())
                    using (var writer = new StreamWriter(stream))
                    {
                        await writer.WriteAsync(csvContent);
                    }

                    await DisplayAlert("Success", "Data exported to OneDrive successfully.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to export data: {ex.Message}", "OK");
            }
        }
    }
}