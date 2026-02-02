using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GolfClubSelectionApp.Models;
using GolfClubSelectionApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MauiColor = Microsoft.Maui.Graphics.Color;
using MauiColors = Microsoft.Maui.Graphics.Colors;
using PdfColors = QuestPDF.Helpers.Colors;
using PdfContainer = QuestPDF.Infrastructure.IContainer;

namespace GolfClubSelectionApp
{
    public partial class MainPage : ContentPage
    {
        private readonly string courseDataPath;
        private readonly Dictionary<string, GolfCourse> courses = new();

        private readonly string clubDistancePath;
        private List<(string Club, int MaxDistance)> clubDistances = new();

        private string selectedDefaultDriver;
        private int[] previousStrokesPerHole = null;
        private string[] changeClubSelections = new string[18];
        private Picker[] changeClubPickers = new Picker[18];
        private bool[] yellowHighlightColumns = new bool[18];

        private readonly GolfDataService _golfDataService = new();

        public MainPage()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            courseDataPath = Path.Combine(FileSystem.AppDataDirectory, "GolfCourseData.txt");
            clubDistancePath = Path.Combine(FileSystem.AppDataDirectory, "ClubAndDistance.txt");

            InitializeComponent();

            // Import from Proton Drive (replaces OneDrive)
            Task.Run(async () => await ImportFromProtonDriveAsync()).Wait();

            LoadClubDistances();
            LoadCourses();

            // Initialize default driver picker only if we have club data
            if (defaultDriverPicker != null && clubDistances.Count > 0)
            {
                defaultDriverPicker.ItemsSource = clubDistances.Select(c => c.Club).ToList();
                defaultDriverPicker.SelectedIndex = 0;
                selectedDefaultDriver = clubDistances[0].Club;
                defaultDriverPicker.SelectedIndexChanged += OnDefaultDriverChanged;
            }
            else if (defaultDriverPicker != null)
            {
                // No club data available - set a message or default empty state
                defaultDriverPicker.ItemsSource = new List<string> { "No club data found" };
                defaultDriverPicker.SelectedIndex = 0;
                defaultDriverPicker.SelectedIndexChanged += OnDefaultDriverChanged;
            }

            coursePicker.SelectedIndexChanged += OnCourseSelected;
        }

        /// <summary>
        /// Returns: C:\Users\<AnyUser>\Proton Drive\ekukulski\My files\Documents\Golf
        /// Works on any Windows PC/user. Folder "ekukulski" is fixed by design.
        /// </summary>
        private static string ProtonGolfFolder
        {
            get
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                return Path.Combine(
                    userProfile,
                    "Proton Drive",
                    "ekukulski",         // MUST NOT CHANGE
                    "My files",
                    "Documents",
                    "Golf"
                );
            }
        }

        private void LoadClubDistances()
        {
            clubDistances.Clear();
            if (File.Exists(clubDistancePath))
            {
                var lines = File.ReadAllLines(clubDistancePath);
                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int distance))
                    {
                        clubDistances.Add((parts[0].Trim(), distance));
                    }
                }
            }
        }

        private void LoadCourses()
        {
            courses.Clear();

            if (!File.Exists(courseDataPath))
            {
                coursePicker.ItemsSource = new List<string> { "No course file found" };
                return;
            }

            var lines = File.ReadAllLines(courseDataPath)
                .Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 60) continue;

                var course = new GolfCourse
                {
                    Name = parts[0].Trim(),
                    Tee = parts[1].Trim(),
                    Yardage = int.TryParse(parts[2], out var y) ? y : 0,
                    Par = int.TryParse(parts[3], out var p) ? p : 0,
                    CourseRating = double.TryParse(parts[4], out var cr) ? cr : 0,
                    SlopeRating = int.TryParse(parts[5], out var sr) ? sr : 0,
                    Handicaps = parts.Skip(6).Take(18).Select(s => int.TryParse(s, out var v) ? v : 0).ToList(),
                    HolePars = parts.Skip(24).Take(18).Select(s => int.TryParse(s, out var v) ? v : 0).ToList(),
                    HoleYardages = parts.Skip(42).Take(18).Select(s => int.TryParse(s, out var v) ? v : 0).ToList()
                };

                if (course.Handicaps.Count == 18 && course.HolePars.Count == 18 && course.HoleYardages.Count == 18)
                    courses[course.Name] = course;
            }

            // Sort course names alphabetically
            var sortedCourseNames = courses.Keys.OrderBy(name => name).ToList();
            coursePicker.ItemsSource = sortedCourseNames;
        }

        private void OnDefaultDriverChanged(object sender, EventArgs e)
        {
            if (defaultDriverPicker.SelectedIndex >= 0 && clubDistances.Count > 0)
            {
                selectedDefaultDriver = defaultDriverPicker.SelectedItem.ToString();

                for (int i = 0; i < 18; i++)
                {
                    changeClubSelections[i] = null;
                    if (changeClubPickers[i] != null)
                        changeClubPickers[i].SelectedIndex = -1;
                }

                if (coursePicker.SelectedItem != null && courses.TryGetValue(coursePicker.SelectedItem.ToString(), out var course))
                {
                    var newStrokesPerHole = CalculateStrokesPerHoleArray(course, changeClubSelections);
                    bool[] highlight = new bool[18];
                    if (previousStrokesPerHole != null)
                    {
                        for (int i = 0; i < 18; i++)
                            highlight[i] = previousStrokesPerHole[i] != newStrokesPerHole[i];
                    }
                    previousStrokesPerHole = newStrokesPerHole;

                    for (int i = 0; i < 18; i++)
                        yellowHighlightColumns[i] = highlight[i];

                    OnCourseSelected(this, EventArgs.Empty, highlight, changeClubSelections.ToArray(), MauiColors.Yellow, yellowHighlightColumns, -1);
                }
                else
                {
                    Array.Clear(yellowHighlightColumns, 0, yellowHighlightColumns.Length);
                    OnCourseSelected(this, EventArgs.Empty, null, changeClubSelections.ToArray(), null, yellowHighlightColumns, -1);
                }
            }
        }

        private void OnChangeClubPickerChanged(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            if (picker == null) return;

            int holeIndex = Array.IndexOf(changeClubPickers, picker);
            if (holeIndex < 0 || holeIndex >= 18) return;

            string selectedClub = picker.SelectedItem as string;
            changeClubSelections[holeIndex] = selectedClub;

            if (coursePicker.SelectedItem != null && courses.TryGetValue(coursePicker.SelectedItem.ToString(), out var course))
            {
                var newStrokesPerHole = CalculateStrokesPerHoleArray(course, changeClubSelections);
                bool[] highlight = new bool[18];
                if (previousStrokesPerHole != null)
                {
                    for (int i = 0; i < 18; i++)
                        highlight[i] = (i == holeIndex) && (previousStrokesPerHole[i] != newStrokesPerHole[i]);
                }
                previousStrokesPerHole = newStrokesPerHole;

                if (highlight[holeIndex])
                    yellowHighlightColumns[holeIndex] = false;

                OnCourseSelected(this, EventArgs.Empty, highlight, changeClubSelections.ToArray(), MauiColors.LightGreen, yellowHighlightColumns, holeIndex);
            }
        }

        private void OnCourseSelected(object sender, EventArgs e) =>
            OnCourseSelected(sender, e, null, changeClubSelections.ToArray(), null, yellowHighlightColumns, -1);

        private void OnCourseSelected(object sender, EventArgs e, bool[] highlightStrokes, string[] changeClubOverride, MauiColor? highlightColor, bool[] yellowHighlightColumns, int greenColumnIndex)
        {
            if (coursePicker.SelectedItem == null) return;

            var selectedCourse = coursePicker.SelectedItem.ToString();
            if (!courses.ContainsKey(selectedCourse)) return;

            var course = courses[selectedCourse];

            if (course.Handicaps.Count != 18 || course.HolePars.Count != 18 || course.HoleYardages.Count != 18)
            {
                DisplayAlert("Data Error", "Course data is incomplete or corrupt.", "OK");
                return;
            }

            // Set course name label background color (light blue)
            courseNameLabel.BackgroundColor = MauiColors.LightBlue;

            courseInfoPanel.IsVisible = true;
            courseNameLabel.Text = $"{course.Name} ({course.Tee})";
            courseDetailsLabel.Text = $"Yardage: {course.Yardage} | Par: {course.Par} | Rating: {course.CourseRating} | Slope: {course.SlopeRating}";

            courseGrid.Children.Clear();
            courseGrid.ColumnDefinitions.Clear();
            courseGrid.RowDefinitions.Clear();

            courseGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            for (var i = 1; i < 20; i++)
                courseGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var baseLabels = new[] {
                "Hole", "Handicap", "Par", "Bogie",
                "Yardage 1", "Stroke 1", "Yardage 2", "Stroke 2", "Yardage 3", "Stroke 3",
                "Yardage 4", "Stroke 4", "Yardage 5", "Stroke 5", "Yardage 6", "Stroke 6"
            };

            var rowLabels = new List<string>();
            for (var i = 0; i < baseLabels.Length; i++)
            {
                rowLabels.Add(baseLabels[i]);
                if (baseLabels[i] == "Bogie" ||
                    (baseLabels[i].StartsWith("Stroke") && baseLabels[i] != "Stroke 6"))
                {
                    rowLabels.Add("");
                }
            }

            var allHoleStrokes = new List<List<string>>();
            for (var h = 0; h < 18; h++)
            {
                allHoleStrokes.Add(CalculateStrokesForHole(course.HoleYardages[h], changeClubOverride?[h]));
            }

            for (var i = 0; i < rowLabels.Count + 3; i++)
                courseGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            // Add "Hole" row with yellow background
            var holeRowColor = MauiColors.Yellow;
            courseGrid.Add(new Label { Text = "Hole", FontAttributes = FontAttributes.Bold, BackgroundColor = holeRowColor }, 0, 0);
            for (var i = 1; i <= 18; i++)
                courseGrid.Add(new Label { Text = i.ToString(), HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = holeRowColor }, i, 0);
            courseGrid.Add(new Label { Text = "Total", FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = holeRowColor }, 19, 0);

            var dataRow = 0;
            var parTotal = 0;
            var bogieTotal = 0;
            var strokesPerHole = new int[18];
            var strokeRowTotals = new int[6];

            for (var row = 1; row < rowLabels.Count; row++)
            {
                var label = rowLabels[row];

                // Determine row background color
                MauiColor? rowBg = null;
                if (label == "Handicap" || label == "Bogie")
                    rowBg = MauiColor.FromArgb("#FFBF00"); // Amber
                else if (label == "Par")
                    rowBg = MauiColors.Yellow;
                else if (label.StartsWith("Yardage"))
                    rowBg = MauiColors.LightGreen;
                else if (label.StartsWith("Stroke"))
                    rowBg = MauiColors.Yellow;
                else if (label == "Strokes")
                    rowBg = MauiColors.LightGreen;

                // Add the label for the row header
                courseGrid.Add(new Label
                {
                    Text = label,
                    FontAttributes = string.IsNullOrWhiteSpace(label) ? FontAttributes.None : FontAttributes.Bold,
                    BackgroundColor = rowBg ?? MauiColors.Transparent
                }, 0, row);

                if (!string.IsNullOrWhiteSpace(label))
                {
                    if (label == "Handicap")
                    {
                        for (var i = 0; i < 18; i++)
                            courseGrid.Add(new Label { Text = course.Handicaps[i].ToString(), HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = rowBg ?? MauiColors.Transparent }, i + 1, row);
                        courseGrid.Add(new Label { Text = "", HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = rowBg ?? MauiColors.Transparent }, 19, row);
                    }
                    else if (label == "Par")
                    {
                        for (var i = 0; i < 18; i++)
                        {
                            parTotal += course.HolePars[i];
                            courseGrid.Add(new Label { Text = course.HolePars[i].ToString(), HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = rowBg ?? MauiColors.Transparent }, i + 1, row);
                        }
                        courseGrid.Add(new Label { Text = parTotal.ToString(), FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = rowBg ?? MauiColors.Transparent }, 19, row);
                    }
                    else if (label == "Bogie")
                    {
                        for (var i = 0; i < 18; i++)
                        {
                            var bogie = course.HolePars[i] + 1;
                            bogieTotal += bogie;
                            courseGrid.Add(new Label { Text = bogie.ToString(), HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = rowBg ?? MauiColors.Transparent }, i + 1, row);
                        }
                        courseGrid.Add(new Label { Text = bogieTotal.ToString(), FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = rowBg ?? MauiColors.Transparent }, 19, row);
                    }
                    else if (label.StartsWith("Stroke"))
                    {
                        var strokeIdx = int.Parse(label.Split(' ')[1]) - 1;
                        var rowTotal = 0;
                        for (var i = 0; i < 18; i++)
                        {
                            var club = (dataRow < allHoleStrokes[i].Count) ? allHoleStrokes[i][dataRow] : "";
                            if (club != "-") { rowTotal++; strokesPerHole[i]++; }
                            courseGrid.Add(new Label { Text = club, HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = rowBg ?? MauiColors.Transparent }, i + 1, row);
                        }
                        strokeRowTotals[strokeIdx] = rowTotal;
                        courseGrid.Add(new Label { Text = rowTotal.ToString(), FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = rowBg ?? MauiColors.Transparent }, 19, row);
                        dataRow++;
                    }
                    else if (label.StartsWith("Yardage"))
                    {
                        for (var i = 0; i < 18; i++)
                        {
                            var text = (dataRow < allHoleStrokes[i].Count) ? allHoleStrokes[i][dataRow] : "";
                            courseGrid.Add(new Label { Text = text, HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = rowBg ?? MauiColors.Transparent }, i + 1, row);
                        }
                        courseGrid.Add(new Label { Text = "", HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = rowBg ?? MauiColors.Transparent }, 19, row);
                        dataRow++;
                    }
                    else if (label == "Strokes")
                    {
                        for (var i = 0; i < 18; i++)
                        {
                            var labelCtrl = new Label
                            {
                                Text = strokesPerHole[i].ToString(),
                                HorizontalTextAlignment = TextAlignment.Center,
                                BackgroundColor = rowBg ?? MauiColors.Transparent
                            };

                            if (highlightStrokes != null && highlightStrokes.Length > i && highlightStrokes[i] && highlightColor != null)
                                labelCtrl.BackgroundColor = (MauiColor)highlightColor;
                            else if (yellowHighlightColumns != null && yellowHighlightColumns.Length > i && yellowHighlightColumns[i])
                                labelCtrl.BackgroundColor = MauiColors.Yellow;

                            courseGrid.Add(labelCtrl, i + 1, row);
                        }
                        courseGrid.Add(new Label { Text = strokesPerHole.Sum().ToString(), FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = rowBg ?? MauiColors.Transparent }, 19, row);
                    }
                }
                else
                {
                    for (var i = 1; i < 20; i++)
                        courseGrid.Add(new Label { Text = "", BackgroundColor = MauiColors.Transparent }, i, row);
                }
            }

            var blankRowIdx = rowLabels.Count;
            for (var i = 0; i < 20; i++)
                courseGrid.Add(new Label { Text = "", BackgroundColor = MauiColors.Transparent }, i, blankRowIdx);

            var strokesRowIdx = rowLabels.Count + 1;
            courseGrid.Add(new Label
            {
                Text = "Strokes",
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = MauiColors.LightGreen
            }, 0, strokesRowIdx);

            var strokesTotal = 0;
            for (var i = 0; i < 18; i++)
            {
                var labelCtrl = new Label
                {
                    Text = strokesPerHole[i].ToString(),
                    HorizontalTextAlignment = TextAlignment.Center,
                    BackgroundColor = MauiColors.LightGreen
                };

                if (highlightStrokes != null && highlightStrokes.Length > i && highlightStrokes[i] && highlightColor != null)
                    labelCtrl.BackgroundColor = (MauiColor)highlightColor;
                else if (yellowHighlightColumns != null && yellowHighlightColumns.Length > i && yellowHighlightColumns[i])
                    labelCtrl.BackgroundColor = MauiColors.Yellow;

                courseGrid.Add(labelCtrl, i + 1, strokesRowIdx);
                strokesTotal += strokesPerHole[i];
            }
            courseGrid.Add(new Label { Text = strokesTotal.ToString(), FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, BackgroundColor = MauiColors.LightGreen }, 19, strokesRowIdx);

            var changeClubRowIdx = rowLabels.Count + 2;

            // "Change Club" label in FloralWhite
            courseGrid.Add(new Microsoft.Maui.Controls.Label
            {
                Text = "Change Club",
                TextColor = MauiColor.FromArgb("#FFFFFAF0"), // FloralWhite
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Start,
                VerticalTextAlignment = TextAlignment.Center,
                BackgroundColor = MauiColors.Transparent,
                Style = null // ensures it won't be overridden by implicit Label styles in the Grid
            }, 0, changeClubRowIdx);

            for (int i = 0; i < 18; i++)
            {
                var picker = new Picker
                {
                    ItemsSource = clubDistances.Select(c => c.Club).ToList(),
                    SelectedIndex = changeClubSelections[i] == null ? -1 : clubDistances.FindIndex(c => c.Club == changeClubSelections[i]),
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    BackgroundColor = MauiColors.Transparent
                };
                int holeIndex = i;
                picker.SelectedIndexChanged += (s, e) =>
                {
                    changeClubSelections[holeIndex] = picker.SelectedIndex >= 0 ? picker.ItemsSource[picker.SelectedIndex] as string : null;
                    OnChangeClubPickerChanged(picker, EventArgs.Empty);
                };
                changeClubPickers[i] = picker;
                courseGrid.Add(picker, i + 1, changeClubRowIdx);
            }
            courseGrid.Add(new Label { Text = "", BackgroundColor = MauiColors.Transparent }, 19, changeClubRowIdx);

            if (highlightStrokes == null)
            {
                previousStrokesPerHole = strokesPerHole.ToArray();
            }

            courseGrid.IsVisible = true;
        }

        private int[] CalculateStrokesPerHoleArray(GolfCourse course, string[] changeClubOverride)
        {
            var strokesPerHole = new int[18];
            for (int h = 0; h < 18; h++)
            {
                var strokes = CalculateStrokesForHole(course.HoleYardages[h], changeClubOverride?[h]);
                int count = 0;
                for (int i = 2; i < strokes.Count; i += 2)
                {
                    if (strokes[i] != "-") count++;
                }
                strokesPerHole[h] = count;
            }
            return strokesPerHole;
        }

        private string GetClubForDistance(int distance)
        {
            if (clubDistances == null || clubDistances.Count == 0)
                return "Unknown";

            var sortedClubs = clubDistances.OrderByDescending(c => c.MaxDistance).ToList();
            foreach (var club in sortedClubs)
            {
                if (distance >= club.MaxDistance)
                    return club.Club;
            }
            return sortedClubs.Last().Club;
        }

        private List<string> CalculateStrokesForHole(int yardage, string overrideClub = null)
        {
            var result = new List<string>();
            var remaining = yardage;
            var puttCount = 0;
            var zeroYardageCount = 0;
            var dashMode = false;
            bool driverUsed = false;

            // Defensive: ensure clubDistances is loaded
            if (clubDistances == null || clubDistances.Count == 0)
                LoadClubDistances();

            result.Add(remaining > 0 ? remaining.ToString() : "0");
            if (remaining == 0) zeroYardageCount++;

            var strokes = 0;
            while (strokes < 6)
            {
                string club = "";

                if (dashMode)
                {
                    club = "-";
                }
                else if (remaining <= 0)
                {
                    if (puttCount < 2)
                    {
                        club = "Putt";
                        puttCount++;
                    }
                    else
                    {
                        club = "-";
                        dashMode = true;
                    }
                }
                else if (strokes == 0 && !string.IsNullOrEmpty(overrideClub))
                {
                    club = overrideClub;
                }
                else if (strokes == 0)
                {
                    // Always use the selected default driver for the first shot if the hole is long enough
                    var defaultDriver = clubDistances.FirstOrDefault(c => c.Club == selectedDefaultDriver);
                    if (defaultDriver.Club != null && remaining >= defaultDriver.MaxDistance)
                    {
                        club = defaultDriver.Club;
                        if (club.Equals("Drive", StringComparison.OrdinalIgnoreCase))
                            driverUsed = true;
                    }
                    else
                    {
                        // If the hole is too short for the default driver, use the best available club for the distance
                        club = GetClubForDistance(remaining);
                    }
                }
                else
                {
                    // For all shots after the first, do not use Driver if it was already used
                    var availableClubs = clubDistances
                        .Where(c => !driverUsed || !c.Club.Equals("Drive", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(c => c.MaxDistance)
                        .ToList();

                    if (driverUsed)
                    {
                        club = availableClubs
                            .FirstOrDefault(c =>
                                (c.Club.Equals("3 Wood", StringComparison.OrdinalIgnoreCase) ||
                                 c.Club.Equals("5 Wood", StringComparison.OrdinalIgnoreCase) ||
                                 c.Club.Equals("4 Hybrid", StringComparison.OrdinalIgnoreCase) ||
                                 c.Club.Equals("5 Iron", StringComparison.OrdinalIgnoreCase)) &&
                                remaining >= c.MaxDistance)
                            .Club;

                        if (string.IsNullOrEmpty(club))
                        {
                            var clubTuple = availableClubs.FirstOrDefault(c => remaining >= c.MaxDistance);
                            club = !string.IsNullOrEmpty(clubTuple.Club) ? clubTuple.Club : (availableClubs.Count > 0 ? availableClubs.Last().Club : "Unknown");
                        }
                    }
                    else
                    {
                        var clubTuple = availableClubs.FirstOrDefault(c => remaining >= c.MaxDistance);
                        club = !string.IsNullOrEmpty(clubTuple.Club) ? clubTuple.Club : (availableClubs.Count > 0 ? availableClubs.Last().Club : "Unknown");
                    }
                }

                result.Add(club);
                strokes++;

                string nextYardage;
                if (dashMode || club == "-")
                {
                    nextYardage = "-";
                }
                else if (club == "Putt")
                {
                    nextYardage = "0";
                    remaining = 0;
                }
                else
                {
                    var clubTuple = clubDistances.FirstOrDefault(c => c.Club == club);
                    var clubDist = !string.IsNullOrEmpty(clubTuple.Club) ? clubTuple.MaxDistance : 0;
                    remaining -= clubDist;
                    nextYardage = remaining > 0 ? remaining.ToString() : "0";
                }

                if (!dashMode && nextYardage == "0")
                {
                    zeroYardageCount++;
                    if (zeroYardageCount > 2)
                    {
                        dashMode = true;
                        nextYardage = "-";
                    }
                }
                result.Add(nextYardage);

                if (dashMode && result.Count < 13)
                {
                    while (result.Count < 13)
                    {
                        result.Add("-");
                    }
                    break;
                }
            }

            while (result.Count < 13)
            {
                result.Add("");
            }

            return result;
        }

        // PDF Export using QuestPDF
        public void SaveDisplayToPdf()
        {
            string courseName = courseNameLabel.Text ?? "Course";

            // ✅ NEW: Save to Proton Drive Golf folder for ANY Windows user
            string golfFolder = ProtonGolfFolder;
            Directory.CreateDirectory(golfFolder);

            // Optional: keep same file name, or add timestamp to avoid overwriting
            string filePath = Path.Combine(golfFolder, "GolfClubPlan.pdf");

            var tableRows = GetShotTableData();

            var lightBlue = PdfColors.Blue.Lighten3;
            var yellow = PdfColors.Yellow.Lighten3;
            var amber = PdfColors.Orange.Lighten2;
            var lightGreen = PdfColors.Green.Lighten4;
            var white = PdfColors.White;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter.Landscape());
                    page.Margin(18);

                    page.Header()
                        .Background(lightBlue)
                        .Padding(4)
                        .Text(courseName)
                        .FontSize(9)
                        .Bold();

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2f);
                            for (int i = 0; i < 18; i++)
                                columns.RelativeColumn(1f);
                        });

                        for (int rowIdx = 0; rowIdx < tableRows.Count; rowIdx++)
                        {
                            var row = tableRows[rowIdx];
                            var firstCell = row[0];

                            var bgColor = white;
                            if (firstCell == "Hole")
                                bgColor = yellow;
                            else if (firstCell == "Handicap" || firstCell == "Bogie")
                                bgColor = amber;
                            else if (firstCell == "Par")
                                bgColor = yellow;
                            else if (firstCell == "Strokes")
                                bgColor = lightGreen;
                            else if (firstCell.StartsWith("Yardage"))
                                bgColor = lightGreen;
                            else if (firstCell.StartsWith("Stroke"))
                                bgColor = yellow;

                            for (int colIdx = 0; colIdx < row.Count; colIdx++)
                            {
                                var cell = table.Cell()
                                    .Element(c => CellStyle(c.Background(bgColor)))
                                    .Text(row[colIdx])
                                    .FontSize(8);

                                if (colIdx == 0)
                                    cell = cell.Bold();
                            }
                        }

                        PdfContainer CellStyle(PdfContainer container) =>
                            container.PaddingVertical(1).PaddingHorizontal(2).AlignCenter();
                    });
                });
            })
            .GeneratePdf(filePath);

            DisplayAlert("PDF Saved", $"PDF saved to:\n{filePath}", "OK");
        }

        private List<List<string>> GetShotTableData()
        {
            var rows = new List<List<string>>();
            int totalColumns = 19;

            rows.Add(Enumerable.Repeat(string.Empty, totalColumns).ToList());

            var holesRow = new List<string> { "Hole" };
            for (int i = 1; i <= 18; i++) holesRow.Add(i.ToString());
            rows.Add(holesRow);

            rows.Add(Enumerable.Repeat(string.Empty, totalColumns).ToList());

            if (coursePicker.SelectedItem != null && courses.TryGetValue(coursePicker.SelectedItem.ToString(), out var course))
            {
                var allHoleStrokes = new List<List<string>>();
                for (var h = 0; h < 18; h++)
                    allHoleStrokes.Add(CalculateStrokesForHole(course.HoleYardages[h], changeClubSelections[h]));

                for (int s = 0; s < 6; s++)
                {
                    var yardageRow = new List<string> { $"Yardage {s + 1}" };
                    for (int h = 0; h < 18; h++)
                        yardageRow.Add(allHoleStrokes[h][s * 2]);
                    rows.Add(yardageRow);

                    var strokeRow = new List<string> { $"Stroke {s + 1}" };
                    for (int h = 0; h < 18; h++)
                        strokeRow.Add(allHoleStrokes[h][s * 2 + 1]);
                    rows.Add(strokeRow);

                    if (s < 5)
                    {
                        rows.Add(Enumerable.Repeat(string.Empty, totalColumns).ToList());
                    }
                }
            }

            return rows;
        }

        private async void OnReturnClicked(object sender, EventArgs e)
        {
            await Navigation.PopToRootAsync();
        }

        private void OnSaveToPdfClicked(object sender, EventArgs e)
        {
            SaveDisplayToPdf();
        }

        // ✅ Proton Drive sync logic (replaces OneDrive references)
        // If you later implement file copy/sync of ManagedFiles, use these paths.
        private static string ProtonDriveBase => ProtonGolfFolder;
        private static readonly string[] ManagedFiles = { "ClubAndDistance.txt", "GolfCourseData.txt" };

        // Call this after any DB update
        public async Task ExportToProtonDriveAsync()
        {
            await _golfDataService.ExportDatabaseAsync();
        }

        // Call this on startup
        public async Task ImportFromProtonDriveAsync()
        {
            MainThread.InvokeOnMainThreadAsync(async () => await _golfDataService.ImportDatabaseAsync());
        }
    }
}
