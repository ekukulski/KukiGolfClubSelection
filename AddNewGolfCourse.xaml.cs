using Microsoft.Maui.Controls;
using System;
using System.IO;
using System.Linq;
using KukiGolfClubSelection.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KukiGolfClubSelection
{
    public partial class AddNewGolfCourse : ContentPage
    {
        private readonly string filePath;
        private readonly GolfDataService _golfDataService = new();

        public AddNewGolfCourse()
        {
            InitializeComponent();

            // Use FileSystem.AppDataDirectory for cross-platform compatibility
            // On Windows, this maps to LocalState: C:\Users\[Username]\AppData\Local\Packages\[PackageId]\LocalState
            filePath = Path.Combine(FileSystem.AppDataDirectory, "GolfCourseData.txt");
        }

        // ✅ FIX: your FindByName is non-generic; cast to Entry safely
        private Entry? FindEntry(string name) => FindByName(name) as Entry;

        /// <summary>
        /// Handles the Save button click. Validates input and saves the course data.
        /// </summary>
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                string? name = CourseNameEntry.Text?.Trim();
                string? tee = TeeEntry.Text?.Trim();
                string? yardage = YardageEntry.Text?.Trim();
                string? par = CourseParEntry.Text?.Trim();
                string? rating = CourseRatingEntry.Text?.Trim();
                string? slope = CourseSlopeRatingEntry.Text?.Trim();

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(tee))
                {
                    await DisplayAlert("Error", "Please enter Course Name and Tee.", "OK");
                    return;
                }

                // Optional: ensure these aren't null in the output line
                yardage ??= "";
                par ??= "";
                rating ??= "";
                slope ??= "";

                const int holeCount = 18;

                var handicapEntries = Enumerable.Range(1, holeCount)
                    .Select(i => (FindEntry($"HoleHandicap{i}")?.Text ?? "").Trim())
                    .ToList();

                var parEntries = Enumerable.Range(1, holeCount)
                    .Select(i => (FindEntry($"HolePar{i}")?.Text ?? "").Trim())
                    .ToList();

                var yardageEntries = Enumerable.Range(1, holeCount)
                    .Select(i => (FindEntry($"HoleYardage{i}")?.Text ?? "").Trim())
                    .ToList();

                if (handicapEntries.Any(string.IsNullOrWhiteSpace) ||
                    parEntries.Any(string.IsNullOrWhiteSpace) ||
                    yardageEntries.Any(string.IsNullOrWhiteSpace))
                {
                    await DisplayAlert("Error", $"Please fill in all {holeCount} Hole Handicaps, Pars, and Yardages.", "OK");
                    return;
                }

                // Save format: basic info, then handicaps, then pars, then yardages
                string line = string.Join(",", new[] { name, tee, yardage, par, rating, slope }
                    .Concat(handicapEntries)
                    .Concat(parEntries)
                    .Concat(yardageEntries));

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
                var handicapEntry = FindEntry($"HoleHandicap{i}");
                var parEntry = FindEntry($"HolePar{i}");
                var yardageEntry = FindEntry($"HoleYardage{i}");

                if (handicapEntry != null) handicapEntry.Text = string.Empty;
                if (parEntry != null) parEntry.Text = string.Empty;
                if (yardageEntry != null) yardageEntry.Text = string.Empty;
            }
        }

        private async void OnReturnClicked(object sender, EventArgs e)
        {
            await Navigation.PopToRootAsync();
        }

        private async Task ExportToOneDriveAsync()
        {
            try
            {
                var csvFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv" } },
                    { DevicePlatform.MacCatalyst, new[] { ".csv" } },
                    { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } },
                    { DevicePlatform.Android, new[] { "text/csv" } }
                });

                var fileResult = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select or Create a CSV file",
                    FileTypes = csvFileType
                });

                if (fileResult != null)
                {
                    string csvContent = string.Join(Environment.NewLine, File.ReadAllLines(filePath)
                        .Select(line => line + "," + string.Join(",", Enumerable.Repeat("", 18))));

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
