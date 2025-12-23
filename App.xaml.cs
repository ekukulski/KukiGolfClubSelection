using Microsoft.Maui.Controls;

namespace GolfClubSelectionApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new NavigationPage(new HomePage());
        }
    }
}