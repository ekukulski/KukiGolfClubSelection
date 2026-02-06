using System.Collections.ObjectModel;
using System.IO;
using GolfClubSelectionApp.Services;

namespace GolfClubSelectionApp
{
    public partial class ClubAndDistancePage : ContentPage
    {
        private readonly string clubDistancePath;
        private readonly GolfDataService _golfDataService = new();
        public ObservableCollection<ClubDistance> ClubDistances { get; set; } = new();

        public ClubAndDistancePage()
        {
            InitializeComponent();
            
            // Use FileSystem.AppDataDirectory for cross-platform compatibility
            // On Windows, this maps to LocalState: C:\Users\[Username]\AppData\Local\Packages\[PackageId]\LocalState
            clubDistancePath = Path.Combine(FileSystem.AppDataDirectory, "ClubAndDistance.txt");

            LoadClubDistances();
            BindingContext = this;
        }

        private void LoadClubDistances()
        {
            ClubDistances.Clear();
            if (File.Exists(clubDistancePath))
            {
                var lines = File.ReadAllLines(clubDistancePath);
                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int distance))
                    {
                        ClubDistances.Add(new ClubDistance { Club = parts[0].Trim(), MaxDistance = distance });
                    }
                }
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                var lines = ClubDistances.Select(cd => $"{cd.Club},{cd.MaxDistance}");
                
                // Ensure directory exists (FileSystem.AppDataDirectory is created by MAUI, but being defensive)
                Directory.CreateDirectory(FileSystem.AppDataDirectory);
                
                File.WriteAllLines(clubDistancePath, lines);
                DisplayAlert("Saved", "Club and distance data updated.", "OK");
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"Failed to save club data: {ex.Message}", "OK");
            }

            await _golfDataService.ExportDatabaseAsync();
        }
        
        private async void OnReturnClicked(object sender, EventArgs e)
        {
            await Navigation.PopToRootAsync();
        }
        
        private async Task ExportToOneDriveAsync()
        {
            // Implementation for exporting data to OneDrive
            try
            {
                var fileName = "ClubAndDistanceBackup.txt";
                var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

                // Create or overwrite the file with the current club distances
                using (var streamWriter = new StreamWriter(filePath))
                {
                    foreach (var clubDistance in ClubDistances)
                    {
                        await streamWriter.WriteLineAsync($"{clubDistance.Club},{clubDistance.MaxDistance}");
                    }
                }

                // Now, use the OneDrive SDK or API to upload 'filePath' to the user's OneDrive
                // This part requires proper authentication and initialization of OneDrive client
                await _golfDataService.ExportDatabaseAsync();

                await DisplayAlert("Export", "Data exported to OneDrive successfully.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to export data: {ex.Message}", "OK");
            }
        }
    }

    public class ClubDistance
    {
        public string? Club { get; set; }
        public int MaxDistance { get; set; }
    }
}