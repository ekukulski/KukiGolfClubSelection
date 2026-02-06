namespace GolfClubSelectionApp.Models;

public class GolfCourse
{
    public string Name { get; set; } = "";
    public string Tee { get; set; } = "";
    public int Yardage { get; set; }
    public int Par { get; set; }
    public double CourseRating { get; set; }
    public int SlopeRating { get; set; }
    public int[] Handicaps { get; set; } =Array.Empty<int>();
    public int[] HolePars { get; set; } = Array.Empty<int>();
    public int[] HoleYardages { get; set; } = Array.Empty<int>();
}