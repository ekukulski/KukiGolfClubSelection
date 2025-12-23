namespace GolfClubSelectionApp.Models;

public class GolfCourse
{
    public string Name { get; set; }
    public string Tee { get; set; }
    public int Yardage { get; set; }
    public int Par { get; set; }
    public double CourseRating { get; set; }
    public int SlopeRating { get; set; }
    public List<int> Handicaps { get; set; }
    public List<int> HolePars { get; set; }
    public List<int> HoleYardages { get; set; }
}