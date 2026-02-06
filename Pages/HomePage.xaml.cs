using Microsoft.Maui.Controls;

namespace GolfClubSelectionApp
{
    public partial class HomePage : ContentPage
    {
        public HomePage()
        {
            InitializeComponent();
        }

        private async void OnAddNewCourseClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AddNewGolfCourse());
        }

        private async void OnClubAndDistanceClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new ClubAndDistancePage());
        }

        private async void OnClubSelectionClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new MainPage());
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
#if WINDOWS
            Application.Current?.Quit();
#endif
        }
    }
}
