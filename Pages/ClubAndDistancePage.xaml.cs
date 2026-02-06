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

                // ✅ FIX: DisplayAlert is async, so await it
                await DisplayAlert("Saved", "Club and distance data updated.", "OK");

                // ✅ Keep this awaited (already correct)
                await _golfDataService.ExportDatabaseAsync();
            }
            catch (Exception ex)
            {
                // ✅ FIX: DisplayAlert is async, so await it
                await DisplayAlert("Error", $"Failed to save club data: {ex.Message}", "OK");
            }
        }

        private async void OnReturnClicked(object sender, EventArgs e)
        {
            await Navigation.PopToRootAsync();
        }
    }

    public class ClubDistance
    {
        public string? Club { get; set; }
        public int MaxDistance { get; set; }
    }
}
