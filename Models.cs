using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScheduleManager.Models
{
    public class ClassOption : INotifyPropertyChanged
    {
        private string _subject;
        private string _lecturerName;
        private string _auditory;

        public string Subject
        {
            get => _subject;
            set => SetProperty(ref _subject, value);
        }

        public string LecturerName
        {
            get => _lecturerName;
            set => SetProperty(ref _lecturerName, value);
        }

        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string Auditory
        {
            get => _auditory;
            set => SetProperty(ref _auditory, value);
        }

        public ClassOption(string subject, string lecturerName, DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime, string auditory)
        {
            if (endTime <= startTime)
                throw new ArgumentException("End time must be after start time.");

            Subject = subject;
            LecturerName = lecturerName;
            DayOfWeek = dayOfWeek;
            StartTime = startTime;
            EndTime = endTime;
            Auditory = auditory;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public override string ToString() =>
            $"{Subject} ({LecturerName}) - {DayOfWeek} {StartTime:HH:mm}-{EndTime:HH:mm}";
    }

    public class ClassList
    {
        public List<ClassOption> Classes { get; set; } = new List<ClassOption>();

        public void AddClass(ClassOption newClass) => Classes.Add(newClass);
        public void RemoveClass(ClassOption classToRemove) => Classes.Remove(classToRemove);

        public List<ClassOption> GetSortedClasses() =>
            Classes.OrderBy(c => c.Subject)
                   .ThenBy(c => c.LecturerName)
                   .ThenBy(c => c.DayOfWeek)
                   .ThenBy(c => c.StartTime)
                   .ToList();
    }

    public enum SchedulePreference
    {
        NoMorningClasses,
        NoEveningClasses,
        PreferMorningClasses,
        PreferEveningClasses,
        NoGapDays,
        NoGapHours,
        LeastClassDays,
        MostClassDays,
        MinimizeEarlyMorningClasses,
        MaximizeBreakTimeBetweenClasses
    }
}
