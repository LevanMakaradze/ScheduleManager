using ScheduleManager.Models;

namespace ScheduleManager.Services
{
    public class ScheduleService
    {
        private List<ClassOption> _classList;

        public ScheduleService(List<ClassOption> classList)
        {
            _classList = classList;
        }

        public List<List<ClassOption>> GenerateSchedules(List<SchedulePreference> preferences)
        {
            var subjects = _classList.Select(c => c.Subject).Distinct().ToList();
            var allSchedules = new List<List<ClassOption>>();

            void FindSchedules(List<ClassOption> currentSchedule, List<string> remainingSubjects)
            {
                if (!remainingSubjects.Any())
                {
                    allSchedules.Add(new List<ClassOption>(currentSchedule));
                    return;
                }

                string currentSubject = remainingSubjects.First();
                var classesForSubject = _classList
                    .Where(c => c.Subject.Equals(currentSubject, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var classOption in classesForSubject)
                {
                    if (!HasTimeConflict(currentSchedule, classOption))
                    {
                        currentSchedule.Add(classOption);
                        FindSchedules(currentSchedule, remainingSubjects.Skip(1).ToList());
                        currentSchedule.RemoveAt(currentSchedule.Count - 1);
                    }
                }
            }

            FindSchedules(new List<ClassOption>(), subjects);

            // Score and filter schedules
            var scoredSchedules = allSchedules
                .Select(schedule => new
                {
                    Schedule = schedule,
                    Score = CalculateScheduleScore(schedule, preferences)
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            var maxScore = scoredSchedules.FirstOrDefault()?.Score ?? 0;
            return scoredSchedules
                .Where(x => x.Score == maxScore)
                .Select(x => x.Schedule)
                .ToList();
        }

        private bool HasTimeConflict(List<ClassOption> schedule, ClassOption newClass) =>
            schedule.Any(existing =>
                existing.DayOfWeek == newClass.DayOfWeek &&
                !(existing.EndTime <= newClass.StartTime || existing.StartTime >= newClass.EndTime));

        public double CalculateScheduleScore(List<ClassOption> schedule, List<SchedulePreference> preferences)
        {
            double score = 0;
            double maxPossibleScore = 100.0;

            var weightedPreferences = new Dictionary<SchedulePreference, double>
    {
        { SchedulePreference.NoMorningClasses, 2.5 },
        { SchedulePreference.NoEveningClasses, 2.5 },
        { SchedulePreference.PreferMorningClasses, 2.0 },
        { SchedulePreference.PreferEveningClasses, 2.0 },
        { SchedulePreference.NoGapDays, 3.0 },
        { SchedulePreference.NoGapHours, 3.0 },
        { SchedulePreference.LeastClassDays, 1.5 },
        { SchedulePreference.MostClassDays, 1.5 },
        { SchedulePreference.MinimizeEarlyMorningClasses, 2.0 },
        { SchedulePreference.MaximizeBreakTimeBetweenClasses, 2.5 }
    };

            foreach (var pref in preferences)
            {
                double prefWeight = weightedPreferences[pref];

                score += pref switch
                {
                    SchedulePreference.NoMorningClasses =>
                        Math.Max(-10, CountMorningClasses(schedule) * -prefWeight),

                    SchedulePreference.NoEveningClasses =>
                        Math.Max(-10, CountEveningClasses(schedule) * -prefWeight),

                    SchedulePreference.PreferMorningClasses =>
                        CountMorningClasses(schedule) * prefWeight,

                    SchedulePreference.PreferEveningClasses =>
                        CountEveningClasses(schedule) * prefWeight,

                    SchedulePreference.NoGapDays =>
                        CountGapDays(schedule) == 0
                            ? schedule.Count * prefWeight
                            : Math.Max(-15, -CountGapDays(schedule) * prefWeight),

                    SchedulePreference.NoGapHours =>
                        CountGapHours(schedule) == 0
                            ? schedule.Count * prefWeight
                            : Math.Max(-15, -CountGapHours(schedule) * prefWeight),

                    SchedulePreference.LeastClassDays =>
                        Math.Max(-10, CountClassDays(schedule) * -prefWeight),

                    SchedulePreference.MostClassDays =>
                        CountClassDays(schedule) * prefWeight,

                    SchedulePreference.MinimizeEarlyMorningClasses =>
                        Math.Max(-10, CountEarlyMorningClasses(schedule) * -prefWeight),

                    SchedulePreference.MaximizeBreakTimeBetweenClasses =>
                        CalculateBreakTime(schedule) * prefWeight,

                    _ => 0
                };
            }

            return Math.Min(Math.Max(score, -maxPossibleScore), maxPossibleScore);
        }

        private int CountEarlyMorningClasses(List<ClassOption> schedule) =>
            schedule.Count(c => c.StartTime.Hour >= 6 && c.StartTime.Hour < 9);


        private double CalculateBreakTime(List<ClassOption> schedule)
        {
            double totalBreakTime = 0;
            var groupedByDay = schedule
                .GroupBy(c => c.DayOfWeek)
                .Select(g => g.OrderBy(c => c.StartTime).ToList());

            foreach (var day in groupedByDay)
            {
                for (int i = 0; i < day.Count - 1; i++)
                {
                    var breakTime = (day[i + 1].StartTime - day[i].EndTime).TotalHours;
                    totalBreakTime += breakTime > 1 ? breakTime : 0;
                }
            }

            return totalBreakTime;
        }

        private int CountGapDays(List<ClassOption> schedule)
        {
            var days = schedule.Select(c => (int)c.DayOfWeek).Distinct().OrderBy(d => d).ToList();
            int gapDays = 0;

            for (int i = 0; i < days.Count - 1; i++)
            {
                gapDays += (days[i + 1] - days[i] - 1); // Count the number of skipped days
            }

            return gapDays;
        }

        private int CountGapHours(List<ClassOption> schedule)
        {
            int gapHours = 0;

            var groupedByDay = schedule
                .GroupBy(c => c.DayOfWeek)
                .Select(g => g.OrderBy(c => c.StartTime).ToList())
                .ToList();

            foreach (var day in groupedByDay)
            {
                for (int i = 0; i < day.Count - 1; i++)
                {
                    var gap = (day[i + 1].StartTime - day[i].EndTime).TotalHours;
                    if (gap > 1)
                    {
                        gapHours += (int)(gap - 1); // Count only gaps greater than 1 hour
                    }
                }
            }

            return gapHours;
        }

        private int CountClassDays(List<ClassOption> schedule) =>
            schedule.Select(c => c.DayOfWeek).Distinct().Count();

        private int CountMorningClasses(List<ClassOption> schedule) =>
            schedule.Count(c => c.StartTime.Hour < 12);

        private int CountEveningClasses(List<ClassOption> schedule) =>
            schedule.Count(c => c.StartTime.Hour >= 18);


    }
}
