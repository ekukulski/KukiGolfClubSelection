using Microsoft.Maui.Controls;
using GolfClubSelectionApp.Services;

namespace GolfClubSelectionApp
{
    public partial class HomePage : ContentPage
    {
        public HomePage()
        {
            InitializeComponent();
            WindowCenteringService.CenterWindow(600, 600);
        }
        /// <summary>
        /// Called when the page appears on screen.
        /// Ensures the window is centered again each time it appears.
        /// </summary>
        protected override void OnAppearing()
        {
            base.OnAppearing();
            WindowCenteringService.CenterWindow(600, 600);
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
#if ANDROID
            Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
#elif WINDOWS
            Application.Current.Quit();
#endif
        }
    }
}